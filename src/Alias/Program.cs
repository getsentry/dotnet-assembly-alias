using Mono.Cecil;
using Mono.Cecil.Rocks;
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

        var assemblyInfos = new List<AssemblyInfo>();

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
                        assemblyInfos.Add(new(name, file, targetName, targetPath, true));
                        isAliased = true;
                        continue;
                    }
                }

                if (name == assemblyToAlias)
                {
                    var targetName = $"{prefix}{name}{suffix}";
                    var targetPath = Path.Combine(fileDirectory, targetName + ".dll");
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

            var netstandard = resolver.Resolve(new AssemblyNameReference("netstandard", new Version()))!;
            var visibleToType = netstandard.MainModule.GetType("System.Runtime.CompilerServices", "InternalsVisibleToAttribute");
            var visibleToConstructor= visibleToType.GetConstructors().Single();
            foreach (var info in assemblyInfos)
            {
                var assemblyTargetPath = info.TargetPath;
                var (module, hasSymbols) = ModuleReaderWriter.Read(info.SourcePath, resolver);
                module.Assembly.Name.Name = info.TargetName;
                FixKey(keyPair, module);
                if (info.isAlias)
                {
                    AddVisibleTo(module, visibleToConstructor, assemblyInfos, publicKey);
                    MakeTypesInternal(module);
                }
                Redirect(module, assemblyInfos, publicKey);
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

        foreach (var assembly in assemblyInfos)
        {
            if (!assembly.isAlias)
            {
                continue;
            }

            File.Delete(assembly.SourcePath);
            File.Delete(Path.ChangeExtension(assembly.SourcePath, "pdb"));
        }
    }

    static void MakeTypesInternal(ModuleDefinition module)
    {
        foreach (var typeDefinition in module.Types)
        {
            typeDefinition.IsPublic = false;
        }
    }

    static void AddVisibleTo(ModuleDefinition module, MethodDefinition visibleToConstructor, List<AssemblyInfo> assemblyInfos, byte[]? publicKey)
    {
        var visibleToConstructorImported = module.ImportReference(visibleToConstructor);

        foreach (var info in assemblyInfos)
        {
            if (module.Assembly.Name.Name == info.TargetName)
            {
                continue;
            }
            var attribute = new CustomAttribute(visibleToConstructorImported);
            string value;
            if (publicKey == null)
            {
                value = info.TargetName;
            }
            else
            {
                value = $"{info.TargetName}, PublicKey={string.Concat(publicKey.Select(x => x.ToString("x2")).ToArray())}";
            }

            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, value));
            module.Assembly.CustomAttributes.Add(attribute);
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

    static void Redirect(ModuleDefinition targetModule, List<AssemblyInfo> assemblyInfos, byte[]? publicKey)
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
            toChange.PublicKey = publicKey;
        }
    }
}
