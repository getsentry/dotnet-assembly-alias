using CommandLine;

public static class CommandRunner
{
    public static IEnumerable<Error> RunCommand(Invoke invoke, params string[] args)
    {
        var arguments = Parser.Default.ParseArguments<Options>(args);

        if (arguments is NotParsed<Options> errors)
        {
            return errors.Errors;
        }

        var parsed = (Parsed<Options>) arguments;

        var options = parsed.Value;
        var targetDirectory = FindTargetDirectory(options.TargetDirectory);
        Console.WriteLine($"TargetDirectory: {targetDirectory}");
        var prefix = options.Prefix;
        if (prefix != null)
        {
            ValidatePrefixSuffix(prefix);
            Console.WriteLine($"Prefix: {prefix}");
        }
        var suffix = options.Suffix;
        if (suffix != null)
        {
            ValidatePrefixSuffix(suffix);
            Console.WriteLine($"Suffix: {suffix}");
        }

        if (prefix == null && suffix == null)
        {
            throw new ErrorException("Either prefix or suffix must be defined.");
        }

        var keyFile = options.Key;

        if (keyFile != null)
        {
            keyFile = Path.GetFullPath(keyFile);
            Console.WriteLine($"KeyFile: {keyFile}");
            if (!File.Exists(keyFile))
            {
                throw new ErrorException($"KeyFile directory does not exist: {keyFile}");
            }
        }

        Console.WriteLine("AssembliesToAlias:");
        var assemblyToAliases = options.AssembliesToAlias.ToList();
        foreach (var assembly in assemblyToAliases)
        {
            Console.WriteLine($" * {assembly}");
        }
        
        var assembliesToExclude = options.AssembliesToExclude.ToList();

        if (assembliesToExclude.Any())
        {
            Console.WriteLine("AssembliesToExclude:");
            foreach (var assembly in assembliesToExclude)
            {
                Console.WriteLine($" * {assembly}");
            }
        }

        var references = options.References.ToList();
        var referencesFile = Path.Combine(targetDirectory, "alias-references.txt");
        if (File.Exists(referencesFile))
        {
            references.AddRange(File.ReadAllLines(referencesFile));
        }

        if (options.ReferenceFile != null && File.Exists(options.ReferenceFile))
        {
            references.AddRange(File.ReadAllLines(options.ReferenceFile));
        }

        if (references.Any())
        {
            Console.WriteLine("References:");
            foreach (var reference in references)
            {
                Console.WriteLine($" * {reference}");
            }
        }

        invoke(
            targetDirectory, 
            assemblyToAliases,
            references, 
            keyFile, 
            assembliesToExclude,
            prefix,
            suffix,
            options.Internalize);
        return Enumerable.Empty<Error>();
    }

    static void ValidatePrefixSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ErrorException("Prefix/Suffix must not contain whitespace");
        }
    }

    static string FindTargetDirectory(string? targetDirectory)
    {
        if (targetDirectory == null)
        {
            return Environment.CurrentDirectory;
        }

        return Path.GetFullPath(targetDirectory);
    }
}