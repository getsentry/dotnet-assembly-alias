using Mono.Cecil;
using StrongNameKeyPair = Mono.Cecil.StrongNameKeyPair;

public static class Program
{
    static int Main(string[] args)
    {
        var errors = CommandRunner.RunCommand(Inner, args);

        if (errors.Any())
        {
            return 1;
        }

        return 0;
    }

    public static void Inner(
        string directory,
        List<string> assemblyNamesToAliases,
        List<string> references,
        string? keyFile,
        List<string> assembliesToExclude,
        string? prefix,
        string? suffix)
    {
        if (!Directory.Exists(directory))
        {
            throw new ErrorException($"Target directory does not exist: {directory}");
        }

        StrongNameKeyPair? keyPair = null;
        byte[]? publicKey = null;
        if (keyFile != null)
        {
            var fileBytes = File.ReadAllBytes(keyFile);

            keyPair = new(fileBytes);
            publicKey = keyPair.PublicKey;
        }

        var allFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories)
            .Where(x => !assembliesToExclude.Contains(x))
            .ToList();

        var assembliesToAlias = new List<AssemblyAlias>();

        void ProcessFile(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var fileDirectory = Path.GetDirectoryName(file)!;
            var isAliased = false;
            foreach (var assemblyToAlias in assemblyNamesToAliases)
            {
                if (assemblyToAlias.EndsWith("*"))
                {
                    var match = assemblyToAlias.TrimEnd('*');
                    if (name.StartsWith(match))
                    {
                        var targetName = $"{prefix}{name}{suffix}";
                        var targetPath = Path.Combine(fileDirectory, targetName + ".dll");
                        assembliesToAlias.Add(new(name, file, targetName, targetPath));
                        isAliased = true;
                        continue;
                    }
                }

                if (name == assemblyToAlias)
                {
                    var targetName = $"{prefix}{name}{suffix}";
                    var targetPath = Path.Combine(fileDirectory, targetName + ".dll");
                    assembliesToAlias.Add(new(name, file, targetName, targetPath));
                    isAliased = true;
                    continue;
                }
            }

            if (!isAliased)
            {
                assembliesToAlias.Add(new(name, file, name, file));
            }
        }

        foreach (var file in allFiles)
        {
            ProcessFile(file);
        }
        
        using var resolver = new AssemblyResolver(references);
        {
            var assembliesToCleanup = new List<ModuleDefinition>();
            var writes = new List<Action>();

            foreach (var assembly in assembliesToAlias)
            {
                var assemblyTargetPath = assembly.TargetPath;
                var (module, hasSymbols) = ModuleReaderWriter.Read(assembly.SourcePath, resolver);
                module.Assembly.Name.Name = assembly.TargetName;
                FixKey(keyPair, module);
                Redirect(module, assembliesToAlias, publicKey);
                resolver.Add(module);
                writes.Add(() => ModuleReaderWriter.Write(keyPair, hasSymbols, module, assemblyTargetPath));
                assembliesToCleanup.Add(module);
            }

            foreach (var write in writes)
            {
                write();
            }

            foreach (var assembly in assembliesToCleanup)
            {
                assembly.Dispose();
            }
        }

        foreach (var assembly in assembliesToAlias)
        {
            if (assembly.SourceName == assembly.TargetName)
            {
                continue;
            }

            File.Delete(assembly.SourcePath);
            File.Delete(Path.ChangeExtension(assembly.SourcePath, "pdb"));
        }
    }

    static void FixKey(StrongNameKeyPair? key, ModuleDefinition module)
    {
        if (key == null)
        {
            module.Assembly.Name.PublicKey = null;
            module.Attributes &= ~ModuleAttributes.StrongNameSigned;
            return;
        }

        module.Assembly.Name.PublicKey = key.PublicKey;
    }

    static void Redirect(ModuleDefinition targetModule, List<AssemblyAlias> aliases, byte[]? publicKey)
    {
        var assemblyReferences = targetModule.AssemblyReferences;
        foreach (var alias in aliases)
        {
            var toChange = assemblyReferences.SingleOrDefault(x => x.Name == alias.SourceName);
            if (toChange == null)
            {
                continue;
            }

            toChange.Name = alias.TargetName;
            toChange.PublicKey = publicKey;
        }
    }
}
