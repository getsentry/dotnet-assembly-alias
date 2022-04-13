using CliWrap;
using CliWrap.Buffered;
using Mono.Cecil;

[UsesVerify]
public class Tests
{
    static string binDirectory = Path.GetDirectoryName(typeof(Tests).Assembly.Location)!;

    static List<string> assemblyFiles = new()
    {
        "AssemblyToProcess",
        "AssemblyToInclude",
        "AssemblyWithEmbeddedSymbols",
        "AssemblyWithNoStrongName",
        "AssemblyWithStrongName",
        "AssemblyWithNoSymbols",
        "AssemblyWithPdb",
        "AssemblyWithResources",
        "Newtonsoft.Json"
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

    static IEnumerable<AssemblyResult> Run(bool copyPdbs, bool sign, bool internalize)
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

        var namesToAliases = assemblyFiles.Where(x => x.StartsWith("AssemblyWith") || x=="Newtonsoft.Json").ToList();
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

        return Verify(results)
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

    [Fact]
    public async Task RunTask()
    {
        var solutionDir = AttributeReader.GetSolutionDirectory();

        var buildResult = await Cli.Wrap("dotnet")
            .WithArguments("build --configuration IncludeAliasTask --no-restore")
            .WithWorkingDirectory(solutionDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        var shutdown = Cli.Wrap("dotnet")
            .WithArguments("build-server shutdown")
            .ExecuteAsync();

        try
        {
            if (buildResult.StandardError.Length > 0)
            {
                throw new(buildResult.StandardError);
            }

            if (buildResult.StandardOutput.Contains("error"))
            {
                throw new(buildResult.StandardOutput.Replace(solutionDir, ""));
            }

            var appPath = Path.Combine(solutionDir, "SampleAppForMsBuild/bin/IncludeAliasTask/SampleAppForMsBuild.dll");
            var runResult = await Cli.Wrap("dotnet")
                .WithArguments(appPath)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            await Verify(
                    new
                    {
                        buildOutput = buildResult.StandardOutput,
                        consoleOutput = runResult.StandardOutput,
                        consoleError = runResult.StandardError
                    })
                .ScrubLinesContaining(
                    " -> ",
                    "You are using a preview version",
                    "Build Engine version",
                    "Time Elapsed")
                .ScrubLinesWithReplace(s => s.Replace('\\','/'));
        }
        finally
        {
            await shutdown;
        }
    }


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

        Program.Inner(
            tempPath,
            assemblyNamesToAlias: new() {"Assembly*"},
            references: new(),
            keyFile: null,
            assembliesToExclude: new() {"AssemblyToInclude", "AssemblyToProcess"},
            prefix: "Alias_",
            suffix: null,
            internalize: true);

        PatchDependencies(tempPath);

        var exePath = Path.Combine(tempPath, "SampleApp.exe");

        var result = await Cli.Wrap(exePath).ExecuteBufferedAsync();

        await Verify(new {result.StandardOutput, result.StandardError});
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