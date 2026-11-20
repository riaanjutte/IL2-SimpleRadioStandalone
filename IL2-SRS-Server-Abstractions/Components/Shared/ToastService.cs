using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Components.Shared
{
    /// <summary>
    /// Shows short confirmations, such as "Saved Channel Limit.", as toasts at the top of the admin UI. One per
    /// browser tab (scoped to the circuit); inject it into a page, including a plugin's, and call <see cref="Show"/>.
    /// </summary>
    public sealed class ToastService
    {
        /// <summary>Raised for each toast; the layout's <see cref="Toasts"/> shows it.</summary>
        public event Action<string> Shown;

        public void Show(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Shown?.Invoke(message);
            }
        }
    }
}
