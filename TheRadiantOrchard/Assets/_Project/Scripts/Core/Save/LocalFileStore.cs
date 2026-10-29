using System;
using System.IO;
using System.Text;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// Save files in one folder (Application.persistentDataPath in the game): save.json, save.bak, save.tmp.
    /// Writes go to save.tmp first, then replace save.json, keeping the old save.json as save.bak.
    /// Synchronous on purpose: the save is a few KB and must finish inside OnApplicationPause on mobile.
    /// </summary>
    public sealed class LocalFileStore : ISaveStore
    {
        public const string MainFileName = "save.json";
        public const string BackupFileName = "save.bak";
        public const string TempFileName = "save.tmp";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _directory;
        private readonly string _mainPath;
        private readonly string _backupPath;
        private readonly string _tempPath;

        public LocalFileStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            _directory = directory;
            _mainPath = Path.Combine(directory, MainFileName);
            _backupPath = Path.Combine(directory, BackupFileName);
            _tempPath = Path.Combine(directory, TempFileName);
        }

        public string MainPath => _mainPath;
        public string BackupPath => _backupPath;
        public string TempPath => _tempPath;

        public string Read() => ReadIfExists(_mainPath);

        public string ReadBackup() => ReadIfExists(_backupPath);

        public void Write(string json)
        {
            Directory.CreateDirectory(_directory);
            WriteFlushed(_tempPath, json ?? string.Empty);

            if (!File.Exists(_mainPath))
            {
                File.Move(_tempPath, _mainPath);
                return;
            }

            try
            {
                File.Replace(_tempPath, _mainPath, _backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceByCopy();
            }
            catch (IOException)
            {
                ReplaceByCopy();
            }
        }

        public void Delete()
        {
            DeleteIfExists(_mainPath);
            DeleteIfExists(_backupPath);
            DeleteIfExists(_tempPath);
        }

        // Fallback where File.Replace is unavailable: the backup is in place before save.json is touched,
        // so a crash between steps still leaves a readable save.bak.
        private void ReplaceByCopy()
        {
            File.Copy(_mainPath, _backupPath, true);
            File.Delete(_mainPath);
            File.Move(_tempPath, _mainPath);
        }

        private static void WriteFlushed(string path, string text)
        {
            byte[] bytes = Utf8NoBom.GetBytes(text);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static string ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path, Utf8NoBom) : null;

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
