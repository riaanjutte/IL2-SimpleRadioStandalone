using Ciribob.IL2.SimpleRadio.Standalone.Server.Admin;
using Microsoft.AspNetCore.Components;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Components.Shared
{
    /// <summary>
    /// Base for the server's admin pages that show live server state (see <see cref="LiveComponentBase"/>),
    /// with the admin service at hand.
    /// </summary>
    public abstract class LiveComponent : LiveComponentBase
    {
        [Inject] protected ServerAdminService Admin { get; set; }
    }
}
