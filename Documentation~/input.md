# Input

The graph receives pointer, drag, scroll, and navigation events through the normal uGUI EventSystem. Use the project's input module: StandaloneInputModule for the legacy Input Manager or InputSystemUIInputModule for the new Input System. The package does not change Active Input Handling.

Only two editing modifiers depend on the backend:

| Property | Default key | Action |
| --- | --- | --- |
| MoveSubtree | Either Shift | Move the entire subtree |
| ToggleAdditionalParent | Either Alt | Toggle an additional visual parent |

LegacySkillTreeModifiers is in the main assembly. It reads keys only with ENABLE_LEGACY_INPUT_MANAGER; otherwise it returns false.

InputSystemSkillTreeModifiers is in the optional Game.SkillTree.InputSystem assembly. It compiles with com.unity.inputsystem 1.0 or later and ENABLE_INPUT_SYSTEM. It becomes the default in Editor and at game startup. When both backends are enabled, the new Input System wins. With no keyboard, both modifiers are false.

Implement ISkillTreeModifiers for custom controls:

```csharp
public sealed class MyTreeModifiers : ISkillTreeModifiers
{
    private readonly MyInput _input;

    public MyTreeModifiers(MyInput input)
    {
        _input = input;
    }

    public bool MoveSubtree => _input.MoveWholeBranch;
    public bool ToggleAdditionalParent => _input.AddVisualConnection;
}

graph.Modifiers = new MyTreeModifiers(input);
```

This assignment affects only that graph and overrides the automatic adapter. Assign null to restore the default backend, or NoSkillTreeModifiers.Instance to disable modifiers, for example when using dedicated on-screen commands.

The contract does not introduce a second pointer-input system or modify EventSystem configuration.
