# Authoring a skill tree

## Root configuration

Import Basic and inspect SkillTreeSample.asset for a complete example. To create a tree:

1. Create **Assets → Create → Game → Skill Tree → Tree** and **Layout**.
2. Assign Layout to Tree and set its View Prefab to the default SkillTreeWindow.prefab or your own view.
3. Create **Requirements → Node Level** and **Node Level Resolver**. Add the former to Requirement Types and assign the latter as Requirement Resolver.
4. Set Initial Points for the sample. Real games provide the balance through their own service; this field does not create a wallet.
5. Click **Create node in this tree** in the Tree Inspector. It creates node and layout assets, registers them, and assigns an ID.
6. Give the node file a meaningful name. Enum members use filenames; display Label/Title can differ.
7. Configure levels, prices, requirements, effects, and [Enum Generation](code-generation.md).

Creation supports Undo. Manually created Node and Node Layout assets must be added to Tree.Nodes and Layout.Nodes. An unassigned node receives an ID; duplicate IDs and ownership by another tree are errors.

Each node has exactly one owner. Removing it from a list preserves its ID and owner. Automatic IDs use an increasing counter; renaming and reordering do not change them. Zero is reserved.

## Levels and requirements

Levels determines the number of ranks. Each entry contains the price and conditions for purchasing that rank. Player progress is a separate count of acquired levels, not state stored in the asset.

Hide Until Available controls visibility. Show Requirements specifies reveal conditions; an empty list uses the next level's requirements. Disabling the flag shows the node regardless of those conditions. Visibility and purchase availability are distinct.

The next level supplies the description, or the last level when fully learned. An empty level description falls back to the node description.

Node Level requires a rank of another node in the same tree. **Visual parents do not imply purchase requirements.** Layout can change independently of progression rules. Requirement cycles and visual cycles are validated separately.

Add new requirement type assets to the catalog and bind them in the selected resolver. The Tree Inspector reports the expected runtime context and incomplete configuration. See [resolvers](resolvers.md).

## Effects

The root Effect Types catalog contains assets implementing ISkillEffectType. Each Levels → Effects entry selects a type and edits its own parameters inline. One type can be used repeatedly with different data.

The type defines parameter schema and validation. A custom parameter PropertyDrawer controls its Inspector appearance. The game executes effects. See [effects](effects.md).

## Layout

SkillNodeLayoutConfig stores appearance, position, and visual parents. SkillTreeLayoutConfig combines node layouts, icon/decorations, curves, animation, and the window prefab.

| Action | Default window control |
| --- | --- |
| Pan | Drag the background |
| Zoom | Wheel/pinch or − and + |
| Fit the tree | Fit |
| Move a node | Drag in Edit mode |
| Create and select a node at the center of the view | Add Node in Edit mode |
| Move a subtree | Shift-drag |
| Change the main visual parent | Right-click in Edit mode |
| Toggle an additional parent | Alt + right-click |
| Inspect node and layout | Properties |
| Inspect gameplay data only | Data |
| Automatic layout | Arrange |
| Edit a selected connection | Auto, Mirrored, or Custom |

Editor actions save to assets and support Undo. Samples wire these through SkillTreeDemoAuthoring; gameplay editing is disabled. Shift/Alt mappings can be replaced through [input adapters](input.md).

Create game-specific colors, icons, and decorations in Assets. Runtime/Defaults are shared by multiple trees. Icons can be sprites or custom SkillTreeNodeIconRenderer implementations; SkillTreeNodeDecorationRenderer controls progression/selection decoration.

## JSON

The Tree context menu provides **Export JSON…** and **Import JSON…**. Format 2 stores configuration, not player progress: numeric IDs, asset GUIDs/local file IDs, levels, and references.

Import updates existing assets of the selected tree. It does not create another tree or overwrite arbitrary project assets. GUIDs preserve associations after renaming; conflicting asset identity and numeric IDs are rejected. If a GUID is absent from the selected tree, the matching ID is used. External types and prefab references must resolve in the receiving project.

Data is loaded into temporary objects and validated before application. Imports cannot change node IDs and support Undo. Format 1 and migration are not supported.

To transfer the complete tree, include Unity assets and their .meta files or use an importable sample. JSON does not deliver custom C# types or external resources.
