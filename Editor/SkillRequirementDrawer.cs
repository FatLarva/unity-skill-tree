using System.Collections.Generic;
using System.Linq;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomPropertyDrawer(typeof(SkillRequirement))]
    public sealed class SkillRequirementDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var source = property.serializedObject;
            var node = source.targetObject as SkillNodeConfig;
            string path = property.propertyPath;
            var content = new VisualElement();
            var dropdown = new DropdownField("Type") { name = "requirementType" };
            dropdown.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            var fields = new VisualElement();
            content.Add(dropdown);
            content.Add(fields);

            var choices = new List<SkillRequirementType>();
            var labels = new List<string>();

            void RefreshChoices()
            {
                if (source.targetObject == null) return;
                var current = source.FindProperty(path)?.FindPropertyRelative("_type");
                if (current == null) return;
                var type = current.objectReferenceValue as SkillRequirementType;
                choices.Clear();
                AddRegisteredTypes(node, choices);
                int registeredCount = choices.Count;
                int selected = choices.IndexOf(type);
                if (selected < 0)
                {
                    selected = choices.Count;
                    choices.Add(type);
                }

                labels.Clear();
                for (int index = 0; index < choices.Count; index++)
                {
                    var choice = choices[index];
                    string name = choice == null ? "Choose a type" : choice.name;
                    if (choice != null && index >= registeredCount) name = $"Unregistered ({name})";
                    string label = name;
                    for (int suffix = 2; labels.Contains(label); suffix++) label = $"{name} ({suffix})";
                    labels.Add(label);
                }

                dropdown.choices = labels;
                dropdown.SetValueWithoutNotify(labels[selected]);
                dropdown.SetEnabled(registeredCount > 0 && choices.Count > 1 && !source.isEditingMultipleObjects);
            }

            void RefreshFields()
            {
                if (source.targetObject == null) return;
                var current = source.FindProperty(path);
                if (current == null) return;
                fields.Unbind();
                fields.Clear();
                var parameters = current.FindPropertyRelative("_parameters");
                if (parameters.managedReferenceValue == null) return;
                var child = parameters.Copy();
                var end = child.GetEndProperty();
                if (child.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        fields.Add(new PropertyField(child.Copy()));
                    } while (child.NextVisible(false));
                }
                if (fields.panel != null) fields.Bind(source);
            }

            dropdown.RegisterValueChangedCallback(change =>
            {
                int index = labels.IndexOf(change.newValue);
                if (index < 0 || source.targetObject == null) return;
                source.UpdateIfRequiredOrScript();
                var current = source.FindProperty(path);
                if (current == null) return;
                var selected = current.FindPropertyRelative("_type");
                var next = choices[index];
                if (next == null || selected.objectReferenceValue == next) return;
                current.FindPropertyRelative("_parameters").managedReferenceValue = next.CreateParameters();
                selected.objectReferenceValue = next;
                source.ApplyModifiedProperties();
                SkillTreeAssetEditor.SaveEditedAsset(node);
                RefreshChoices();
                RefreshFields();
            });
            dropdown.RegisterCallback<PointerDownEvent>(_ => RefreshChoices(), TrickleDown.TrickleDown);
            dropdown.RegisterCallback<FocusInEvent>(_ => RefreshChoices());
            content.TrackPropertyValue(property.FindPropertyRelative("_type"), _ =>
            {
                RefreshChoices();
                RefreshFields();
            });
            RefreshChoices();
            RefreshFields();
            return content;
        }

        private static void AddRegisteredTypes(SkillNodeConfig node, List<SkillRequirementType> choices)
        {
            if (node == null) return;
            if (Selection.activeObject is SkillTreeNodeInspectorContext context && context.Node == node && context.Tree != null)
            {
                AddTypes(context.Tree, choices);
                return;
            }

            bool hasTree = false;
            foreach (string guid in AssetDatabase.FindAssets("t:SkillTreeConfig"))
            {
                var tree = AssetDatabase.LoadAssetAtPath<SkillTreeConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (tree == null || !tree.Nodes.Contains(node)) continue;
                if (hasTree)
                {
                    choices.RemoveAll(type => !tree.RequirementTypes.Contains(type));
                }
                else
                {
                    AddTypes(tree, choices);
                    hasTree = true;
                }
            }
        }

        private static void AddTypes(SkillTreeConfig tree, List<SkillRequirementType> choices)
        {
            foreach (var type in tree.RequirementTypes)
            {
                if (type != null && !choices.Contains(type)) choices.Add(type);
            }
        }
    }
}
