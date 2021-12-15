using Mono.Cecil;

namespace Alias;

public static class Aliaser
{
    public static void Run(
        IEnumerable<string> references,
        IEnumerable<SourceTargetInfo> infos,
        bool internalize,
        StrongNameKeyPair? key,
        IEnumerable<string> internalsVisibleNames)
    {
        var infoList = infos.ToList();
        using var resolver = new AssemblyResolver(references);
        var assembliesToCleanup = new List<ModuleDefinition>();
        var writes = new List<Action>();
        var internalsVisibleNamesList = internalsVisibleNames.ToList();
        foreach (var info in infoList)
        {
            var (module, hasSymbols) = ModuleReaderWriter.Read(info.SourcePath, resolver);
            module.Assembly.Name.Name = info.TargetName;
            module.SeyKey(key);
            if (info.isAlias && internalize)
            {
                AddVisibleTo(module, resolver, infoList, key, internalsVisibleNamesList);
                module.MakeTypesInternal();
            }

            Redirect(module, infoList, key);
            resolver.Add(module);
            writes.Add(() => ModuleReaderWriter.Write(key, hasSymbols, module, info.TargetPath));
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

    static void Redirect(ModuleDefinition targetModule, List<SourceTargetInfo> assemblyInfos, StrongNameKeyPair? key)
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

    static void AddVisibleTo(ModuleDefinition module, AssemblyResolver resolver, List<SourceTargetInfo> assemblyInfos, StrongNameKeyPair? key, IEnumerable<string> internalsVisibleNames)
    {
        var constructorImported = module.ImportReference(resolver.VisibleToConstructor);

        var assembly = module.Assembly;

        void AddAttribute(AssemblyDefinition assembly, string name)
        {
            var attribute = new CustomAttribute(constructorImported);
            string value;
            if (key == null)
            {
                value = name;
            }
            else
            {
                value = $"{name}, PublicKey={key.PublicKeyString()}";
            }

            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, value));
            assembly.CustomAttributes.Add(attribute);
        }

        foreach (var internalsVisibleName in internalsVisibleNames)
        {
            AddAttribute(assembly, internalsVisibleName);
        }

        foreach (var info in assemblyInfos)
        {
            var name = info.TargetName;
            if (assembly.Name.Name == name)
            {
                continue;
            }

            AddAttribute(assembly, name);
        }
    }
}