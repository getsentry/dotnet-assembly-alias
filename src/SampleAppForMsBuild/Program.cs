foreach (var line in ClassToTest.Method())
{
    Console.WriteLine(line);
}

Console.WriteLine(AssemblyToIncludeClass.Method());
Console.WriteLine(AssemblyWithStrongNameClass.Method());