using System.Diagnostics;
using Mono.Cecil;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Alias;

public class AliasTask :
    Task,
    ICancelableTask
{
    [Required] 
    public string IntermediateAssembly { get; set; } = null!;
    [Required] 
    public string ProjectDirectory { get; set; } = null!;
    [Required]
    public string IntermediateDirectory { get; set; } = null!;
    public string? AssemblyOriginatorKeyFile { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    [Required]
    public ITaskItem[] AssembliesToAlias { get; set; } = null!;

    public bool SignAssembly { get; set; }
    public bool Internalize { get; set; }
    [Required]
    public string References { get; set; } = null!;

    public override bool Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            InnerExecute();
            return true;
        }
        catch (ErrorException exception)
        {
            Log.LogError($"AssemblyAlias: {exception}");
            return false;
        }
        finally
        {
            Log.LogMessageFromText($"Finished AssemblyAlias {stopwatch.ElapsedMilliseconds}ms", MessageImportance.Normal);
        }
    }

    void InnerExecute()
    {
        Log.LogWarning("AAAAAAAAA");

        var assembliesToAlias = AssembliesToAlias.Select(x => x.ItemSpec).ToList();
        Log.LogWarning($"AssembliesToAlias:{Environment.NewLine}{string.Join(Environment.NewLine, assembliesToAlias)}");
        //var splitReferences = References.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries)
        //    .ToList();
        //var allFiles = new List<string>(splitReferences) {IntermediateAssembly};
        //var assembliesToAlias = AssembliesToAlias
        //    .Select(x => x.ItemSpec)
        //    .ToList();
        //var assemblyInfos = Finder.FindAssemblyInfos(assembliesToAlias, allFiles, Prefix, Suffix);

        //  Aliaser.Run(splitReferences, assemblyInfos, Internalize, GetKey());
    }

    StrongNameKeyPair? GetKey()
    {
        if (!SignAssembly)
        {
            return null;
        }

        if (AssemblyOriginatorKeyFile == null)
        {
            throw new ErrorException("AssemblyOriginatorKeyFile not defined");
        }

        if (!File.Exists(AssemblyOriginatorKeyFile))
        {
            throw new ErrorException($"AssemblyOriginatorKeyFile does no exist:{AssemblyOriginatorKeyFile}");
        }

        var bytes = File.ReadAllBytes(AssemblyOriginatorKeyFile);
        return new(bytes);
    }

    public void Cancel()
    {
    }
}