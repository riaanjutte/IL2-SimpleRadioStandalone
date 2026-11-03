namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Audit
{
    /// <summary>How long the event log keeps events; plugins keeping their own history can follow it.</summary>
    public sealed class AuditRetention
    {
        public const string DaysKey = "SRS_EVENT_RETENTION_DAYS";
        public const string MaxRowsKey = "SRS_EVENT_MAX_ROWS";

        public int Days { get; init; } = 30;
        public int MaxRows { get; init; } = 100_000;
    }
}
