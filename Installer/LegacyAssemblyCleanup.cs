using System.IO;

namespace Installer
{
    internal static class LegacyAssemblyCleanup
    {
        public static bool RemoveExternalCommonAssembly(string installPath)
        {
            // A leftover DLL takes precedence over the current client and server's embedded copy.
            string path = Path.Combine(installPath, "DCS-SR-Common.dll");
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
    }
}
