using System.Diagnostics;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Alias;

public class AliasTask :
    Task,
    ICancelableTask
{
    [Required] 
    public string ProjectDirectory { get; set; } = null!;
    [Required]
    public string IntermediateDirectory { get; set; } = null!;
    public string? KeyOriginatorFile { get; set; }
    public string? AssemblyOriginatorKeyFile { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    [Required]
    public ITaskItem[] AssembliesToAlias { get; set; } = null!;

    public bool SignAssembly { get; set; }
    public bool DelaySign { get; set; }
    [Required]
    public string References { get; set; } = null!;

    public override bool Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
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

    public void Cancel()
    {
    }
}