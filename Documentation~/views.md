# Window prefab and custom UI

SkillTreeLayoutConfig.ViewPrefab references a SkillTreeView at the prefab root. The default Runtime/Defaults/Prefabs/SkillTreeWindow.prefab already contains its hierarchy, layout, fonts, buttons, and references.

## Configure the default window

1. Create a prefab variant or copy the default into Assets.
2. Edit panels, colors, TMP text, and Currency Label. The default font is LiberationSans SDF from TMP Essential Resources; import those resources or assign your own font to text and graph.
3. Assign the root view to your layout's View Prefab and the layout to SkillTreeConfig.
4. Use an existing Canvas with GraphicRaycaster and an EventSystem with the game's input module.

The prefab stretches to its parent. Its default layout targets a CanvasScaler reference resolution of 1920 × 1080. Customize anchors/layout groups in the prefab. SkillTreeWindow does not replace panel geometry on selection or opening. Graph elements, icons, and level indicators are created dynamically inside the configured containers.

Every default-window reference except Wallet Icon is required, including hidden editor buttons. Validate reports the missing field and the presenter logs it. A custom SkillTreeView can expose a smaller UI contract.

Editing is disabled by default. SetEditingAllowed(true) exposes editing controls; samples demonstrate asset editor integration. Use a prefab variant for decoration or derive from SkillTreeWindow and override ShowDetails for behavior. Call base OnEnable/OnDisable when overriding them to retain subscriptions.

## Presenter lifetime

```csharp
// levels: ISkillLevelReader for this game session.
// context: dependencies expected by the selected resolver asset.
// getWallet: Func<int> returning the current balance.
var presenter = SkillTreePresenter.Create(tree, container, levels, context, getWallet);
presenter.PurchaseRequested += OnPurchaseRequested;
presenter.Open();

// After changes to progression, balance, or requirement data:
presenter.Refresh();

// Hide temporarily without destroying the view:
presenter.Close();

// End the session:
presenter.PurchaseRequested -= OnPurchaseRequested;
presenter.Dispose();
```

Create owns and destroys the instantiated ViewPrefab. A presenter constructed with an existing view removes subscriptions on Dispose but leaves that view alive. An inactive window can be populated before Open.

PurchaseRequested expresses intent. The game validates, changes progression/balance, applies effects, and refreshes. The presenter reevaluates all nodes, owns snapshots, and sends changed content through UpdateContent. Membership/order changes use SetContent. Game integration does not need manual Clone calls or dependency tracking. Refresh from game events rather than every frame.

Nodes, Wallet, GetNodeInfo, TryGetNodeInfo, and Refreshed support additional UI. SkillTreeNodeInfo.NextLevel contains the next rank's price, requirements, effects, and description, or null after the final rank. Definition and effect parameters are authored data, not player state. The constructor populates initial data, so read once immediately after subscribing to Refreshed.

## Minimal custom view

Attach this component to your prefab root and assign the child Graph and four UI references. Connect a close button to CloseFromButton in the Inspector.

```csharp
using System.Collections.Generic;
using Game.SkillTree;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CompactSkillTreeView : SkillTreeView
{
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _wallet;
    [SerializeField] private TMP_Text _feedback;
    [SerializeField] private Button _purchase;
    private SkillTreeNode _selected;
    private bool _allowPurchase;

    private void OnEnable()
    {
        Graph.Selected += ShowSelection;
        _purchase.onClick.AddListener(BuySelected);
        RefreshSelection();
    }

    private void OnDisable()
    {
        Graph.Selected -= ShowSelection;
        _purchase.onClick.RemoveListener(BuySelected);
    }

    public override void SetContent(IEnumerable<SkillTreeNode> nodes, int wallet, bool allowPurchase = true)
    {
        ShowWallet(wallet, allowPurchase);
        Graph.SetNodes(nodes);
        RefreshSelection();
    }

    public override void UpdateContent(IEnumerable<SkillTreeNode> changes, int wallet, bool allowPurchase = true)
    {
        ShowWallet(wallet, allowPurchase);
        Graph.UpdateNodes(changes);
        RefreshSelection();
    }

    public override void SetFeedback(string message)
    {
        _feedback.text = message;
    }

    public void CloseFromButton()
    {
        RequestClose();
    }

    public override bool Validate(out string error)
    {
        if (!base.Validate(out error))
        {
            return false;
        }

        if (_title && _wallet && _feedback && _purchase)
        {
            return true;
        }

        error = $"Assign all UI references on '{name}'.";
        return false;
    }

    private void ShowWallet(int wallet, bool allowPurchase)
    {
        _wallet.text = wallet.ToString();
        _allowPurchase = allowPurchase;
        Graph.AllowPurchase = allowPurchase;
    }

    private void RefreshSelection()
    {
        SkillTreeNode selected = null;
        foreach (var node in Graph.Nodes)
        {
            if (node.Id == Graph.SelectedId)
            {
                selected = node;
                break;
            }
        }

        ShowSelection(selected);
    }

    private void ShowSelection(SkillTreeNode node)
    {
        _selected = node;
        _title.text = node?.Title ?? "Select a skill";
        _purchase.interactable = _allowPurchase && node != null && node.CanPurchase;
    }

    private void BuySelected()
    {
        if (_allowPurchase && _selected != null && _selected.CanPurchase)
        {
            RequestPurchase(_selected.Id);
        }
    }
}
```

Reference Game.SkillTree, Unity.TextMeshPro, and UnityEngine.UI; presenter code also needs Game.SkillTree.Data. The view itself consumes display data and need not reference Data. A controller can read presenter.GetNodeInfo after Graph.Selected/Refreshed for a custom effects panel.

A custom view must implement both content paths, including balance-only changes, and own UI subscriptions. Graph geometry and interaction remain the package implementation.
