using System;
using System.Collections.Generic;
using System.IO;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomEditor(typeof(SkillTreeConfig))]
    public sealed class SkillTreeConfigEditor : SkillTreeInspectorBase
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = BeginInspector();
            var tree = (SkillTreeConfig)target;
            root.Add(new Button(() => CreateNode(tree)) { text = "Create node in this tree" });
            var details = new VisualElement();
            details.AddToClassList("skill-tree-resolver-details");
            var implementation = new Label
            {
                name = "requirementResolverImplementation"
            };
            implementation.AddToClassList("skill-tree-resolver-implementation");
            var error = new HelpBox(string.Empty, HelpBoxMessageType.Error)
            {
                name = "requirementResolverValidation"
            };
            details.Add(implementation);
            details.Add(error);
            var property = serializedObject.GetIterator();
            if (property.NextVisible(true))
            {
                do
                {
                    var field = new PropertyField(property.Copy());
                    if (property.propertyPath == "m_Script") field.SetEnabled(false);
                    root.Add(field);
                    if (property.propertyPath == "_requirementResolver")
                    {
                        root.Add(details);
                    }
                }
                while (property.NextVisible(false));
            }

            void RefreshValidation()
            {
                if (tree == null) return;
                var resolver = tree.RequirementResolver;
                implementation.text = "Runtime dependencies: " + (resolver != null
                    ? resolver.ContextType?.Name ?? "Missing dependency contract"
                    : "Assign a resolver SO");
                implementation.tooltip = resolver != null
                    ? $"{resolver.ContextType?.FullName}\nPass this contract when creating the runtime resolver. The asset never stores session dependencies."
                    : "The tree requires an explicit requirement resolver asset.";
                bool valid = tree.Validate(out string message) && SkillTreeEnumManifestWriter.Validate(tree, out message);
                error.EnableInClassList("skill-tree-hidden", valid);
                error.text = valid ? string.Empty : message;
            }

            bool membershipQueued = false;
            root.RegisterCallback<SerializedPropertyChangeEvent>(change =>
            {
                if (change.changedProperty.propertyPath.StartsWith("_nodes", StringComparison.Ordinal) && !membershipQueued)
                {
                    membershipQueued = true;
                    EditorApplication.delayCall += () =>
                    {
                        membershipQueued = false;
                        if (tree == null) return;
                        try
                        {
                            var objects = new List<UnityEngine.Object> { tree };
                            foreach (var node in tree.Nodes) if (node != null) objects.Add(node);
                            Undo.RecordObjects(objects.ToArray(), "Assign skill node identifiers");
                            tree.SynchronizeNodeIds();
                            foreach (var item in objects) EditorUtility.SetDirty(item);
                            tree.NotifyChanged();
                            AssetDatabase.SaveAssets();
                        }
                        catch (Exception exception)
                        {
                            Debug.LogError(exception.Message, tree);
                        }
                        RefreshValidation();
                    };
                }
                RefreshValidation();
            });
            root.schedule.Execute(RefreshValidation).Every(200);
            RefreshValidation();
            return root;
        }

        private static void CreateNode(SkillTreeConfig tree)
        {
            if (tree.Layout == null)
            {
                Debug.LogError("Assign a layout config before creating a node.", tree);
                return;
            }
            string treePath = AssetDatabase.GetAssetPath(tree);
            string directory = Path.GetDirectoryName(treePath)?.Replace('\\', '/') ?? "Assets";
            string path = EditorUtility.SaveFilePanelInProject("Create skill node", "NewSkill", "asset",
                "Choose the node filename; it also defines its generated enum name.", directory);
            if (string.IsNullOrEmpty(path)) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create skill node");
            Undo.RecordObjects(new UnityEngine.Object[] { tree, tree.Layout }, "Create skill node");
            var node = ScriptableObject.CreateInstance<SkillNodeConfig>();
            AssetDatabase.CreateAsset(node, path);
            Undo.RegisterCreatedObjectUndo(node, "Create skill node");
            try
            {
                tree.AddNode(node);
                string title = Path.GetFileNameWithoutExtension(path);
                node.Configure(new SkillNodeProperties(node.Id, title, title, string.Empty, string.Empty,
                    SkillTreeNodeKind.Skill, false, new[] { new SkillLevel(0) }));
                var appearance = ScriptableObject.CreateInstance<SkillNodeLayoutConfig>();
                appearance.Configure(new SkillTreeNodeLayout(node.Id, 0, Vector2.zero, Color.white));
                string appearancePath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, "layout.asset"));
                AssetDatabase.CreateAsset(appearance, appearancePath);
                Undo.RegisterCreatedObjectUndo(appearance, "Create skill node");
                var layouts = new List<SkillNodeLayoutConfig>(tree.Layout.Nodes) { appearance };
                tree.Layout.Configure(layouts);
                EditorUtility.SetDirty(tree);
                EditorUtility.SetDirty(tree.Layout);
                EditorUtility.SetDirty(node);
                AssetDatabase.SaveAssets();
                Selection.activeObject = node;
                Undo.CollapseUndoOperations(group);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError(exception.Message, tree);
            }
        }
    }
}
