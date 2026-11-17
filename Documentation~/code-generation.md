# Node IDs and generated enums

A node has one SkillTreeConfig owner. Adding a node assigns a positive int ID stored in the node asset. Renaming or reordering preserves the ID. Zero means no node; duplicate IDs are errors and deleted IDs are not automatically reused.

Enums provide names for these same numbers. The package includes a compiled Roslyn generator, so normal use needs no .NET SDK, generator build, or .csproj edits.

## Configuration

Open Enum Generation on the root Tree Inspector:

| Field | Example | Meaning |
| --- | --- | --- |
| Enabled | On | Generate this tree's enum |
| Namespace | Game.Skills | Namespace; may be empty |
| Type Name | SkillId | Enum name |
| Assembly Name | MyGame.Runtime | Exact asmdef assembly name, without .dll |

Use Assembly-CSharp when gameplay scripts have no asmdef. The assembly must exist and contain at least one C# script. Custom asmdefs must reference Game.SkillTree.Data, which brings the generator into scope. Invalid assembly configuration is reported in Console and Inspector validation.

Edit nodes, run **Assets → Refresh**, wait for compilation, then use the enum. Long Stride.asset with ID 17 produces SkillId.Long_Stride = 17:

```csharp
using Game.SkillTree.Data;
using Game.Skills;

SkillNodeConfig FindStride(SkillTreeConfig tree)
{
    return tree.FindNode((int)SkillId.Long_Stride);
}
```

The generator emits a public int enum with None = 0. Other assemblies reference the selected assembly normally; they do not receive duplicate enums.

## Compilation timing

After edits, an idle Editor update writes an XML manifest:

```text
Assets/Game/Generated/SkillTree/<tree GUID>.FatLarva.SkillTree.Generator.additionalfile
```

The writer does not call AssetDatabase.Refresh, ImportAsset, or RequestScriptCompilation. It changes the file only when node membership, IDs, asset names/GUIDs, or generation settings change. Prices, descriptions, effects, and node list order do not change it.

The next normal Unity refresh passes the additionalfile to the generator and compiles the enum with the selected assembly. No generated .cs is written to Assets and there is no second source import.

Unity Auto Refresh can also detect changes, including when the Editor regains focus. Disable Auto Refresh if you want manual compilation timing. The package does not change global Editor preferences or intercept Refresh. Normal C# compilation triggers continue to work.

Commit manifests and .meta files alongside their assets. They let a clean checkout compile code using generated enums before the package's Editor writer runs. Assets remain authoritative; do not edit manifests manually.

## Names and diagnostics

Member names use asset filenames without extensions, not skill display titles. Invalid characters become underscores and leading digits receive an underscore. Unicode letters are preserved and keywords are escaped with @.

| Filename | Member |
| --- | --- |
| Long Stride.asset | Long_Stride |
| 7 Lives.asset | _7_Lives |
| class.asset | @class |
| Stärke.asset | Stärke |

Names that normalize to the same identifier stop generation with a diagnostic. None, value__, and the enum name are reserved. Namespace and type names must be valid C# identifiers.

| Diagnostic | Cause |
| --- | --- |
| STENUM001 | Invalid manifest, ID, GUID, name, or duplicate node ID/name |
| STENUM002 | Two manifests generate the same enum in one assembly |
| STENUM003 | A type with that full name already exists |

Renaming an asset changes its enum member on the next compilation but preserves the number. Update C# references yourself; aliases and migrations are not generated. Saved numeric values remain stable while the nodes and IDs are preserved.

## Generator implementation and builds

Editor/SkillTreeEnumManifestWriter.cs extracts inputs from assets. Editor/SkillTreeEnumManifestFormat.cs supplies format and validation rules and is also compiled into the generator project. SourceGenerator~/SkillTreeEnumGenerator.cs generates only for the exact configured Compilation.AssemblyName.

The compiled DLL is under Runtime/Data/Analyzers. Its .meta applies RoslynAnalyzer and disables Editor/player plugin loading. Placement under Game.SkillTree.Data scopes it to that assembly and its consumers. Do not move it below an Editor asmdef.

To modify the generator, use .NET SDK 8 or later from the package root:

```powershell
./SourceGenerator~/Build.ps1
```

The script builds, runs Roslyn checks, and updates the supplied DLL only after success. The generator targets netstandard2.0 with Microsoft.CodeAnalysis.CSharp 4.3.0. Use -DotNet when the SDK is not on PATH and -NoRestore for already restored dependencies. Sources, projects, and the DLL belong in version control; bin and obj do not. Unity ignores the ~ source directory.

Checks cover consumer compilation, stable IDs, Unicode/keywords, assembly scope, conflicts, invalid inputs, and 600 nodes. Editor verification is still required for DLL import and Refresh behavior.

Unity documentation: [additional files](https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/script-compilation/roslyn-analyzers/additional-files), [analyzer scope](https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/script-compilation/roslyn-analyzers/analyzer-scope-and-diagnostics), [source generators](https://docs.unity.com/en-us/engine/6000.6/manual/scripting/debugging-and-diagnostics/roslyn-analyzers/create-source-generator).
