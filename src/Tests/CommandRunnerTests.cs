[UsesVerify]
public class CommandRunnerTests
{
    [Fact]
    public Task MissingAssembliesToAlias()
    {
        var result = Parse("--target-directory directory");
        return Verifier.Verify(result);
    }

    [Fact]
    public Task All()
    {
        var result = Parse("--target-directory directory --key test.snk --assemblies-to-alias assembly");
        return Verifier.Verify(result);
    }

    [Fact]
    public Task KeyRelative()
    {
        var result = Parse("--key test.snk --assemblies-to-alias assembly");
        return Verifier.Verify(result);
    }
    [Fact]
    public Task KeyFull()
    {
        var result = Parse($"--key {Environment.CurrentDirectory}{Path.DirectorySeparatorChar}test.snk --assemblies-to-alias assembly");
        return Verifier.Verify(result);
    }

    [Fact]
    public Task ReferenceFile()
    {
        var result = Parse("--assemblies-to-alias assembly --reference-file referenceFile");
        return Verifier.Verify(result);
    }

    [Fact]
    public Task References()
    {
        var result = Parse("--assemblies-to-alias assembly --references reference1");
        return Verifier.Verify(result);
    }

    [Fact]
    public Task ReferencesMultiple()
    {
        var result = Parse("--assemblies-to-alias assembly --references reference1;reference2");
        return Verifier.Verify(result);
    }

    [Fact]
    public Task CurrentDirectory()
    {
        var result = Parse("--assemblies-to-alias assembly");
        return Verifier.Verify(result);
    }

    [Fact]
    public Task MultipleAssemblies()
    {
        var result = Parse("--assemblies-to-alias assembly1;assembly2");
        return Verifier.Verify(result);
    }

    //[Fact]
    //public Task MultipleAssembliesSplit()
    //{
    //    var result = Parse("--assemblies-to-alias", "assembly2", "--assemblies-to-alias", "assembly2");
    //    return Verifier.Verify(result);
    //}

    static Result Parse(string input)
    {
        string? directory = null;
        string? key = null;
        IEnumerable<string>? assembliesToAlias = null;
        IEnumerable<string>? assembliesToExclude = null;
        IEnumerable<string>? references = null;
        var result = CommandRunner.RunCommand(
            (_targetDirectory, _assembliesToAlias, _references, _key, _assembliesToExclude) =>
            {
                directory = _targetDirectory;
                key = _key;
                assembliesToAlias = _assembliesToAlias;
                assembliesToExclude = _assembliesToExclude;
                references = _references;
            },
            input.Split(' '));
        return new(result, directory, key, assembliesToAlias, references, assembliesToExclude);
    }

    public record Result(IEnumerable<Error> errors, string? directory, string? key, IEnumerable<string>? assembliesToAlias, IEnumerable<string>? references, IEnumerable<string>? assembliesToExclude);
}