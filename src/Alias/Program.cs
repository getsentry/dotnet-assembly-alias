using StrongNameKeyPair = Mono.Cecil.StrongNameKeyPair;

public static class Program
{
    static int Main(string[] args)
    {
        try
        {
            var errors = CommandRunner.RunCommand(Inner, args);

            if (errors.Any())
            {
                return 1;
            }

            return 0;
        }
        catch (ErrorException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    public static void Inner(
        string directory,
        List<string> assemblyNamesToAlias,
        List<string> references,
        string? keyFile,
        List<string> assembliesToExclude,
        string? prefix,
        string? suffix,
        bool internalize)
    {
        var list = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories).ToList();
        var allFiles = list.Where(x => !assembliesToExclude.Contains(x));

        var assemblyInfos = Finder.FindAssemblyInfos(assemblyNamesToAlias, allFiles, name => $"{prefix}{name}{suffix}")
            .ToList();

        var keyPair = GetKeyPair(keyFile);

        Aliaser.Run(references, internalize, assemblyInfos, keyPair);

        foreach (var assembly in assemblyInfos.Where(_ => _.isAlias))
        {
            File.Delete(assembly.SourcePath);
            File.Delete(Path.ChangeExtension(assembly.SourcePath, "pdb"));
        }
    }

    static StrongNameKeyPair? GetKeyPair(string? keyFile)
    {
        if (keyFile == null)
        {
            return null;
        }

        var fileBytes = File.ReadAllBytes(keyFile);
        return new(fileBytes);
    }
}