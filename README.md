# Fat Larva Skill Tree

A uGUI skill tree package with node and level configuration, requirement evaluation, graph layout editing, and a ready-made window prefab. Version **0.2.0**, targeting **Unity 6000.6.8f1**.

The game owns progression, currency, purchases, effect execution, and persistence. `SkillTreePresenter` connects those systems to the view and manages node snapshots and updates.

## Install and run

1. Install Fat Larva Tools through Package Manager → Install package from Git URL: `https://github.com/FatLarva/tools.git#v1.1.0`.
2. Place the package directory at `Packages/com.fatlarva.skilltree`, or select its `package.json` with Install package from disk. For a disk dependency, keep the source directory accessible.
3. Wait for uGUI `2.6.0` and Newtonsoft JSON `3.2.2`. TextMeshPro is included in uGUI.
4. Run **Window → TextMeshPro → Import TMP Essential Resources**.
5. Import **Basic Skill Tree** from Package Manager Samples, open `Scenes/SkillTreeSample.unity`, and enter Play Mode.

The new Input System is optional. Pointer input uses the project's EventSystem; Shift/Alt use a replaceable adapter. Samples choose their input module at startup and prefer the new Input System when both backends are enabled. See [input](Documentation~/input.md).

UPM does not resolve transitive Git dependencies from a package's `package.json`. Add Tools to the project manifest. A disk installation can use:

```json
{
  "dependencies": {
    "com.fatlarva.tools": "https://github.com/FatLarva/tools.git#v1.1.0",
    "com.fatlarva.skilltree": "file:../../SharedPackages/com.fatlarva.skilltree"
  }
}
```

Merge this fragment into the existing manifest. Relative paths start at `Packages`; embedded packages do not need a `file:` entry. The package does not require URP, VContainer, UniTask, FMOD, or the original game.

## Integration

Assign a SkillTreeView prefab to `SkillTreeLayoutConfig.ViewPrefab`. The default is `Runtime/Defaults/Prefabs/SkillTreeWindow.prefab`. Copy it into your game or create a prefab variant; UI references are already assigned.

```csharp
// levels implements ISkillLevelReader.
// sessionContext matches the selected resolver asset.
var presenter = SkillTreePresenter.Create(tree, container, levels, sessionContext,
    () => wallet.Points);
presenter.PurchaseRequested += Purchase;
presenter.Open();

// After progression, balance, or requirement data changes:
presenter.Refresh();

// When the screen's lifetime ends:
presenter.Dispose();
```

The game's Purchase handler rechecks conditions, completes the transaction, applies effects, saves progression, and refreshes the presenter. A UI event is a request, not a completed purchase. See [integration](Documentation~/integration.md).

## Model and extension points

| Component | Responsibility |
| --- | --- |
| SkillTreeConfig | Nodes, requirement/effect catalogs, resolver asset, layout, enum settings |
| SkillNodeConfig | Stable positive int ID, single owner, text, levels |
| SkillLevel | Price, requirements, and effects of a purchased level |
| SkillEffect | Type asset and inline SerializeReference parameters; no execution |
| SkillRequirementResolver&lt;TContext&gt; | Stateless asset factory; shared bindings for validation and runtime dispatch |
| SkillTreePresenter | Node state computation and changed snapshots |
| SkillTreeView | Replaceable panels, buttons, and descriptions |
| SkillTreeGraph | Graph rendering, navigation, selection, layout, and animation |

An effect type can implement ISkillEffectType on a custom ScriptableObject base. Parameters use their normal PropertyDrawer inside the effects list. Resolver factories need no separate bootstrap registration: runtime dependencies belong to the session, not the asset.

## Guides

- [Integration, purchases, and refresh](Documentation~/integration.md)
- [Authoring, levels, layout, and JSON](Documentation~/authoring.md)
- [Resolvers and runtime dependencies](Documentation~/resolvers.md)
- [Effect types, inline data, and Inspector](Documentation~/effects.md)
- [Window prefab and custom UI](Documentation~/views.md)
- [IDs, source generator, and Refresh](Documentation~/code-generation.md)
- [Legacy, new, and custom input](Documentation~/input.md)
- [Design review and possible improvements](Documentation~/review.md)

## Samples

Basic contains 30 nodes, game-owned progression and wallet, a custom effect with a drawer, and an icon renderer example. Large Skill Tree contains an independent 600-node layout. Either sample can be imported alone or alongside the other. Sample progression resets between runs.

Edit imported assets under Assets/Samples, not package originals under Samples~. Disable Allow Editing for gameplay windows.

## Updates and limitations

Refresh is event-driven. It recomputes every node to support arbitrary game requirements, but sends only changed snapshots to the graph. UI objects are retained; connection geometry is cached separately from text and balance. State, visibility, positions, node appearance, style, and camera changes invalidate connection geometry.

There is no dependency index or per-node mesh rebuild. Animation and view changes may rebuild an entire layer. Measure performance in the target Player; the 600-node sample does not guarantee a frame rate.

Assembly names Game.SkillTree* are preserved. Version 0.2 changes IDs, effects, resolver, and view APIs. Old JSON, configs, and saves are not migrated. The original jam game remains on its earlier package version.

## Checks

EditMode tests are in Tests/Editor. Add `"testables": ["com.fatlarva.skilltree"]` to the project manifest for Unity Test Runner. Run generator checks with `SourceGenerator~/Build.ps1`. Compilation and Roslyn checks do not replace Editor import, prefab, and Play Mode verification.
