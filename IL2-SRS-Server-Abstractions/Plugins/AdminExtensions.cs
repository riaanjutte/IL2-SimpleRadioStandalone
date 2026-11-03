using System;
using System.Reflection;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins
{
    /// <summary>A link added to the admin UI's side navigation.</summary>
    public sealed record AdminNavItem(string Href, string Label);

    /// <summary>A panel added to the bottom of the Settings page (a component with no required parameters).</summary>
    public sealed record AdminSettingsSection(Type Component);

    /// <summary>An assembly with routable admin UI pages (components with <c>@page</c>).</summary>
    public sealed record AdminRazorAssembly(Assembly Assembly);

    /// <summary>A stylesheet every admin UI page loads, relative to the site root (e.g. plugins/my-plugin/my.css).</summary>
    public sealed record AdminStylesheet(string Href);

    /// <summary>
    /// What a plugin (or a host building on the server) adds to the admin UI. A plugin's own pages are found
    /// without <see cref="AddAdminPages"/>.
    /// </summary>
    public static class AdminExtensions
    {
        public static IServiceCollection AddAdminNavItem(this IServiceCollection services, string href, string label)
        {
            return services.AddSingleton(new AdminNavItem(href, label));
        }

        public static IServiceCollection AddAdminSettingsSection<T>(this IServiceCollection services)
            where T : Microsoft.AspNetCore.Components.IComponent
        {
            return services.AddSingleton(new AdminSettingsSection(typeof(T)));
        }

        public static IServiceCollection AddAdminPages(this IServiceCollection services, Assembly assembly)
        {
            return services.AddSingleton(new AdminRazorAssembly(assembly));
        }

        public static IServiceCollection AddAdminStylesheet(this IServiceCollection services, string href)
        {
            return services.AddSingleton(new AdminStylesheet(href));
        }

        /// <summary>The address of a file in a plugin's wwwroot folder, e.g. plugins/my-plugin/my.css.</summary>
        public static string PluginFile(string pluginId, string path)
        {
            return "plugins/" + pluginId + "/" + path.TrimStart('/');
        }
    }

    /// <summary>Registers event types a plugin publishes, so the event log and its filters know them.</summary>
    public static class EventTypeExtensions
    {
        public static IServiceCollection AddEventTypes(this IServiceCollection services, params SrsEventType[] types)
        {
            return services.AddSingleton(new EventTypeRegistration(types));
        }
    }
}
