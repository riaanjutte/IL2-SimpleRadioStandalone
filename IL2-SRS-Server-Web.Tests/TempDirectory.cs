using System;
using System.IO;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Tests
{
    internal sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "srs-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public SrsDatabase CreateDatabase() => new SrsDatabase(File(SrsDatabase.FileName));

        public void Dispose()
        {
            // Only this directory's pools: clearing them all would close connections other tests are using.
            foreach (var database in Directory.EnumerateFiles(Path, "*.db", SearchOption.AllDirectories))
            {
                SqliteDatabase.ClearPool(database);
            }

            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
