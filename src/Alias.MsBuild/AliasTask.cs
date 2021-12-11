using System.Diagnostics;
using Mono.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace Alias;

public class AliasTask :
    Task,
    ICancelableTask
{
    [Required]
    public ITaskItem[] ReferenceCopyLocalPaths { get; set; } = null!;

    [Required]
    public string IntermediateAssembly { get; set; } = null!;

    [Required]
    public string IntermediateDirectory { get; set; } = null!;

    public string? AssemblyOriginatorKeyFile { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    
    public ITaskItem[]? AssembliesToSkipRename { get; set; }
    
    public bool SignAssembly { get; set; }
    public bool Internalize { get; set; }

    [Required]
    public ITaskItem[] ReferencePath { get; set; } = null!;

    [Output]
    public ITaskItem[] CopyLocalPathsToRemove { get; set; } = null!;

    [Output]
    public ITaskItem[] CopyLocalPathsToAdd { get; set; } = null!;

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
        List<string> assembliesToSkipRename;
        if (AssembliesToSkipRename == null)
        {
            assembliesToSkipRename = new List<string>();
        }
        else
        {
            assembliesToSkipRename = AssembliesToSkipRename.Select(x => x.ItemSpec).ToList();
        }

        var assemblyCopyLocalPaths = ReferenceCopyLocalPaths
            .Select(x => x.ItemSpec)
            .Where(x=>Path.GetExtension(x).ToLowerInvariant() ==".dll")
            .ToList();
        var references = ReferencePath.Select(x => x.ItemSpec)
            .Where(x => !assemblyCopyLocalPaths.Contains(x))
            .ToList();

        var assembliesToAlias= assemblyCopyLocalPaths
            .Where(x => !assembliesToSkipRename.Contains(Path.GetFileNameWithoutExtension(x)))
            .ToList();
        
        var assembliesToTarget = assemblyCopyLocalPaths
            .Where(x => assembliesToSkipRename.Contains(Path.GetFileNameWithoutExtension(x)))
            .ToList();

        assembliesToTarget.Insert(0, IntermediateAssembly);

        var sourceTargetInfos = new List<SourceTargetInfo>();
        var copyLocalPathsToRemove = new List<ITaskItem>();
        var copyLocalPathsToAdd = new List<ITaskItem>();

        void ProcessCopyLocal(string sourcePath, string targetPath)
        {
            var copyLocalToRemove = ReferenceCopyLocalPaths.SingleOrDefault(x => x.ItemSpec == sourcePath);
            if (copyLocalToRemove != null)
            {
                copyLocalPathsToRemove.Add(copyLocalToRemove);
            }

            var pdbToRemove = Path.ChangeExtension(sourcePath, "pdb");
            copyLocalToRemove = ReferenceCopyLocalPaths.SingleOrDefault(x => x.ItemSpec == pdbToRemove);
            if (copyLocalToRemove != null)
            {
                copyLocalPathsToRemove.Add(copyLocalToRemove);
            }

            copyLocalPathsToAdd.Add(new TaskItem(targetPath));

            var pdbToAdd = Path.ChangeExtension(targetPath, "pdb");
            if (File.Exists(pdbToAdd))
            {
                copyLocalPathsToAdd.Add(new TaskItem(pdbToAdd));
            }
        }

        foreach (var sourcePath in assembliesToAlias)
        {
            var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            var targetName = $"{Prefix}{sourceName}{Suffix}";
            var targetPath = Path.Combine(IntermediateDirectory, $"{targetName}.dll");
            sourceTargetInfos.Add(new(sourceName, sourcePath, targetName, targetPath, true));
            ProcessCopyLocal(sourcePath, targetPath);
        }

        foreach (var sourcePath in assembliesToTarget)
        {
            var name = Path.GetFileNameWithoutExtension(sourcePath);
            var targetPath = Path.Combine(IntermediateDirectory, $"{name}.dll");
            sourceTargetInfos.Add(new(name, sourcePath, name, targetPath, false));
            ProcessCopyLocal(sourcePath, targetPath);
        }

        var separator = $"{Environment.NewLine}\t";
        var inputs = $@"
Prefix: {Prefix}
Suffix: {Suffix}
Internalize: {Internalize}
AssembliesToAlias:{separator}{string.Join(separator, assembliesToAlias.Select(Path.GetFileNameWithoutExtension))}
AssembliesToTarget:{separator}{string.Join(separator, assembliesToTarget.Select(Path.GetFileNameWithoutExtension))}
TargetInfos:{separator}{string.Join(separator, sourceTargetInfos.Select(x => $"{x.SourceName} => {x.TargetName}"))}
ReferenceCopyLocalPaths:{separator}{string.Join(separator, ReferenceCopyLocalPaths.Select(x => $"{x.ItemSpec}"))}
";
        Log.LogMessageFromText(inputs, MessageImportance.High);

        Aliaser.Run(references, sourceTargetInfos, Internalize, GetKey());
        CopyLocalPathsToRemove = copyLocalPathsToRemove.ToArray();
        CopyLocalPathsToAdd = copyLocalPathsToAdd.ToArray();
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