Many .NET applications and frameworks support a [plugin based model](https://en.wikipedia.org/wiki/Plug-in_(computing)). Also known as "add-in" or "extension" model. A plugin model allows extension or customization of functionality by adding assemblies and config files to a directory that is scanned at application startup. For example:

 * [MsBuild tasks](https://docs.microsoft.com/en-us/visualstudio/msbuild/task-writing)
 * [Visual Studio extensions](https://docs.microsoft.com/en-us/visualstudio/extensibility/starting-to-develop-visual-studio-extensions)
 * [ReSharper](https://www.jetbrains.com/resharper/)/[Rider](https://www.jetbrains.com/rider/) plugins
 * [Unity Plugins](https://docs.unity3d.com/Manual/Plugins.html)


## The problem

Most plugin based models load all assemblies into a single shared context. This is the common approach as it has better memory usage and startup performance. The history and rules of assembly loading in .NET is convoluted, with the current status being that it is difficult (and sometimes impossible) to load multiple different versions of the same assembly into a shared context. For example, it is not possible to load both versions 12.0.2 and 12.0.3 of `Newtonsoft.Json.dll` into the same context. In a plugin environment, the resulting behavior is often based on the load order of plugins. At runtime the reference used in the first loaded plugin is then used by every subsequent plugin. So if a plugin relies on a later version of a reference than the on initially loaded, that plugin will fail at either load time or runtime. At compile/build time a similar conflict can occur if the build tooling had conflict detection in place.

More specifically in the Unity world, [UPM (Unity Package Manager)](https://docs.unity3d.com/Manual/upm-ui.html) packages can include one or more DLLs that can cause such conflicts when used together. With Unity adding support for .NET Standard 2.0, different package developers (including Unity themselves), started bundling some `System` DLLs such as `System.Runtime.CompilerServices.dll`, `System.Memory.dll`, and `System.Buffers.dll`. Since the release of .NET 5.0, many of these DLLs have become part of the standard library, this means there's no need to bring them in via NuGet or bundled in a UPM package. The Sentry SDK for .NET, is dependency-free when targeting .NET 5 or higher. In the case of Unity, there is [work towards supporting .NET 6](https://forum.unity.com/threads/unity-future-net-development-status.1092205/). However we required a solution to unblock a growing number of users hitting issues with more than one package bundling these DLLs.


## Options we considered and ruled out


### [Costura](https://github.com/Fody/Costura)

Costura merges dependencies into a target assembly as resources. Custom assembly loading logic is then added to the target assembly so that dependencies are loaded from resources instead of from disk. The important point here is that the dependency assemblies are not changed as part of the being added as resources. So those assemblies each still have the same assembly name and, when loaded will respect the standard assembly loading logic. So in a plugin environment, using Costura will still result in a conflict.


### [ILMerge](https://github.com/dotnet/ILMerge) / [ILRepack](https://github.com/gluck/il-repack)

ILMerge and ILRepack work by copying the IL from dependencies into the target assembly. So the resulting assembly has duplicates of all the types from all the dependencies and no longer references those dependencies. This approach does resolve the conflict, however, both these projects are not currently being actively maintained. For example, both have known bugs related to .NET Core and portable PDBs.


## The solution

With the other existing options exhausted, it was decided to build a new tool.

[Alias](https://github.com/getsentry/dotnet-assembly-alias/)

Alias performs the following steps:

 * Given a directory containing the target assembly and its dependencies.
 * Rename all the dependencies with a unique key. The rename applies to both the file name and the assembly name in IL.
 * Patch the corresponding references in the target assembly and dependencies.

This results in a group of files that will not conflict with any assemblies loaded in the plugin context.

One point of interest is that the result is not a single file, the approach used by ILRepack, ILMerge, and Costura. The reason for this is that for the reviewed plugin scenarios, all supported a plugin being deployed to its own directory as a group of files. So "single file" was not a problem that needed to be solved.

This allowed the Sentry UPM package to include "its own version" of the shim DLLs needed in order to work in a .NET Standard 2.0 target. IL2CPP's linker still takes care of dropping any unused code in the final application. Given Sentry's commitment to support Unity's LTS version from 2019.4 onwards, we expect to rely on this solution for a few years. Until the lowest supported Unity version allows us to include only `Sentry.dll` without any transient dependencies.


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

You can use Alias to resolve conflicts in your UPM packages too. Like the [Sentry SDK for Unity](https://github.com/getsentry/sentry-unity), our tools are open source.


## Better MsBuild integration

Currently Alias is only a dotnet tool (command line). This maps well to use it as part of the bundling/packaging step of the development life-cycle. However, it can make it difficult to debug if something goes wrong, as unit tests and running a project from the IDE don't automatically use the aliased assembly. Ideally Alias could also be shipped as an MsBuild task. So when it is applied to a project, that projects resulting assembly is "aliased" and all dependent projects would consume that assembly. This proved too difficult to achieve in the short term. The complexity resolves around the fact that, not only the outputted assembly, but also the transient dependencies (that are aliased) of the target project need to be modified. Another attempt to implement MsBuild integration will be made in the future. If you have skills in MsBuild, we would appreciate any help in implementing this feature.