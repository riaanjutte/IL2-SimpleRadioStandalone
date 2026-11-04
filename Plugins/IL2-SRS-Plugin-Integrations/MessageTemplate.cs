using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>How event values are escaped before a template sees them.</summary>
    public enum TemplateEscape
    {
        /// <summary>Values are used as they are.</summary>
        None,

        /// <summary>
        /// Markdown characters are backslash-escaped and line breaks become spaces, so a pilot name such as
        /// <c>**Admin**</c> or <c>[click](https://…)</c> shows as plain text in Discord.
        /// </summary>
        DiscordMarkdown,

        /// <summary>Text is escaped for use inside a JSON string, e.g. <c>"name": "{{ client.name }}"</c>.</summary>
        Json
    }

    /// <summary>A template could not be parsed or rendered.</summary>
    public sealed class TemplateException : Exception
    {
        public TemplateException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Scriban templates over event data, e.g. <c>{{ client.name }} joined {{ client.coalitionName }}</c>.
    /// Templates run sandboxed: no includes, limited loops, recursion and run time, and a capped output.
    /// </summary>
    public static class MessageTemplate
    {
        public const int MaxTemplateLength = 8000;
        public const int MaxOutputLength = 32000;
        private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(2);

        private static readonly char[] MarkdownCharacters =
            { '\\', '*', '_', '~', '`', '|', '>', '#', '-', '[', ']', '(', ')', '<' };

        /// <summary>Checks that a template parses; returns an error message or null.</summary>
        public static string Check(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                return null;
            }

            if (template.Length > MaxTemplateLength)
            {
                return $"A template can be at most {MaxTemplateLength} characters.";
            }

            var parsed = Template.Parse(template);
            return parsed.HasErrors ? FirstError(parsed) : null;
        }

        /// <summary>Renders a template with the event data; throws <see cref="TemplateException"/> on errors.</summary>
        public static string Render(string template, JsonElement data, TemplateEscape escape)
        {
            var error = Check(template);
            if (error != null)
            {
                throw new TemplateException(error);
            }

            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            var globals = Convert(data, escape, null) as ScriptObject ?? new ScriptObject();
            if (escape != TemplateEscape.None)
            {
                // The same values without escaping, for dates and comparisons: {{ raw.client.name }}.
                globals["raw"] = Convert(data, TemplateEscape.None, null);
            }

            using var timeout = new CancellationTokenSource(RenderTimeout);
            var context = new TemplateContext
            {
                LoopLimit = 200,
                LoopLimitQueryable = 200,
                RecursiveLimit = 20,
                ObjectRecursionLimit = 20,
                LimitToString = 2000,
                RegexTimeOut = TimeSpan.FromMilliseconds(250),
                TemplateLoader = null,
                StrictVariables = false,
                EnableRelaxedMemberAccess = true,
                MemberRenamer = member => member.Name,
                CancellationToken = timeout.Token
            };
            context.PushGlobal(globals);

            string output;
            try
            {
                output = Template.Parse(template).Render(context);
            }
            catch (OperationCanceledException)
            {
                throw new TemplateException($"The template took longer than {RenderTimeout.TotalSeconds:0} seconds.");
            }
            catch (ScriptRuntimeException ex)
            {
                throw new TemplateException(ex.OriginalMessage ?? ex.Message);
            }

            return output.Length > MaxOutputLength ? output[..MaxOutputLength] : output;
        }

        /// <summary>
        /// Addresses set by an admin and checked to be http(s) URLs. They aren't Markdown-escaped: a backslash
        /// before a '-' or '_' would break the link in Discord.
        /// </summary>
        private static readonly HashSet<string> Links = new(StringComparer.Ordinal) { "server.adminUrl" };

        /// <summary>Escapes text for Discord so it shows as written, without formatting.</summary>
        public static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var builder = new StringBuilder(text.Length + 8);
            foreach (var c in text)
            {
                if (c is '\r' or '\n')
                {
                    builder.Append(' ');
                    continue;
                }

                if (Array.IndexOf(MarkdownCharacters, c) >= 0)
                {
                    builder.Append('\\');
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>Cuts text to a length, ending it with an ellipsis when it was longer.</summary>
        public static string Truncate(string text, int length)
        {
            return text == null || text.Length <= length ? text : text[..(length - 1)] + "…";
        }

        private static object Convert(JsonElement element, TemplateEscape escape, string path)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new ScriptObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        obj[property.Name] = Convert(property.Value, escape,
                            path == null ? property.Name : path + "." + property.Name);
                    }

                    return obj;
                case JsonValueKind.Array:
                    var array = new ScriptArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        array.Add(Convert(item, escape, path + "[]"));
                    }

                    return array;
                case JsonValueKind.String:
                    return escape == TemplateEscape.DiscordMarkdown && Links.Contains(path)
                        ? element.GetString()
                        : Escape(element.GetString(), escape);
                case JsonValueKind.Number:
                    return element.TryGetInt64(out var integer) ? integer : element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }

        private static string Escape(string text, TemplateEscape escape)
        {
            return escape switch
            {
                TemplateEscape.DiscordMarkdown => EscapeMarkdown(text),
                TemplateEscape.Json => JsonEncodedText.Encode(text, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).Value,
                _ => text
            };
        }

        private static string FirstError(Template parsed)
        {
            var message = parsed.Messages.FirstOrDefault(m => m.Type == Scriban.Parsing.ParserMessageType.Error);
            return message == null
                ? "The template is not valid."
                : $"Template error at line {message.Span.Start.Line + 1}, column {message.Span.Start.Column + 1}: " +
                  message.Message;
        }
    }
}
