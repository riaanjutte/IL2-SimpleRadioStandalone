using System.Collections.Generic;
using System.Linq;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Events
{
    /// <summary>
    /// A field in an event payload, addressed by a dotted path such as <c>client.name</c>. The example value
    /// is used for test runs and previews.
    /// </summary>
    public sealed record SrsEventField(string Path, string Type, string Description, object Example = null);

    /// <summary>
    /// One kind of event: its type name (e.g. <c>client.connected</c>), the event log category it is stored
    /// under, and the payload fields it carries (listed by the API at /event-types).
    /// </summary>
    public sealed class SrsEventType
    {
        public SrsEventType(string type, string category, string label, string description,
            params IEnumerable<SrsEventField>[] fields)
        {
            Type = type;
            Category = category;
            Label = label;
            Description = description;
            Fields = fields.SelectMany(group => group).ToArray();
        }

        public string Type { get; }
        public string Category { get; }
        public string Label { get; }
        public string Description { get; }
        public IReadOnlyList<SrsEventField> Fields { get; }

        public override string ToString() => Type;
    }
}
