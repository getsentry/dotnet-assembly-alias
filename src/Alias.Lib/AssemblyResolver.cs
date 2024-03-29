using Alias;
using Mono.Cecil;
using Mono.Cecil.Rocks;

public class AssemblyResolver :
    IAssemblyResolver
{
    static ReaderParameters readerParameters = new(ReadingMode.Deferred)
    {
        ReadSymbols = false
    };

    static AssemblyResolver()
    {
        using var netStandardResource = typeof(AssemblyResolver).Assembly.GetManifestResourceStream("Alias.Lib.netstandard.dll");
        netStandard = AssemblyDefinition.ReadAssembly(
            netStandardResource,
            new(ReadingMode.Immediate)
            {
                ReadSymbols = false
            });
    }

    Dictionary<string, AssemblyDefinition> cache;

    static readonly AssemblyDefinition netStandard;

    public AssemblyResolver(IEnumerable<string> references)
    {

        cache = new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["netstandard"] = netStandard,
            ["mscorlib"] = netStandard,
        };
        foreach (var reference in references)
        {
            var assembly = GetAssembly(reference);
            cache[assembly.Name.Name] = assembly;
        }

        VisibleToConstructor = GetVisibleToConstructor(netStandard);
    }

    public MethodDefinition VisibleToConstructor { get; }

    static MethodDefinition GetVisibleToConstructor(AssemblyDefinition assembly)
    {
        var visibleToType = assembly.MainModule.GetType("System.Runtime.CompilerServices", "InternalsVisibleToAttribute");
        return visibleToType.GetConstructors().Single();
    }

    public void Add(ModuleDefinition module)
    {
        var assembly = module.Assembly;
        cache[assembly.Name.Name] = assembly;
    }

    static AssemblyDefinition GetAssembly(string file)
    {
        try
        {
            return AssemblyDefinition.ReadAssembly(file, readerParameters);
        }
        catch (Exception exception)
        {
            throw new($"Could not read '{file}'.", exception);
        }
    }

    public AssemblyDefinition? Resolve(AssemblyNameReference name) =>
        Resolve(name, new());

    public AssemblyDefinition? Resolve(AssemblyNameReference name, ReaderParameters? parameters)
    {
        if (cache.TryGetValue(name.Name, out var assembly))
        {
            return assembly;
        }

        throw new ErrorException($"Could not load assembly: {name.Name}. It may need to exist in the target directory, or be added to the reference list.");
    }

    public void Dispose()
    {
        foreach (var value in cache.Values)
        {
            value.Dispose();
        }
    }
}