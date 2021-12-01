using System.Diagnostics;
using System.Text;

[UsesVerify]
public class Tests
{
    static string binDirectory = Path.GetDirectoryName(typeof(Tests).Assembly.Location)!;

    static List<string> assemblyFiles = new()
    {
        "AssemblyToProcess",
        "AssemblyWithEmbeddedSymbols",
        "AssemblyWithNoStrongName",
        "AssemblyWithStrongName",
        "AssemblyWithNoSymbols",
        "AssemblyWithPdb",
        "AssemblyWithResources"
    };

    static Tests()
    {
        tempPath = Path.Combine(binDirectory, "Temp");
        Directory.CreateDirectory(tempPath);
    }

    public Tests()
    {
        Helpers.PurgeDirectory(tempPath);
    }

    IEnumerable<AssemblyResult> Run(bool copyPdbs, bool sign, bool internalize)
    {
        foreach (var assembly in assemblyFiles.OrderBy(x => x))
        {
            var assemblyFile = $"{assembly}.dll";
            File.Copy(Path.Combine(binDirectory, assemblyFile), Path.Combine(tempPath, assemblyFile));
            if (copyPdbs)
            {
                var pdbFile = $"{assembly}.pdb";
                var sourceFileName = Path.Combine(binDirectory, pdbFile);
                if (File.Exists(sourceFileName))
                {
                    File.Copy(sourceFileName, Path.Combine(tempPath, pdbFile));
                }
            }
        }

        string? keyFile = null;
        if (sign)
        {
            keyFile = Path.Combine(AttributeReader.GetProjectDirectory(), "test.snk");
        }

        var namesToAliases = assemblyFiles.Where(x => x.StartsWith("AssemblyWith")).ToList();
        Program.Inner(tempPath, namesToAliases, new(), keyFile, new(), null, "_Alias", internalize);

        return BuildResults();
    }

    static IEnumerable<AssemblyResult> BuildResults()
    {
        var resultingFiles = Directory.EnumerateFiles(tempPath);
        foreach (var assembly in resultingFiles.Where(x => x.EndsWith(".dll")).OrderBy(x => x))
        {
            using var definition = AssemblyDefinition.ReadAssembly(assembly);
            var attributes = definition.CustomAttributes
                .Where(x => x.AttributeType.Name.Contains("Internals"))
                .Select(x => $"{x.AttributeType.Name}({string.Join(',', x.ConstructorArguments.Select(y => y.Value))})")
                .OrderBy(x => x)
                .ToList();
            yield return 
                new AssemblyResult(
                    definition.Name.FullName,
                    definition.MainModule.TryReadSymbols(),
                    definition.MainModule.AssemblyReferences.Select(x => x.FullName).OrderBy(x => x).ToList(),
                    attributes);
        }
    }

    [Theory]
    [MemberData(nameof(GetData))]
    public Task Combo(bool copyPdbs, bool sign, bool internalize)
    {
        var results = Run(copyPdbs, sign, internalize);

        return Verifier.Verify(results)
            .UseParameters(copyPdbs, sign, internalize);
    }

    //[Fact]
    //public Task PatternMatching()
    //{
    //    foreach (var assembly in assemblyFiles.OrderBy(x => x))
    //    {
    //        var assemblyFile = $"{assembly}.dll";
    //        File.Copy(Path.Combine(binDirectory, assemblyFile), Path.Combine(tempPath, assemblyFile));
    //    }

    //    var namesToAliases = assemblyFiles.Where(x => x.StartsWith("AssemblyWith")).ToList();
    //    Program.Inner(tempPath, namesToAliases, new(), null, new(), null, "_Alias", false);
    //    var results = BuildResults();

    //    return Verifier.Verify(results);
    //}

#if DEBUG

    [Fact]
    public async Task RunSample()
    {
        var solutionDirectory = AttributeReader.GetSolutionDirectory();

        var targetPath = Path.Combine(solutionDirectory, "SampleApp/bin/Debug/net6.0");

        var tempPath = Path.Combine(targetPath, "temp");
        Directory.CreateDirectory(tempPath);
        Helpers.PurgeDirectory(tempPath);

        Helpers.CopyFilesRecursively(targetPath, tempPath);

        Program.Inner(tempPath, new() {"Assembly*"}, new(), null, new(), "Alias_", null, true);

        PatchDependencies(tempPath);

        var exePath = Path.Combine(tempPath, "SampleApp.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo)!;
        var output = new StringBuilder();
        var error = new StringBuilder();
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += (_, args) => output.AppendLine(args.Data);
        process.ErrorDataReceived += (_, args) => error.AppendLine(args.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        await Verifier.Verify(new {output, error});
        Assert.Equal(0, process.ExitCode);
    }

#endif

    static void PatchDependencies(string targetPath)
    {
        var depsFile = Path.Combine(targetPath, "SampleApp.deps.json");
        var text = File.ReadAllText(depsFile);
        text = text.Replace("Assembly", "Alias_Assembly");
        File.Delete(depsFile);
        File.WriteAllText(depsFile, text);
    }

    static bool[] bools = {true, false};
    static readonly string tempPath;

    public static IEnumerable<object[]> GetData()
    {
        foreach (var copyPdbs in bools)
        foreach (var sign in bools)
        foreach (var internalize in bools)
        {
            yield return new object[] {copyPdbs, sign, internalize};
        }
    }
}

public record AssemblyResult(string Name, bool HasSymbols, List<string> References, List<string> Attributes);