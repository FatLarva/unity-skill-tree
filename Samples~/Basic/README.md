# Basic Skill Tree

A standalone 30-node example demonstrating the window prefab, level purchases, a game-owned wallet, requirements, editable effect parameters, and a custom arrow icon renderer.

## Run

1. Import **Basic Skill Tree** from Package Manager Samples.
2. Run **Window → TextMeshPro → Import TMP Essential Resources** if needed.
3. Open `Scenes/SkillTreeSample.unity` and enter Play Mode.

SkillTreeDemo already references Container and Config. The tree has a requirement resolver and the layout references the default window prefab. No manual factory registration or uGUI construction is needed.

The scene adds the input module for your project when Play starts. It uses the legacy Input Manager in Old mode and the Input System in New or Both mode. When integrating the tree into your own scene, use that scene's configured EventSystem.

## Try it

- Select a node, inspect requirements, and purchase an available rank.
- Drag the background to pan; use the wheel to zoom and Fit for an overview.
- Enable Edit in the Unity Editor. Shift-drag moves a subtree; right-click changes the main visual parent; Alt-right-click toggles an additional parent.
- In Edit mode, Add Node creates a node and its layout at the center of the view. Properties and Arrange open the Inspector; Data opens the selected node asset. Asset edits support Undo.
- SkillTreeDemo's component menu provides **Add 100 Test Points**, **Reset Test Progress**, and **Open Skill Tree**.

Layout Reset restores the layout captured at session start. Reset Test Progress resets wallet, levels, and applied effects. Progress does not persist between runs.

## Integration

Scripts/SkillTreeDemo.cs contains the main path:

```csharp
var progress = new SkillTreeDemoProgression(config);
var presenter = SkillTreePresenter.Create(
    config, container, progress, progress, () => progress.Wallet);

presenter.PurchaseRequested += id =>
{
    if (progress.TryPurchase(id, out var message))
    {
        presenter.Refresh();
    }

    presenter.View.SetFeedback(message);
};
presenter.Open();
// Dispose the presenter when the session ends.
```

SkillTreeDemoProgression implements ISkillLevelReader, rechecks purchases, debits currency, stores levels by int ID, and applies effects. The presenter reads this state and sends changed snapshots to the graph. Call Refresh after changes; do not construct SkillTreeNode snapshots or call Clone yourself.

SkillTreeDemoAuthoring optionally connects editing controls. It responds to asset-change notifications instead of scanning every node each frame. Editing is disabled in Players.

## Effects

One StatModifierEffectType asset describes effects across nodes. Each entry stores its own StatModifierParameters: Stat, Value, and Operation. Different values do not require separate type assets.

Editor/StatModifierParametersDrawer.cs draws the Inspector. SkillTreeDemoProgression.Apply executes effects. Values begin at zero; Add, Multiply, and Set follow purchase order. Already applied effects are not recomputed after asset edits; reset test progress to check the new data.

New types need a descriptor asset, serializable parameters, and a game handler. The package does not execute effects. This demo explicitly rejects purchases containing types without a handler.

## Data

- Data/SkillTree/SkillTreeSample.asset is the root configuration.
- Nodes and Layouts each contain 30 entries.
- Every node has this tree as owner and a unique positive int ID.
- The enum is Game.SkillTree.Samples.Generated.BasicSkillId in Game.SkillTree.Samples. Runtime sample code does not depend on the enum and works before its first generation.
- JSON exports asset configuration, not player progression.

The large sample is independent and imported separately. Shared contracts and guides are in the package documentation.
