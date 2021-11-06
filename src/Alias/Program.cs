using Alias;
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
        var publicKey = Array.Empty<byte>();
        if (keyFile != null)
        {
            var fileBytes = File.ReadAllBytes(keyFile);

            keyPair = new(fileBytes);
            publicKey = keyPair.PublicKey;
        }
        
        var allFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories)
            .Where(x=> !assembliesToExclude.Contains(x))
            .ToList();

        var assembliesToPatch = allFiles
            .Select(x => new FileAssembly(Path.GetFileNameWithoutExtension(x), x))
            .ToList();

        var assembliesToAlias = new List<AssemblyAlias>();

        foreach (var assemblyToAlias in assemblyNamesToAliases)
        {
            if (string.IsNullOrWhiteSpace(assemblyToAlias))
            {
                throw new ErrorException("Empty string in assembliesToAliasString");
            }

            void ProcessItem(FileAssembly item)
            {
                assembliesToPatch.Remove(item);
                assembliesToAlias.Add(new(item.Name, item.Path, $"{prefix}{item.Name}{suffix}", item.Path.Replace(".dll", "_Alias.dll")));
            }

            if (assemblyToAlias.EndsWith("*"))
            {
                var match = assemblyToAlias.TrimEnd('*');
                foreach (var item in assembliesToPatch.Where(x => x.Name.StartsWith(match)).ToList())
                {
                    ProcessItem(item);
                }
            }
            else
            {
                var item = assembliesToPatch.SingleOrDefault(x => x.Name == assemblyToAlias);
                if (item == null)
                {
                    throw new ErrorException($"Could not find {assemblyToAlias} in {directory}.");
                }

                ProcessItem(item);
            }
        }

        using var resolver = new AssemblyResolver(references);
        {
            var assembliesToCleanup = new List<ModuleDefinition>();
            var writes = new List<Action>();

            foreach (var assembly in assembliesToAlias)
            {
                var assemblyTargetPath = assembly.TargetPath;
                File.Delete(assemblyTargetPath);
                var (module, hasSymbols) = ModuleReaderWriter.Read(assembly.SourcePath, resolver);

                var name = module.Assembly.Name;
                name.Name = assembly.TargetName;
                FixKey(keyPair, name);
                Redirect(module, assembliesToAlias, publicKey);
                resolver.Add(module);
                writes.Add(() => ModuleReaderWriter.Write(keyPair, hasSymbols, module, assemblyTargetPath));
                assembliesToCleanup.Add(module);
            }

            foreach (var assembly in assembliesToPatch)
            {
                var assemblyPath = assembly.Path;
                var (module, hasSymbols) = ModuleReaderWriter.Read(assemblyPath, resolver);

                FixKey(keyPair, module.Assembly.Name);
                Redirect(module, assembliesToAlias, publicKey);
                resolver.Add(module);

                writes.Add(() => ModuleReaderWriter.Write(keyPair, hasSymbols, module, assemblyPath));
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
            File.Delete(assembly.SourcePath);
        }
    }

    static void FixKey(StrongNameKeyPair? key, AssemblyNameDefinition name)
    {
        if (key == null)
        {
            name.Hash = Array.Empty<byte>();
            name.PublicKey = Array.Empty<byte>();
            name.PublicKeyToken = Array.Empty<byte>();
        }
        else
        {
            name.PublicKey = key.PublicKey;
        }
    }

    static void Redirect(ModuleDefinition targetModule, List<AssemblyAlias> aliases, byte[] publicKey)
    {
        var assemblyReferences = targetModule.AssemblyReferences;
        foreach (var alias in aliases)
        {
            var toChange = assemblyReferences.SingleOrDefault(x => x.Name == alias.SourceName);
            if (toChange != null)
            {
                toChange.Name = alias.TargetName;
                toChange.PublicKey = publicKey;
            }
        }
    }
}
