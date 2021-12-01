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
        string? suffix,
        bool internalize)
    {
        if (!Directory.Exists(directory))
        {
            throw new ErrorException($"Target directory does not exist: {directory}");
        }

        var allFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories)
            .Where(x => !assembliesToExclude.Contains(x))
            .ToList();

        StrongNameKeyPair? keyPair = null;
        byte[]? publicKey = null;
        if (keyFile != null)
        {
            var fileBytes = File.ReadAllBytes(keyFile);

            keyPair = new(fileBytes);
            publicKey = keyPair.PublicKey;
        }


        var assemblyInfos = new List<AssemblyInfo>();

        void ProcessFile(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var fileDirectory = Path.GetDirectoryName(file)!;
            var isAliased = false;
            foreach (var assemblyToAlias in assemblyNamesToAliases)
            {
                var targetName = $"{prefix}{name}{suffix}";
                var targetPath = Path.Combine(fileDirectory, $"{targetName}.dll");

                if (assemblyToAlias.EndsWith("*"))
                {
                    var match = assemblyToAlias.TrimEnd('*');
                    if (name.StartsWith(match))
                    {
                        assemblyInfos.Add(new(name, file, targetName, targetPath, true));
                        isAliased = true;
                    }

                    continue;
                }

                if (name == assemblyToAlias)
                {
                    assemblyInfos.Add(new(name, file, targetName, targetPath, true));
                    isAliased = true;
                    continue;
                }
            }

            if (!isAliased)
            {
                assemblyInfos.Add(new(name, file, name, file, false));
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

            foreach (var info in assemblyInfos)
            {
                var assemblyTargetPath = info.TargetPath;
                var (module, hasSymbols) = ModuleReaderWriter.Read(info.SourcePath, resolver);
                module.Assembly.Name.Name = info.TargetName;
                module.SeyKey(keyPair);
                if (info.isAlias && internalize)
                {
                    AddVisibleTo(module, resolver, assemblyInfos, keyPair);
                    module.MakeTypesInternal();
                }

                Redirect(module, assemblyInfos, keyPair);
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

        foreach (var assembly in assemblyInfos.Where(_ => _.isAlias))
        {
            File.Delete(assembly.SourcePath);
            File.Delete(Path.ChangeExtension(assembly.SourcePath, "pdb"));
        }
    }
    
    static void AddVisibleTo(ModuleDefinition module, AssemblyResolver resolver, List<AssemblyInfo> assemblyInfos, StrongNameKeyPair? key)
    {
        var constructorImported = module.ImportReference(resolver.VisibleToConstructor);

        var assembly = module.Assembly;

        foreach (var info in assemblyInfos)
        {
            if (assembly.Name.Name == info.TargetName)
            {
                continue;
            }

            var attribute = new CustomAttribute(constructorImported);
            string value;
            if (key == null)
            {
                value = info.TargetName;
            }
            else
            {
                value = $"{info.TargetName}, PublicKey={PublicKeyToString(key.PublicKey)}";
            }

            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, value));
            assembly.CustomAttributes.Add(attribute);
        }
    }

    static string PublicKeyToString(byte[] publicKey)
    {
        return string.Concat(publicKey.Select(x => x.ToString("x2")));
    }
    
    static void Redirect(ModuleDefinition targetModule, List<AssemblyInfo> assemblyInfos, StrongNameKeyPair? key)
    {
        var assemblyReferences = targetModule.AssemblyReferences;
        foreach (var info in assemblyInfos)
        {
            var toChange = assemblyReferences.SingleOrDefault(x => x.Name == info.SourceName);
            if (toChange == null)
            {
                continue;
            }

            toChange.Name = info.TargetName;
            toChange.PublicKey = key?.PublicKey;
        }
    }
}