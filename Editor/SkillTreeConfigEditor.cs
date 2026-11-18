using System;
using System.Collections.Generic;
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
            root.Add(new Button(() => SkillTreeAssetEditor.CreateNodeWithDialog(tree, Vector2.zero))
            {
                text = "Create node in this tree"
            });
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

    }
}
