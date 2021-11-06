using Xunit;

public class ModuleReaderTests 
{
    [Fact]
    public void WithSymbols()
    {
        var assemblyPath = Path.Combine(Environment.CurrentDirectory, "DummyAssembly.dll");
        using var resolver = new AssemblyResolver(Enumerable.Empty<string>());
        var result = ModuleReaderWriter.Read(assemblyPath, resolver);
        Assert.NotNull(result.module);
        Assert.True(result.hasSymbols);
    }

    [Fact]
    public void NoSymbols()
    {
        var assemblyPath = Path.Combine(Environment.CurrentDirectory, "AssemblyWithNoSymbols.dll");
        using var resolver = new AssemblyResolver(Enumerable.Empty<string>());
        var result = ModuleReaderWriter.Read(assemblyPath, resolver);

        Assert.NotNull(result.module);
        Assert.False(result.hasSymbols);
    }
}