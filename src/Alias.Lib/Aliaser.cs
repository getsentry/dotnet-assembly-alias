using Mono.Cecil;

public static class Aliaser
{
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
                value = $"{info.TargetName}, PublicKey={key.PublicKeyString()}";
            }

            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, value));
            assembly.CustomAttributes.Add(attribute);
        }
    }

    public static void Run(List<string> references, bool internalize, List<AssemblyInfo> assemblyInfos, StrongNameKeyPair? keyPair)
    {
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
    }
}