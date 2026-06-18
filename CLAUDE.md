# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Alias renames .NET assemblies on disk and rewrites all references to point at the new names, to avoid "first-loaded wins" dependency conflicts when assemblies are loaded into a shared AppDomain (Unity extensions, MSBuild tasks, SharePoint plugins). It is shipped two ways: a `dotnet` global tool (`assemblyalias`) and an MSBuild task that runs automatically during a build.

This is a fork (`getsentry/dotnet-assembly-alias`) of Simon Cropp's original. Release notes live in GitHub Milestones, not a changelog.

## Commands

All commands run against the `src` directory (it holds the solution, not the repo root).

```bash
dotnet build src --configuration Release      # also packs nupkgs to /nugets in Release
dotnet test src                               # run all tests
dotnet test src --filter "FullyQualifiedName~CommandRunnerTests"   # one test class
dotnet test src --filter "Name~Combo"         # tests matching a name
```

A specific .NET SDK is pinned in [src/global.json](src/global.json) (`rollForward: latestFeature`, prerelease allowed). `TreatWarningsAsErrors` is on for the whole solution — warnings fail the build.

## Architecture

The renaming logic is shared; the two entry points only differ in how they discover the file list.

- **[Alias.Lib](src/Alias.Lib/)** — the engine, shared by both entry points. Multi-targets `net6;net48;netstandard2.0`.
  - [Aliaser.cs](src/Alias.Lib/Aliaser.cs) is the core. Given a list of `SourceTargetInfo` (source name/path → target name/path, plus `IsAlias`), it uses Mono.Cecil to: read each module, rename the assembly, optionally internalize types + add `InternalsVisibleTo` for the other aliased assemblies, redirect every matching `AssemblyReference` to the new name, then write all modules. **Reads happen first, writes are deferred to a second pass** — modules must resolve each other before any is written.
  - [AssemblyResolver.cs](src/Alias.Lib/AssemblyResolver.cs) is a strict Cecil `IAssemblyResolver`: it resolves only from an explicit cache (the references list + a bundled `netstandard.dll` embedded resource, also aliased as `mscorlib`). Unresolved references throw rather than probing disk — this is why callers must supply a complete references list.
  - [CecilExtensions.cs](src/Alias.Lib/CecilExtensions.cs) — `MakeTypesInternal`, `SeyKey` (sic; sets/strips strong-name key), `PublicKeyString`.

- **[Alias](src/Alias/)** — the `assemblyalias` CLI tool (`net6.0`, packed `PackAsTool`).
  - [CommandRunner.cs](src/Alias/CommandRunner.cs) parses args (CommandLineParser), validates, assembles the references list (including an `alias-references.txt` auto-discovered in the target dir), then calls `Program.Inner`.
  - [Finder.cs](src/Alias/Finder.cs) globs `*.dll` in the target directory and decides which match the alias name patterns (`*` suffix = prefix wildcard). It yields a `SourceTargetInfo` for **every** dll — aliased ones get the renamed target, others map to themselves so their references still get redirected.
  - [Program.cs](src/Alias/Program.cs) `Inner` runs the aliaser then deletes the original source dll/pdb files.

- **[Alias.MsBuild](src/Alias.MsBuild/)** — the MSBuild task (`netstandard2.0`).
  - [AliasTask.cs](src/Alias.MsBuild/AliasTask.cs) runs `AfterCompile`. Instead of globbing a directory, it derives the file list from MSBuild item groups (`ReferenceCopyLocalPaths`, `ReferencePath`, `IntermediateAssembly`), aliases everything except `Alias_AssembliesToSkipRename`, writes outputs to the intermediate dir, and returns `CopyLocalPathsToAdd`/`CopyLocalPathsToRemove` so [build/Alias.MsBuild.targets](src/Alias.MsBuild/build/Alias.MsBuild.targets) can swap them in the copy-local set. The consuming project's own intermediate assembly is always treated as a non-alias target.

`SourceTargetInfo` ([src/Alias.Lib/SourceTargetInfo.cs](src/Alias.Lib/SourceTargetInfo.cs)) is the contract between the entry points and the engine — both paths construct a `List<SourceTargetInfo>` and hand it to `Aliaser.Run`.

### Mono.Cecil

Cecil (and `Mono.Cecil.Pdb`, `Mono.Cecil.Rocks`) is referenced as **checked-in DLLs in [src/Lib/](src/Lib/)**, not via NuGet, and bundled into the tool/task packages. Don't replace these with package references without checking the packaging in the csprojs.

## Strong naming

Strong naming is woven through everything. If a key (`.snk`) is supplied, aliased assemblies are re-signed with it and `InternalsVisibleTo` values include the public key; if no key is supplied, strong-name signing is **stripped**. The build itself signs all projects with `src/key.snk`. Tests use a separate `src/Tests/test.snk`.

## Tests

Uses **Verify** (snapshot testing). Assertions compare against `*.verified.txt` files; a mismatch produces a `*.received.txt`. To accept intentional changes, replace the `.verified.txt` with the `.received.txt` content (or use a Verify diff tool).

- [Tests.cs](src/Tests/Tests.cs) drives the engine directly via `Program.Inner` over a set of fixture assemblies (the `AssemblyWith*` / `AssemblyTo*` projects), across the `Combo` matrix of `copyPdbs` × `sign` × `internalize`, then inspects the rewritten assemblies with Cecil.
- [CommandRunnerTests.cs](src/Tests/CommandRunnerTests.cs) verifies CLI arg parsing/validation output.

The many `AssemblyWith*` / `AssemblyTo*` / `SampleApp*` / `DummyAssembly` projects in the solution are **test fixtures**, each exercising a specific case (embedded symbols, pdb, resources, strong name, no strong name, no symbols). They are not part of the shipped product.
