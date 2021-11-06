using CommandLine;

public class Options
{
    [Option('t', "target-directory", Required = false)]
    public string? TargetDirectory { get; set; }

    [Option('a', "assemblies-to-alias", Required = true, Separator = ';')]
    public IEnumerable<string> AssembliesToAlias { get; set; } = null!;

    [Option('e', "assemblies-to-exclude", Required = false, Separator = ';')]
    public IEnumerable<string> AssembliesToExclude { get; set; } = null!;

    [Option('r', "references", Separator = ';')]
    public IEnumerable<string> References { get; set; } = null!;
    
    [Option("reference-file", Required = false)]
    public string? ReferenceFile { get; set; }
    
    [Option('k', "key", Required = false)]
    public string? Key { get; set; }
}