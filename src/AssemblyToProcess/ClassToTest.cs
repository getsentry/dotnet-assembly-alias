
using Newtonsoft.Json;

public static class ClassToTest
{
    public static IEnumerable<string> Method()
    {
        yield return AssemblyWithEmbeddedSymbolsClass.Method();
        yield return AssemblyWithStrongNameClass.Method();
        yield return AssemblyWithNoStrongNameClass.Method();
        yield return AssemblyWithNoSymbolsClass.Method();
        yield return AssemblyWithPdbClass.Method();
        yield return AssemblyToIncludeClass.Method();
        yield return typeof(JsonSerializer).FullName!;
        //yield return AssemblyWithResourcesClass.Method();
    }
}