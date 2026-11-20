using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Admin
{
    /// <summary>The actor recorded for changes made from the admin UI.</summary>
    public sealed class AdminActor : IAdminActor
    {
        public AdminActor(AdminAuthOptions options)
        {
            Name = options.Disabled ? "admin (no login)" : "admin";
        }

        public string Name { get; }
    }
}
