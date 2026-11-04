using System;
using System.Collections.Generic;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Events
{
    /// <summary>Publishes typed events and hands them to subscribers, including the event log.</summary>
    public interface IEventBus
    {
        /// <summary>
        /// Raised for every published event. Handlers run on the publishing thread, which can be the voice and
        /// sync server: queue the event and return; never block or do I/O in the handler. A handler that throws
        /// is logged and skipped.
        /// </summary>
        event Action<SrsEvent> EventPublished;

        void Publish(SrsEvent srsEvent);
    }

    /// <summary>
    /// Every event type known to this server: the built-in ones and those plugins add with
    /// <see cref="Plugins.EventTypeExtensions.AddEventTypes"/>.
    /// </summary>
    public interface IEventTypeRegistry
    {
        IReadOnlyList<SrsEventType> All { get; }

        /// <summary>The event type with this name, or null.</summary>
        SrsEventType Find(string type);
    }

    /// <summary>Event types a plugin publishes, registered with <see cref="Plugins.EventTypeExtensions.AddEventTypes"/>.</summary>
    public sealed record EventTypeRegistration(IReadOnlyList<SrsEventType> Types);
}
