using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

namespace FilenameObfuscator
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand("A tool for obfuscating file names.");
            rootCommand.AddValidator(x => "Select a command to continue. Run --help for more information.");

            var obfuscationCommand = CreateObfuscationCommand();
            rootCommand.AddCommand(obfuscationCommand);

            var deobfuscate = CreateDeobfuscationCommand();
            rootCommand.AddCommand(deobfuscate);

            return rootCommand.Invoke(args);
        }

        private static Command CreateDeobfuscationCommand()
        {
            var deobfuscate = new Command("deobfuscate", "Deobfuscates all file names in a given directory");
            deobfuscate.TreatUnmatchedTokensAsErrors = true;

            deobfuscate.AddOption(new Option(new[] { "-d", "-dir", "--directory" }, "Directory to deobfuscate.")
            {
                Argument = new Argument<DirectoryInfo>("directory").ExistingOnly(),
                Required = true
            });

            deobfuscate.AddOption(new Option(new[] { "-m", "-map", "--mapping" }, "The mapping file to be used for the deobfuscation process.")
            {
                Argument = new Argument<FileInfo>("mapping").ExistingOnly(),
                Required = true
            });

            deobfuscate.Handler = CommandHandler.Create<DirectoryInfo, FileInfo>(Deobfuscate);
            return deobfuscate;
        }

        private static Command CreateObfuscationCommand()
        {
            var obfuscate = new Command("obfuscate", "Obfuscates all file names in a given directory");
            obfuscate.TreatUnmatchedTokensAsErrors = true;

            obfuscate.AddOption(new Option(new[] { "-d", "-dir", "--directory" }, "Directory to obfuscate.")
            {
                Argument = new Argument<DirectoryInfo>("directory").ExistingOnly(),
                Required = true
            });

            obfuscate.AddOption(new Option(new[] { "-m", "-map", "--mapping" }, "The mapping file to be used for the obfuscation process. If the given file path exists it will be used, otherwise it will be generated for the given directory.")
            {
                Argument = new Argument<FileInfo>("mapping"),
                Required = true
            });

            obfuscate.AddOption(new Option(
                new[] { "-c", "--force-create" },
                "Force the creation of a new mapping file regardless of whether the one supplied exists.")
            {
                Argument = new Argument<bool>("force-create", () => false),
            });

            obfuscate.AddOption(new Option(
                new[] { "-a", "--force-apply" },
                "Force the application of a mapping file regardless of whether the one supplied is nonexistent.")
            {
                Argument = new Argument<bool>("force-apply", () => false),
            });

            obfuscate.AddOption(new Option(new[] { "-g", "--generate-only" }, "Create the mapping but do not apply it at this time. This option is only used when a new mapping is being created.")
            {
                Argument = new Argument<bool>("generate-only", () => false),
            });

            obfuscate.AddValidator(x =>
            {
                if (x.ValueForOption<bool>("--force-create") && x.ValueForOption<bool>("--force-apply"))
                    return "--force-create and --force-apply are mutually exclusive.";

                return null;
            });

            obfuscate.AddValidator(x =>
            {
                if (x.ValueForOption<bool>("--force-apply") && x.ValueForOption<bool>("--generate-only"))
                    return "--generate-only is incompatible with --force-apply.";

                return null;
            });

            obfuscate.AddValidator(x =>
            {
                var mapping = x.ValueForOption<FileInfo>("--mapping");

                if (mapping != null)
                {
                    if (mapping.Exists)
                    {
                        if (x.ValueForOption<bool>("--force-create") || x.ValueForOption<bool>("--generate-only"))
                            return $"Mapping file already exists: {mapping.FullName}";
                    }
                    else
                    {
                        if (x.ValueForOption<bool>("--force-apply"))
                            return $"Mapping file does not exist: {mapping.FullName}";
                    }
                }

                return null;
            });

            obfuscate.Handler = CommandHandler.Create<DirectoryInfo, FileInfo, bool, bool, bool>(Obfuscate);
            return obfuscate;
        }

        private static void Obfuscate(DirectoryInfo directory, FileInfo mapping, bool forceCreate, bool forceApply, bool generateOnly)
        {
            if (mapping.Exists)
            {
                if (forceCreate || generateOnly)
                    throw new IOException($"Mapping file already exists: {mapping.FullName}");

                using var fs = mapping.OpenRead();
                var fileMapping = FileMapping.Load(fs).Result;
                fileMapping.Apply(directory);
            }
            else
            {
                if (forceApply)
                    throw new FileNotFoundException($"Mapping file does not exist: {mapping.FullName}", mapping.FullName);

                var fileMapping = FileMapping.Create(directory);
                using var fs = mapping.OpenWrite();
                fileMapping.Save(fs).Wait();

                if (!generateOnly)
                    fileMapping.Apply(directory);
            }
        }

        private static void Deobfuscate(DirectoryInfo directory, FileInfo mapping)
        {
            using var fs = mapping.OpenRead();
            var fileMapping = FileMapping.Load(fs).Result;
            fileMapping.Reverse(directory);
        }
    }
}

