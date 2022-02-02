Many .net applications and frameworks support a [plugin based model](https://en.wikipedia.org/wiki/Plug-in_(computing)). Also known as "add-in" or "extension" model. A plugin model allows extension or customization of functionality by adding assemblies and config files to a directory that is scanned at application startup. For example:

 * [MsBuild tasks](https://docs.microsoft.com/en-us/visualstudio/msbuild/task-writing)
 * [Visual Studio extensions](https://docs.microsoft.com/en-us/visualstudio/extensibility/starting-to-develop-visual-studio-extensions)
 * [ReSharper](https://www.jetbrains.com/resharper/)/[Rider](https://www.jetbrains.com/rider/) plugins
 * [Unity Plugins](https://docs.unity3d.com/Manual/Plugins.html)


## The problem

Most plugin based models load all assemblies into a single AppDomain. A single AppDomain is the common approach as it has better memory usage and startup performance. The history and rules of assembly loading in .NET is convoluted, with the current status being that it is difficult (and sometime impossible) to load multiple different versions of the same assembly into a single AppDomain. For example it is not possible to load both versions 12.0.2 and 12.0.3 of Newtonsoft.Json.dll into the same AppDomain. In a plugin environment, the resulting behavior is, based on the load order of plugins, the reference used in the first loaded plugin is then used by every subsequent plugin. So if a plugin relies on a later version of a reference than the on initially loaded, that plugin will fail at either load time or runtime. This problem is commonly referred to a [Diamond Dependency conflict](https://jlbp.dev/what-is-a-diamond-dependency-conflict).


## Other options considered and ruled out


### [Costura](https://github.com/Fody/Costura)

Costura merges dependencies into a target assembly as resources. Custom assembly loading logic is then added to the target assembly so that dependencies are loaded from resources instead of from disk. The important point here is that the dependency assemblies are not changed as part of the being added as resources. So those assemblies each still have the same assembly name and, when loaded will respect the standard assembly loading logic. So in a plugin environment, using Costura will still result in a Diamond Dependency conflict.


### [ILMerge](https://github.com/dotnet/ILMerge) / [ILRepack](https://github.com/gluck/il-repack)

ILMerge and ILRepack work by copying the IL from dependencies into the target assembly. So the resulting assembly has duplicates of all the types from all the dependencies and no longer references those dependencies. This approach does resolve the Diamond Dependency problem, however, both these projects are not currently being actively maintained. For example, both have known bugs related to .net core and portable symbols.


## The solution

With the other existing options exhausted, it was decided to build a new tool.

[Alias](https://github.com/getsentry/dotnet-assembly-alias/edit/main/readme.md)

Alias performs the following steps:

 * Given a directory containing the target assembly and dependencies files.
 * Rename all the dependencies with a unique key. The rename applies to both the file name and the assembly name in IL.
 * Patch the corresponding references in the target assembly and dependencies.

This results in a group of files that will not conflict with any assemblies loaded in the plugin AppDomain.

One point of interest is that the result is not a single file, the approach used by ILRepack, ILMerge, and Costura. The reason for this is that for the reviewed plugin scenarios, all supported a plugin being deployed to its own directory as a group of files. So "single file" was not a problem that needed to be solved.


## How to use

Alias is shipped as a [dotnet CLI tool](https://docs.microsoft.com/en-us/dotnet/core/tools/). So the [Alias tool](https://nuget.org/packages/Alias/) needs to be installed:

```ps
dotnet tool install --global Alias
```

Alias can then be used from the command line:

```ps
assemblyalias --target-directory "C:/Code/TargetDirectory"
              --suffix _Alias
              --assemblies-to-alias "Newtonsoft.Json.dll;Serilog*"
```

The `--suffix` should be a value that is unique enough to prevent conflicts. A good candidate is the name of the plugin or some derivative thereof.