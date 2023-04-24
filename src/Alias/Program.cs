using System.Text;
using Alias;
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
        bool internalize,
        Action<string> log)
    {
        var list = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories).ToList();
        
        var allFiles = Finder.FilterAssemblies(assembliesToExclude, list)
            .ToList();

        var assemblyInfos = Finder.FindAssemblyInfos(assemblyNamesToAlias, allFiles, prefix, suffix)
            .ToList();

        var builder = new StringBuilder("Resolved assemblies to alias:");
        builder.AppendLine();
        foreach (var assemblyInfo in assemblyInfos.Where(_ => _.IsAlias))
        {
            builder.AppendLine($" * {assemblyInfo.SourceName}");
        }

        log(builder.ToString());

        var keyPair = GetKeyPair(keyFile);

        Aliaser.Run(references, assemblyInfos, internalize, keyPair);

        foreach (var assembly in assemblyInfos.Where(_ => _.IsAlias))
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