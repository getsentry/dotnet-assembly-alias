public delegate void Invoke(
    string targetDirectory,
    List<string> assembliesToAlias,
    List<string> references, 
    string? keyFile,
    List<string> assembliesToExclude);
