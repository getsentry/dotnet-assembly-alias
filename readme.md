# <img src='/src/icon.png' height='30px'> Alias

[![NuGet Status](https://img.shields.io/nuget/v/Sentry.AssemblyAlias.svg?label=Sentry.AssemblyAlias%20nuget)](https://www.nuget.org/packages/Sentry.AssemblyAlias/)
[![NuGet Status](https://img.shields.io/nuget/v/Sentry.AssemblyAlias.Lib.svg?label=Sentry.AssemblyAlias.Lib%20nuget)](https://www.nuget.org/packages/Sentry.AssemblyAlias.Lib/)
[![NuGet Status](https://img.shields.io/nuget/v/Sentry.AssemblyAlias.MsBuild.svg?label=Sentry.AssemblyAlias.MsBuild%20nuget)](https://www.nuget.org/packages/Sentry.AssemblyAlias.MsBuild/)

Renames assemblies and fixes references. Designed as an alternative to [Costura](https://github.com/Fody/Costura), [ILMerge](https://github.com/dotnet/ILMerge), and [ILRepack](https://github.com/gluck/il-repack).

**See [Milestones](../../milestones?state=closed) for release notes.**

Designed to mitigate scenarios where an assembly runs in a plugin context, for example Unity extensions, MSBuild tasks, or SharePoint extensions. In these scenarios an assembly, and all its references, are loaded into a shared AppDomain, so dependencies operate as "first on wins". For example, if two add-in assemblies use different versions of Newtonsoft, the first add-in that is loaded defines which version of Newtonsoft is used by all subsequent add-ins.

This project works around this problem by renaming references and preventing name conflicts.


## dotnet tool

https://www.nuget.org/packages/Sentry.AssemblyAlias/

**[.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) or higher is required to run this tool.**

For a given directory and a subset of assemblies, it:

 * Changes the assembly name of each "alias" assembly.
 * Renames the "alias" assemblies on disk.
 * Fixes the references in all assemblies to point to the new alias assemblies.


### Installation

Ensure [dotnet CLI is installed](https://docs.microsoft.com/en-us/dotnet/core/tools/).

Install [Sentry.AssemblyAlias](https://nuget.org/packages/Sentry.AssemblyAlias/)

```ps
dotnet tool install --global Sentry.AssemblyAlias
```


### Usage

```ps
assemblyalias --target-directory "C:/Code/TargetDirectory"
              --suffix _Alias
              --assemblies-to-alias "Microsoft*;System*;EmptyFiles"
```


### Arguments


#### Target Directory

`-t` or `--target-directory`

Optional. If no directory is passed the current directory will be used.


#### Internalize

`-i` or `--internalize`

Optional. Internalizes all types in the aliased assemblies. Defaults to false.


#### Prefix/Suffix

Either a prefix or suffix must be defined.


##### Prefix

`-p` or `--prefix`

The prefix to use when renaming assemblies.


##### Suffix

`-s` or `--suffix`

The suffix to use when renaming assemblies.


#### Assemblies to alias

`-a` or `--assemblies-to-alias`

Required. A semi-colon separated list of assembly names to alias. Names ending in `*` are treated as wildcards.


#### Assemblies to exclude

`-e` or `--assemblies-to-exclude`

Optional. A semi-colon separated list of assembly names to exclude.


#### Key

`-k` or `--key`

Path to an snk file.

Optional. If no key is passed, strong naming will be removed from all assemblies.


#### References

`-r` or `--references`

Optional. A semi-colon separated list of paths to reference files.


#### Reference File

`--reference-file`

Optional. A path to a file containing references file paths. One file path per line.


##### Default Reference File

By default, the target directory is scanned for a reference file named `alias-references.txt`.

It can be helpful to extract references during a build using MSBuild and write them to a file accessible to Alias:

<!-- snippet: WriteReferenceForAlias -->
<a id='snippet-WriteReferenceForAlias'></a>
```csproj
<Target Name="WriteReferenceForAlias" AfterTargets="AfterCompile">
  <ItemGroup>
    <ReferenceForAlias Include="@(ReferencePath)" Condition="'%(FileName)' == 'CommandLine'" />
  </ItemGroup>
  <WriteLinesToFile File="$(TargetDir)/alias-references.txt" Lines="%(ReferenceForAlias.FullPath)" Overwrite="true" />
</Target>
```
<sup><a href='/src/SampleApp/SampleApp.csproj#L19-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-WriteReferenceForAlias' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
