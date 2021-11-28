using System.Text.Json;

public static class RefClass
{
    public static string Method()
    {
        return JsonSerializer.Serialize("value");
    }
}