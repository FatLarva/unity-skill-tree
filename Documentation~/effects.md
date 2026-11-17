# Effect types and inline parameters

The package describes purchased effects. The game applies them and chooses addition, multiplication, reversal, and persistence semantics.

One ScriptableObject describes an effect type. Each SkillLevel.Effects entry references that type and stores its own SerializeReference parameters inside the node.

| Node | Type | Parameters |
| --- | --- | --- |
| Heavy Strike | Stat Modifier | damage, 10, Add |
| Fast Feet | Stat Modifier | speed, 2, Add |
| Heavy Strike, next rank | Stat Modifier | damage, 25, Add |

These use one type asset and three inline parameter objects, not separate assets for each value.

## Add a type

```csharp
using System;
using Game.SkillTree.Data;
using UnityEngine;

[Serializable]
public sealed class DamageParameters : ISkillEffectParameters
{
    [Min(1)] public int Amount = 1;
}

[CreateAssetMenu(menuName = "My Game/Effects/Damage")]
public sealed class DamageEffectType : SkillEffectType<DamageParameters>
{
    protected override bool Validate(DamageParameters parameters, out string error)
    {
        error = parameters.Amount > 0 ? null : "Damage must be positive.";
        return error == null;
    }

    protected override string Describe(DamageParameters parameters)
    {
        return $"Damage +{parameters.Amount}";
    }
}
```

Create the type asset, add it to the root tree's Effect Types catalog, add a level effect, and select Damage. Amount appears inline.

Changing a type creates default parameters; Undo restores the previous type and data. Editing Amount changes the current node, not the shared type asset.

ParametersType must be a concrete Serializable class implementing ISkillEffectParameters. Unity objects and structs cannot be managed parameters. Validation checks schema, values, and catalog membership. Missing types/parameters are errors; Reset parameters replaces incompatible data.

## Existing ScriptableObject bases

SkillEffectType<TParameters> is optional. An existing asset hierarchy can implement ISkillEffectType directly:

```csharp
public sealed class ExistingDamageType : MyExistingAssetBase, ISkillEffectType
{
    public Type ParametersType => typeof(DamageParameters);

    public ISkillEffectParameters CreateParameters()
    {
        return new DamageParameters();
    }

    public bool ValidateParameters(ISkillEffectParameters parameters, out string error)
    {
        error = parameters is DamageParameters { Amount: > 0 }
            ? null
            : "Expected positive damage.";
        return error == null;
    }

    public string Describe(ISkillEffectParameters parameters)
    {
        return $"Damage +{((DamageParameters)parameters).Amount}";
    }
}
```

SkillEffectTypeReference serializes a ScriptableObject reference and validates the interface. Components, GameObjects, and unrelated assets cannot be assigned.

## Custom parameter Inspector

The effects list draws the entire serialized parameter property, allowing its registered PropertyDrawer to render it. Without a drawer, Unity shows ordinary fields.

```csharp
// Place in an Editor assembly.
[CustomPropertyDrawer(typeof(DamageParameters))]
public sealed class DamageParametersDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        return new PropertyField(property.FindPropertyRelative("Amount"), "Extra damage");
    }
}
```

This requires UnityEditor, UnityEditor.UIElements, and UnityEngine.UIElements. Runtime effect types do not reference UnityEditor. The package Inspector uses UI Toolkit; implement OnGUI and GetPropertyHeight too if the same drawer must support a custom IMGUI Inspector.

Basic includes StatModifierEffectType, StatModifierParameters, and a custom drawer as examples, not a mandatory effect schema.

## Create data from code

```csharp
var first = SkillEffect.Create(damageType, new DamageParameters { Amount = 10 });
var second = SkillEffect.Create(damageType, new DamageParameters { Amount = 25 });
var defaults = SkillEffect.Create(damageType);
```

Create validates the asset interface, parameter type, and values. The overload without parameters calls CreateParameters, which must return a new object each time.

Configuration constructors copy entry arrays, not arbitrary managed parameter graphs. Treat parameters as immutable configuration at runtime. Use a separate parameter object per independently authored entry; do not put remaining duration, accumulated bonuses, or player references in it.

## Execution

Read purchased effects from node.Levels[levelIndex].Effects and execute them in game code:

```csharp
foreach (var effect in purchasedLevel.Effects)
{
    if (effect.Parameters is DamageParameters damage)
    {
        combat.AddDamage(damage.Amount);
    }
}
```

If multiple types share a parameter class, also inspect effect.TypeAsset. The package requires no Apply method, effect resolver, DI container, or global stat table. Saving, restoring, recalculating persistent bonuses, and one-shot actions belong to the game.
