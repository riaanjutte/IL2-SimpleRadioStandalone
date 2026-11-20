using System;
using System.Collections.Generic;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Components.Shared
{
    /// <summary>
    /// Decides when a form shows a field's error: once the field has been left (or changed and saved), or after
    /// the form was submitted. Until then an empty form isn't covered in errors.
    /// </summary>
    public sealed class FormState
    {
        private readonly HashSet<string> _touched = new(StringComparer.Ordinal);

        public bool Submitted { get; private set; }

        public void Touch(string field) => _touched.Add(field);

        public void Submit() => Submitted = true;

        /// <summary>Starts over, e.g. after a successful submit cleared the form.</summary>
        public void Reset()
        {
            _touched.Clear();
            Submitted = false;
        }

        /// <summary>The error to show for a field now: <paramref name="error"/> once the field counts as touched.</summary>
        public string Show(string field, string error)
        {
            return error != null && (Submitted || _touched.Contains(field)) ? error : null;
        }
    }
}
