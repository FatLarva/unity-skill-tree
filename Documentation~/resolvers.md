# Requirement evaluation

SkillTreeConfig.RequirementResolver is a required ScriptableObject factory selected on the root tree. Typed bindings provide both Editor support checks and runtime dispatch. No separate bootstrap/DI factory registration is needed.

## Default Node Level requirement

1. Create **Game → Skill Tree → Requirements → Node Level** and add it to Requirement Types.
2. Create **Node Level Resolver** and assign Requirement Resolver.
3. Select Node Level on a node's level and specify the required node/rank.
4. Pass current-session progression to the factory:

```csharp
using System.Collections.Generic;
using Game.SkillTree.Requirements;

// Game-owned progression, keyed by stable node IDs.
var levels = new Dictionary<int, int>();
var reader = new SkillLevelReader(levels);
var evaluator = new SkillRequirementEvaluator(tree, reader);

if (evaluator.Validate(out var error))
{
    bool canLearnFirstRank = evaluator.MeetsRequirements(node, levelIndex: 0);
    bool visible = evaluator.IsVisible(node, acquiredLevels: 0);
}
```

SkillLevelReader reads the supplied dictionary directly and clamps ranks to the node's level range. After purchasing, the game updates progression and refreshes its presenter. Evaluation does not debit currency or apply effects.

## Custom dependencies and checks

Use a DTO or interface exposing the specific dependencies needed by requirements. A service locator or entire account object is unnecessary.

```csharp
public sealed class GameRequirementContext
{
    public ISkillLevelReader Levels { get; }
    public System.Func<int> GetLuckLevel { get; }

    public GameRequirementContext(ISkillLevelReader levels, System.Func<int> getLuckLevel)
    {
        Levels = levels;
        GetLuckLevel = getLuckLevel;
    }
}
```

A requirement type defines schema and descriptions; each node level stores its own inline parameters:

```csharp
using System;
using Game.SkillTree.Data;
using UnityEngine;

[CreateAssetMenu(menuName = "Example/Requirements/Luck Level")]
public sealed class LuckLevelRequirement : SkillRequirementType
{
    [Serializable]
    public sealed class Parameters : SkillRequirementParameters
    {
        [SerializeField, Min(0)] private int _minimum = 1;
        public int Minimum => _minimum;
    }

    public override SkillRequirementParameters CreateParameters()
    {
        return new Parameters();
    }

    public override bool AreParametersValid(SkillRequirementParameters parameters)
    {
        return parameters is Parameters { Minimum: >= 0 };
    }

    public override string Describe(SkillRequirementParameters parameters, string rankLabel)
    {
        return parameters is Parameters value ? $"Luck level {value.Minimum}" : "Invalid luck requirement";
    }
}

public sealed class LuckLevelCheck
{
    private readonly Func<int> _getLuckLevel;

    public LuckLevelCheck(Func<int> getLuckLevel)
    {
        _getLuckLevel = getLuckLevel ?? throw new ArgumentNullException(nameof(getLuckLevel));
    }

    public bool IsMet(LuckLevelRequirement.Parameters parameters)
    {
        return _getLuckLevel() >= parameters.Minimum;
    }
}
```

Derive the factory from SkillRequirementResolver<GameRequirementContext>. Put each Unity asset class in a matching filename.

```csharp
using Game.SkillTree.Data;
using Game.SkillTree.Requirements;
using UnityEngine;

[CreateAssetMenu(menuName = "Example/Requirements/Game Resolver")]
public sealed class GameRequirementResolver : SkillRequirementResolver<GameRequirementContext>
{
    protected override void Configure(RequirementBindings<GameRequirementContext> bindings)
    {
        bindings.Add<SkillNodeLevelRequirementType, SkillNodeLevelRequirementType.Parameters>(
            context => new SkillNodeLevelCheck(context.Levels).IsMet);
        bindings.Add<LuckLevelRequirement, LuckLevelRequirement.Parameters>(
            context => new LuckLevelCheck(context.GetLuckLevel).IsMet);
    }
}
```

Assign the factory to the root tree and add both requirement types to its catalog:

```csharp
var context = new GameRequirementContext(reader, () => account.LuckLevel);
var evaluator = new SkillRequirementEvaluator(tree, context);
```

Checker constructors may take arbitrary dependencies selected explicitly from the context. Factories run once per resolver session, not per check. Returned delegates retain their checker; sessions sharing one asset receive independent checkers and contexts.

Configure declares bindings and must not create runtime checkers or access live services itself. Runtime work belongs inside the context factory. Contexts, checkers, and bound delegates are not stored in the asset. Changing the factory's revision recreates evaluator handlers; ordinary progression changes do not.

## Validation

- Supports derives from Add and uses the exact CLR requirement type. Derived types need their own binding; multiple assets of one CLR type share a binding.
- ValidateBindings detects duplicate registrations and null factories without constructing checkers.
- ValidateRequirementType checks binding coverage and the type returned by CreateParameters. Default parameter values need not yet satisfy a configured requirement.
- Runtime validation checks context type, factory result, parameter type, and the requirement asset's AreParametersValid before dispatch.
- Editor validation cannot prove service availability. Constructor failures and null callbacks appear when creating the session.

Because the Inspector field uses a non-generic base, the selected asset's context type is checked when the resolver is created. Diagnostics name expected and actual types. Interface contexts are supported.

## Chains and diagnostics

A normal resolver dispatches directly by registered type. If an integration needs multiple resolvers, it can explicitly create SkillRequirementResolverChain(first, second). A chain stops on Handled, including an unmet requirement. Only Unhandled proceeds to the next resolver.

Runtime ISkillRequirementResolver does not duplicate Supports; the asset's bindings are authoritative. Unknown types return Unhandled and the evaluator reports a missing handler and an unmet requirement.

SkillRequirementEvaluator logs configuration errors with the root tree as context and returns the message from Validate. Identical repeated errors are suppressed until successful validation. Configuration errors are distinct from ordinary unmet conditions and can be displayed separately.

Runtime checker exceptions are logged and make that requirement unmet, without preventing other nodes from being checked. Later calls retry against current game state. A successful call resets duplicate-error suppression. Validate covers configuration and resolver creation; MeetsRequirements and IsVisible return evaluation results.

Checkers receive parameters, not a requirement asset. Put per-check settings in TParameters; do not rely on hidden runtime state of different assets of the same CLR type. Checkers synchronously read injected services and do not own subscriptions or resources. The game owns service lifetime.

## Tests

Game.SkillTree.Requirements.Tests covers Editor/runtime separation, independent sessions, binding uniqueness, invalid parameters/contexts/factories, chain semantics, and repeated diagnostic suppression. Add the package to manifest testables to run the EditMode tests.
