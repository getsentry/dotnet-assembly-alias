using System.Text.Json;

public static class AssemblyToTargetClass
{
    public static string Method()
    {
        return JsonSerializer.Serialize(@"a\b");
    }
}