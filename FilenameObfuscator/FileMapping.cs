using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FilenameObfuscator
{
    public class FileMapping
    {
        private Dictionary<string, string> mapping;

        private FileMapping(Dictionary<string, string> mapping)
        {
            this.mapping = mapping;
        }

        public static FileMapping Create(DirectoryInfo dir)
        {
            if (!dir.Exists)
                throw new DirectoryNotFoundException();

            var mapping = new Dictionary<string, string>();
            var options = new EnumerationOptions
            {
                AttributesToSkip = 0,
                RecurseSubdirectories = true
            };

            foreach (var file in dir.EnumerateFiles("*", options))
            {
                var originalPath = Path.GetRelativePath(dir.FullName, file.FullName);
                string obfuscatedName;

                do
                {
                    obfuscatedName = Path.GetRandomFileName();
                    var obfuscatedPath = Path.Join(dir.FullName, obfuscatedName);

                    if (File.Exists(obfuscatedPath) || Directory.Exists(obfuscatedPath))
                        continue;

                } while (!mapping.TryAdd(obfuscatedName, originalPath));
            }

            return new FileMapping(mapping);
        }

        public static async Task<FileMapping> Load(Stream stream)
        {
            return new FileMapping(await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream));
        }

        public async Task Save(Stream stream)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };

            await JsonSerializer.SerializeAsync(stream, mapping, options);
        }

        public void Apply(DirectoryInfo dir)
        {
            foreach (var (obfuscatedName, originalRelativePath) in mapping)
            {
                var originalPath = Path.Join(dir.FullName, originalRelativePath);

                if (!File.Exists(originalPath))
                    continue;

                var obfuscatedPath = Path.Join(dir.FullName, obfuscatedName);

                File.Move(originalPath, obfuscatedPath, false);
            }
        }

        public void Reverse(DirectoryInfo dir)
        {
            foreach (var (obfuscatedName, originalRelativePath) in mapping)
            {
                var obfuscatedPath = Path.Join(dir.FullName, obfuscatedName);

                if (!File.Exists(obfuscatedPath))
                    continue;

                var originalPath = Path.Join(dir.FullName, originalRelativePath);

                var originalParent = Path.GetDirectoryName(originalPath);

                if (!Directory.Exists(originalParent))
                    Directory.CreateDirectory(originalParent);

                File.Move(obfuscatedPath, originalPath, false);
            }
        }
    }
}

