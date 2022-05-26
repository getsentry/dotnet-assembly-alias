namespace Alias;

public static class Finder
{
    public static IEnumerable<SourceTargetInfo> FindAssemblyInfos(
        IEnumerable<string> assemblyNamesToAlias,
        IEnumerable<string> allFiles,
        string? prefix,
        string? suffix)
    {
        if (prefix == null && suffix == null)
        {
            throw new ErrorException("Either prefix or suffix must be defined.");
        }

        return FindAssemblyInfos(assemblyNamesToAlias, allFiles, name => $"{prefix}{name}{suffix}");
    }

    static IEnumerable<SourceTargetInfo> FindAssemblyInfos(
        IEnumerable<string> assemblyNamesToAlias,
        IEnumerable<string> allFiles,
        Func<string, string> getTargetName)
    {
        assemblyNamesToAlias = assemblyNamesToAlias.ToList();

        foreach (var file in allFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var fileDirectory = Path.GetDirectoryName(file)!;
            var isAliased = false;
            foreach (var assemblyToAlias in assemblyNamesToAlias)
            {
                var targetName = getTargetName(name);
                var targetPath = Path.Combine(fileDirectory, $"{targetName}.dll");

                if (assemblyToAlias.EndsWith("*"))
                {
                    var match = assemblyToAlias.TrimEnd('*');
                    if (name.StartsWith(match))
                    {
                        yield return new(name, file, targetName, targetPath, true);
                        isAliased = true;
                    }

                    continue;
                }

                if (name == assemblyToAlias)
                {
                    yield return new(name, file, targetName, targetPath, true);
                    isAliased = true;
                }
            }

            if (!isAliased)
            {
                yield return new(name, file, name, file, false);
            }
        }
    }
}