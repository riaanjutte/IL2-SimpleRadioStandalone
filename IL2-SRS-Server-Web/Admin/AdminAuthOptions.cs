using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Hosting;
using Microsoft.Extensions.Configuration;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Admin
{
    /// <summary>
    /// Admin UI password, taken from SRS_ADMIN_PASSWORD or a file named by SRS_ADMIN_PASSWORD_FILE
    /// (for Docker secrets). The UI only runs without a password when SRS_ADMIN_AUTH_DISABLED=true.
    /// </summary>
    public sealed class AdminAuthOptions
    {
        public const string PasswordKey = "SRS_ADMIN_PASSWORD";
        public const string PasswordFileKey = "SRS_ADMIN_PASSWORD_FILE";
        public const string DisabledKey = "SRS_ADMIN_AUTH_DISABLED";
        public const int MinimumPasswordLength = 8;

        private readonly byte[] _passwordHash;

        /// <summary>Login cookie claim holding <see cref="SessionStamp"/>.</summary>
        public const string SessionStampClaim = "srs:session-stamp";

        private AdminAuthOptions(bool disabled, byte[] passwordHash)
        {
            Disabled = disabled;
            _passwordHash = passwordHash;
            SessionStamp = passwordHash == null
                ? null
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("session:" + Convert.ToHexString(passwordHash))));
        }

        public bool Disabled { get; }

        /// <summary>
        /// Changes when the password does. Login cookies carry it, so changing the password signs out every browser
        /// that logged in with the old one. Derived from the password hash, never the password itself.
        /// </summary>
        public string SessionStamp { get; }

        public static AdminAuthOptions FromConfiguration(IConfiguration configuration)
        {
            return Create(configuration[PasswordKey], configuration[PasswordFileKey], configuration[DisabledKey]);
        }

        internal static AdminAuthOptions Create(string password, string passwordFile, string disabled)
        {
            if (bool.TryParse(disabled, out var isDisabled) && isDisabled)
            {
                return new AdminAuthOptions(true, null);
            }

            if (string.IsNullOrEmpty(password) && !string.IsNullOrWhiteSpace(passwordFile))
            {
                try
                {
                    password = File.ReadAllText(passwordFile.Trim()).TrimEnd('\r', '\n');
                }
                catch (Exception ex)
                {
                    throw new ServerStartupException(
                        $"Unable to read the admin password from {PasswordFileKey} ({passwordFile}).", ex);
                }
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ServerStartupException(
                    $"No admin password is configured. Set {PasswordKey} (or {PasswordFileKey}) to protect the admin UI, " +
                    $"or set {DisabledKey}=true if access is already restricted another way.");
            }

            if (password.Length < MinimumPasswordLength)
            {
                throw new ServerStartupException(
                    $"{PasswordKey} must be at least {MinimumPasswordLength} characters long.");
            }

            return new AdminAuthOptions(false, Hash(password));
        }

        public bool Verify(string candidate)
        {
            if (Disabled)
            {
                return true;
            }

            // Compare fixed-length hashes so the comparison time does not depend on the input.
            return CryptographicOperations.FixedTimeEquals(_passwordHash, Hash(candidate ?? string.Empty));
        }

        private static byte[] Hash(string value)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(value));
        }
    }
}
