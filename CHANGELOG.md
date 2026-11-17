# Changelog

## 0.2.0

- Stable positive integer node IDs, single-tree ownership, automatic allocation and validation.
- Roslyn source generator and native additionalfile manifest: filename-based enum names, stable values, generation on Unity compilation.
- Effect type assets implementing ISkillEffectType, inline serializable parameters, custom Inspector drawers; application remains game-owned.
- Stateless typed resolver factory assigned directly to the root config; one binding map for validation and dispatch.
- Prefab-based SkillTreeView and SkillTreePresenter, with snapshot ownership and incremental content delivery.
- Logical input modifiers and optional Input System adapter; Legacy remains supported.
- Cached connection geometry, with invalidation separated from text and wallet updates.
- JSON format 2 with asset identity metadata and integer IDs.
- Independent Basic (30 nodes) and Large (600 nodes) samples.
- Documentation and focused Editor/Roslyn tests.

Breaking release. No migration or compatibility layer for previous configs, JSON or gameplay code.

## 0.1.0

Initial extraction of the reusable skill-tree code, defaults and Basic sample from UnityGameJam2026.