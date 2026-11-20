using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>
    /// Encrypts secrets stored in the database (integration webhook URLs and API keys) with the server's
    /// data protection keys in the data directory's keys folder. Back up that folder with srs.db: without
    /// it, stored secrets cannot be read and have to be entered again.
    /// </summary>
    public sealed class SecretProtector
    {
        private const string Prefix = "protected:";

        private readonly IDataProtector _protector;

        public SecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("IL2-SRS.Integrations.Secrets.v1");
        }

        public string Protect(string secret)
        {
            return Prefix + _protector.Protect(secret);
        }

        /// <summary>The secret, or null when it is not a protected value or the key that protected it is gone.</summary>
        public string Unprotect(string stored)
        {
            if (stored == null || !stored.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                return _protector.Unprotect(stored.Substring(Prefix.Length));
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        /// <summary>A hint that identifies a stored secret without revealing it, e.g. "ends in 0Jdl".</summary>
        public static string Hint(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return "unreadable; enter it again";
            }

            return secret.Length <= 8 ? "set" : "ends in " + secret.Substring(secret.Length - 4);
        }
    }
}
