# <img src='/src/icon.png' height='30px'> Alias

[![Build status](https://ci.appveyor.com/api/projects/status/9es21v2yrcugyyxk/branch/master?svg=true)](https://ci.appveyor.com/project/SimonCropp/Alias)
[![NuGet Status](https://img.shields.io/nuget/v/Alias.svg)](https://www.nuget.org/packages/Alias/)

Rename assemblies and fixes references.

**[.net 5](https://dotnet.microsoft.com/download/dotnet/5.0) or [.net 6](https://dotnet.microsoft.com/download/dotnet/6.0) is required to run this tool.**


## What it does

For a given directory and a subset of assemblies:

 * Changes the assembly name of each "alias" assembly.
 * Renames "alias" assemblies on disk.
 * For all assemblies, fixes the references to point to the new alias assemblies.


## Installation

Ensure [dotnet CLI is installed](https://docs.microsoft.com/en-us/dotnet/core/tools/).

Install [Alias](https://nuget.org/packages/Alias/)

```ps
dotnet tool install --global Alias --version 0.1.0-beta.5
```


## Usage

```ps
assemblyalias --target-directory "C:/Code/TargetDirectory" --assemblies-to-alias "Microsoft*;System*;EmptyFiles"
```


## Arguments


### Target Directory

`-t` or `--target-directory`

Optional. if no directory is passed the current directory will be used.


### Prefix/Suffix

Either a prefix or suffix must be defined.


#### Prefix

`-p` or `--prefix`

The prefix to use when renaming assemblies.


#### Suffix

`-s` or `--suffix`

The suffix to use when renaming assemblies.


### Assemblies to alias

`-a` or `--assemblies-to-alias`

Required. A semi-colon seperated list of assembly names to alias. Names ending in `*` are treased as wildcards.


### Assemblies to exclude

`-e` or `--assemblies-to-exclude`

Optional. A semi-colon seperated list of assembly names to exclude.


### Key

`-k` or `--key`

Path to an snk file. 

Optional. If no key is passed, string naming will be removed from all assemblies.


### References

`-r` or `--references`

Optional. A semi-colon seperated list of paths to reference files.


### Reference File

`--reference-file`

Optional. A path to a file cotaining references file paths. On file path per line.