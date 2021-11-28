using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class Tests
{
    [Fact]
    public Task Run()
    {
        return Verifier.Verify(TargetClass.Method());
    }
}