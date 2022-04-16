using AssemblyWithResources;

public static class AssemblyWithResourcesClass
{
    public static string Method()
    {
        return $"AssemblyWithResources: {strings.Resource}";
    }
}