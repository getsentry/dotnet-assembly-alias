public delegate void Invoke(
    string directory,
    List<string> assembliesToAlias,
    List<string> references,
    string? keyFile,
    List<string> assembliesToExclude,
    string? prefix,
    string? suffix,
    bool internalize);
