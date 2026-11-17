# Design review and possible improvements

This document separates version 0.2 changes from proposals. The original game and extracted 0.1 package keep their earlier API; this revision is developed independently.

## Changes in 0.2

| Original approach | Package limitation | Current design |
| --- | --- | --- |
| Input System directly inside the graph | Project-wide setup for Shift/Alt | Logical modifiers with Legacy/New adapters; pointer input through EventSystem |
| Manually entered string IDs | Typos and unstable references | Positive int IDs, one owner, automatic assignment and duplicate checks |
| String references in C# | No compiler validation or Find Usages | Generated enums from asset names and stable IDs through additionalfile |
| Fixed Target/Id/Value/Operation effect format | Imposed game semantics | Type assets, inline managed parameters, interface, and custom PropertyDrawer |
| Resolver description plus external factory registration | Extra setup and duplicated type support | Resolver assets are factories; typed bindings drive support and dispatch |
| Chains as the default composition | Linear lookup and extra objects | Direct type dispatch; chains remain an explicit option |
| Window constructed entirely from code | Difficult Editor customization | Layout references a validated prefab with SkillTreeView |
| Manual snapshots and Clone | Missed dependencies or broken comparison | Presenter owns projection, snapshots, and change detection |
| Shared geometry invalidation | Text/balance could rebuild connections | Separate connection cache with explicit invalidation causes |
| Large layout kept outside the package | Installation could not reproduce load | Independent 600-node sample |

## Boundaries to preserve

**The game owns progression and purchases.** It chooses an in-memory dictionary, a server transaction, or persistent-effect recalculation. The package does not silently debit currency, apply bonuses, or save accounts.

**Assets describe configuration; runtime objects read services.** Configure declares bindings and session factories receive their own context. Editor checks schema and coverage but cannot prove arbitrary live-service correctness.

**Visual connections and requirements are independent.** Layout changes must not alter progression rules by turning every visual parent into a purchase dependency.

**The graph is the core and the window is extensible.** Custom panels and descriptions can reuse graph behavior. Making every rendering stage replaceable would introduce a larger extension framework.

**EventSystem already handles pointer input.** The package abstraction covers only the two editing modifiers not represented by those events.

## Proposals

These are not implemented automatically.

| Priority | Proposal | Scope |
| --- | --- | --- |
| High | Profile 30/600 nodes in the target Player | Separate projection, TMP, curves, Canvas rebuild, and GPU costs before optimizing |
| High | Clarify Editor/game mode in the window | Show when edits write to assets and that connections are not requirements |
| Medium | Navigable list of tree validation errors | Current validation reports the first error |
| Medium | Reduce projection allocations | Reuse temporary data after profiling while preserving snapshot ownership |
| Medium | Optional dependency index | Only for explicitly declared dependencies; arbitrary resolvers still need full reevaluation |
| Medium | Search and focus in large graphs | Define selection, centering, and hidden-node behavior |
| Medium | Localizable requirement/effect descriptions | Separate presentation data from effect execution |
| Low | Simplify asmref/directory layout | Requirements currently reside under Graph but compile into Data |

A dependency index needs a new contract, and per-node/chunk geometry needs different buffers and invalidation. A large sample alone does not justify either change.

## UX considerations

- Make main and additional visual-parent connections distinct during editing.
- Existing API exposes definitions, levels, prices, and requirements. Before/after effect comparisons still need game semantics.
- Parameter drawers cover inline editing; reordering, copying, and bulk editing are separate tasks.
- Spatial gamepad navigation, Inspector scroll persistence, branch duplication, and connection-style behavior after reparenting remain open decisions.
- Internal layout JSON and assembly restructuring were not changed in this release.

## Verification

Assemblies have explicit dependencies and can compile independently of the original game. Input backends and samples require their own checks. Tests cover effects, resolver contracts, independent sessions, identity, JSON, and view lifetime. The generator has separate Roslyn checks.

Compiling tests does not execute Unity Test Runner, and checking GUID/fileID references does not visually verify a prefab. Consult the project-specific verification results for what was actually run.
