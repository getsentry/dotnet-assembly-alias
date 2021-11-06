using System.Collections.Generic;

public static class ClassToTest
{
    public static IEnumerable<string> Method()
    {
        yield return AssemblyWithEmbeddedSymbolsClass.Method();
        yield return AssemblyWithNoStrongNameClass.Method();
        yield return AssemblyWithNoSymbolsClass.Method();
        yield return AssemblyWithPdbClass.Method();
        yield return AssemblyWithResourcesClass.Method();
    }
}
