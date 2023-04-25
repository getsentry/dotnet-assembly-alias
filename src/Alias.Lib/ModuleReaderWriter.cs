using Mono.Cecil;
using Mono.Cecil.Cil;

static class ModuleReaderWriter
{
    public static (ModuleDefinition module, bool hasSymbols) Read(string file, IAssemblyResolver resolver)
    {
        try
        {
            return InnerRead(file, resolver);
        }
        catch (Exception exception)
        {
            throw new($"Failed to read: {file}", exception);
        }
    }

    static (ModuleDefinition module, bool hasSymbols) InnerRead(string file, IAssemblyResolver resolver)
    {
        var parameters = new ReaderParameters
        {
            AssemblyResolver = resolver,
            InMemory = true,
        };

        var module = ModuleDefinition.ReadModule(file, parameters);

        var hasSymbols = TryReadSymbols(module);

        return (module, hasSymbols);
    }

    public static bool TryReadSymbols(this ModuleDefinition module)
    {
        var hasSymbols = false;
        try
        {
            module.ReadSymbols();
            hasSymbols = true;
        }
        catch (SymbolsNotFoundException)
        {
        }

        return hasSymbols;
    }

    public static void Write(StrongNameKeyPair? key, bool hasSymbols, ModuleDefinition module, string file)
    {
        var parameters = new WriterParameters
        {
            WriteSymbols = hasSymbols
        };
        if (key != null)
        {
            parameters.StrongNameKeyPair = key;
        }

        try
        {
            module.Write(file, parameters);
        }
        catch (Exception ex)
        {
            throw new($"Could not write module {file}", ex);
        }
    }
}