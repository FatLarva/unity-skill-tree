# Integrating with a game

Integration consists of tree configuration, a window prefab, and SkillTreePresenter. The presenter reads game progression and balance; the game handles purchase requests.

## Setup

1. Reference Game.SkillTree and Game.SkillTree.Data from the gameplay asmdef. Custom uGUI code also needs UnityEngine.UI and, when using TMP, Unity.TextMeshPro.
2. Prepare a Canvas with GraphicRaycaster, a RectTransform container, and the project's EventSystem/input module.
3. Assign Layout, nodes, type catalogs, and Requirement Resolver in SkillTreeConfig. The default Node Level Resolver expects ISkillLevelReader.
4. Assign the default SkillTreeWindow.prefab or your own SkillTreeView to Layout.View Prefab. Configure currency text, fonts, and editing controls in the prefab.
5. Call SkillTreePresenter.Create, or use the constructor taking an existing view.

See [authoring](authoring.md), or import Basic for a complete configuration.

## Minimal example

This example stores progression in memory. For Node Level Resolver, one SkillLevelReader provides progression and requirement context.

```csharp
using System.Collections.Generic;
using Game.SkillTree.Data;
using Game.SkillTree.Requirements;
using UnityEngine;

public sealed class TreeScreen : MonoBehaviour
{
    [SerializeField] private SkillTreeConfig _tree;
    [SerializeField] private RectTransform _container;

    private readonly Dictionary<int, int> _progress = new();
    private SkillTreePresenter _presenter;
    private int _points;

    private void Start()
    {
        _points = _tree.InitialPoints;
        var levels = new SkillLevelReader(_progress);
        _presenter = SkillTreePresenter.Create(
            _tree, _container, levels, levels, () => _points);
        _presenter.PurchaseRequested += Purchase;
        _presenter.Open();
    }

    private void Purchase(int id)
    {
        if (!_presenter.Refresh() ||
            !_presenter.TryGetNodeInfo(id, out var info) || !info.CanPurchase)
        {
            return;
        }

        var purchasedLevel = info.Definition.Levels[info.Level];
        _points -= info.Cost;
        _progress[id] = info.Level + 1;

        // Apply purchasedLevel.Effects through game systems and save progression.
        _presenter.Refresh();
    }

    private void OnDestroy()
    {
        if (_presenter == null)
        {
            return;
        }

        _presenter.PurchaseRequested -= Purchase;
        _presenter.Dispose();
    }
}
```

Real games should validate and debit through a purchase service. That service owns atomicity and revalidation for asynchronous/server transactions. A UI snapshot does not authorize a debit. Basic's SkillTreeDemoProgression separates progression and effect execution from the view.

## Refresh

Call presenter.Refresh after skill levels, balance, or any requirement dependency changes, including quests, inventory, and character level. The package cannot know which services a custom resolver reads.

The presenter computes levels, visibility, next price, availability, descriptions, requirements, and layout. It compares snapshots and calls View.UpdateContent for changes, or SetContent when node membership/order changes. Balance updates also work with no changed nodes.

Every node is reevaluated to support arbitrary requirements. Graph objects survive purchases and connection geometry is cached separately from text and purchase highlighting.

Refresh(false) disables purchasing. Open(selectedId, fitView) shows the window and Close hides it. Refresh before reopening after game data changes.

## Lifetime

Create instantiates Layout.ViewPrefab. Dispose removes subscriptions, closes the editor context, and destroys that instance. A presenter constructed with an existing view leaves the view alive. Use a separate presenter for each screen/session.

Pass current session dependencies as requirementContext. Recreate the presenter when the session changes. The shared resolver asset must not retain accounts, wallets, or DI containers. See [resolvers](resolvers.md).

Treat configuration and inline effect parameters as immutable runtime data. Store progression separately. Rank is the number of acquired levels: zero means none; the next level index is initially zero, then one, and so on.

## Custom presentation

Implement SkillTreeView or customize SkillTreeWindow. Nodes, GetNodeInfo, Wallet, and Refreshed provide data for additional panels. Keep SkillTreeGraph as the graph core; see [views](views.md).

Custom presenters may call SetContent and UpdateContent directly, but must build snapshots, apply layout, and reevaluate dependencies themselves. Do not mutate a previously submitted SkillTreeNode in place: use Clone or a new instance so changes remain detectable. The standard presenter handles this ownership.

## Diagnostics

| Symptom | Check |
| --- | --- |
| Missing view prefab | Layout.View Prefab |
| Missing resolver or requirement support | Resolver asset, Configure, and type catalog |
| Wrong runtime context | Argument type and selected factory's ContextType |
| Incomplete prefab | view.Validate names the missing reference |
| Unavailable node | Maximum rank, visibility, requirements, balance, allowPurchase |
| Stale display | Refresh after the game service changes |

config.Validate checks configuration. SkillRequirementEvaluator.Validate also checks runtime resolver creation. Console errors include the tree context and identical repeated errors are suppressed. Invalid configuration makes Refresh return false, disables purchasing, and sends feedback to the view.

Custom editor changes should use Undo, mark assets dirty, and call NotifyChanged when the API does not already do so. Package Inspectors handle this. Never store runtime progression in configuration assets.
