using Xunit;
using Xunit.Abstractions;

public class Tests
{
    ITestOutputHelper testOutputHelper;

    public Tests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Foo()
    {
        testOutputHelper.WriteLine(AssemblyConsumingTest.Method());
    }
}