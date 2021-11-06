# <img src='/src/icon.png' height='30px'> Alias

[![Build status](https://ci.appveyor.com/api/projects/status/9es21v2yrcugyyxk/branch/master?svg=true)](https://ci.appveyor.com/project/SimonCropp/Alias)
[![NuGet Status](https://img.shields.io/nuget/v/Alias.svg)](https://www.nuget.org/packages/Alias/)

Rename assemblies and fixes references.

**[.net 5](https://dotnet.microsoft.com/download/dotnet/5.0) or [.net 6](https://dotnet.microsoft.com/download/dotnet/6.0) is required to run this tool.**


## What it does

For a given directory and a subset of assembly names to "alias"

 * Changes the assembly name of each "alias" assembly.
 * Renames "alias" assemblies on disk.
 * For all assemblies, fixes the references to point to the new alias assemblies.

Both the assembly name and file name changes are currently hardcoded to add the suffix `_Alias`. This will be configurable in a future version.


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


### Arguments


#### Target Directory

`-t` or `--target-directory`

Optional. if no directory is passed the current directory will be used.


#### Assemblies to alias

`-a` or `--assemblies-to-alias`

A semi-colon seperated list of assembly names. Names ending in `*` are treased as wildcards.


#### Key

`-k` or `--key`

Path to an snk file. 

Optional. If no key is passed, string naming will be removed from all assemblies.


## Icon

[Woman](https://thenounproject.com/term/woman/3424720/) designed by [auttapol](https://thenounproject.com/monsterku69) from [The Noun Project](https://thenounproject.com).
