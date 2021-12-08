namespace Alias;

public record SourceTargetInfo(
    string SourceName, 
    string SourcePath, 
    string TargetName, 
    string TargetPath, 
    bool isAlias);