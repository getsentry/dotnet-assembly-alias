[UsesVerify]
public class Tests
{
    public List<AssemblyResult> Run(bool copyPdbs, bool sign)
    {
        var binDirectory = Path.GetDirectoryName(typeof(Tests).Assembly.Location)!;

        var assemblyFiles = new List<string>
        {
            "AssemblyToProcess",
            "AssemblyWithEmbeddedSymbols",
            "AssemblyWithNoStrongName",
            "AssemblyWithNoSymbols",
            "AssemblyWithPdb",
            "AssemblyWithResources"
        };
        var tempPath = Path.Combine(binDirectory, "Temp");
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, true);
        }

        Directory.CreateDirectory(tempPath);
        foreach (var assembly in assemblyFiles)
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

        Program.Inner(tempPath, assemblyFiles.Where(x => x.StartsWith("AssemblyWith")).ToList(), new List<string>(), keyFile, new List<string>(),null,"_Alias");

        var resultingFiles = Directory.EnumerateFiles(tempPath);
        var results = new List<AssemblyResult>();
        foreach (var assembly in resultingFiles.Where(x => x.EndsWith(".dll")))
        {
            using var definition = AssemblyDefinition.ReadAssembly(assembly);
            results.Add(
                new(
                    definition.Name.FullName,
                    definition.MainModule.TryReadSymbols(),
                    definition.MainModule.AssemblyReferences.Select(x => x.FullName).ToList()));
        }

        return results;
    }

    [Theory]
    [MemberData(nameof(GetData))]
    public async Task Combo(bool copyPdbs, bool sign)
    {
        var results = Run(copyPdbs, sign);

        await Verifier.Verify(results)
            .UseParameters(copyPdbs, sign);
    }

    static bool[] bools = {true, false};

    public static IEnumerable<object[]> GetData()
    {
        foreach (var copyPdbs in bools)
        foreach (var sign in bools)
        {
            yield return new object[] {copyPdbs, sign};
        }
    }
}

public record AssemblyResult(string Name, bool HasSymbols, List<string> References);