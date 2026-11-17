using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomPropertyDrawer(typeof(SkillTreeVisualConnection))]
    public sealed class SkillTreeVisualConnectionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var content = new VisualElement();
            var source = property.serializedObject;
            var layout = source.targetObject as SkillNodeLayoutConfig;
            var parent = property.FindPropertyRelative("_parentId");
            var connection = property.FindPropertyRelative("_connection");
            if (layout == null)
            {
                content.Add(new PropertyField(parent, "Parent ID"));
                content.Add(new PropertyField(connection));
                return content;
            }

            var graph = SkillTreeAssetEditor.FindGraph(layout);
            string parentPath = parent.propertyPath;
            string connectionPath = connection.propertyPath;
            if (graph == null)
            {
                content.Add(new PropertyField(parent, "Parent ID"));
            }
            else
            {
                var ids = new List<int> { 0 };
                var names = new List<string> { "Choose a parent" };
                foreach (var node in graph.Nodes)
                {
                    if (node.Id == layout.NodeId || node.Id == layout.ParentId) continue;
                    ids.Add(node.Id);
                    names.Add($"{node.Title} ({node.Id})");
                }
                if (!ids.Contains(parent.intValue))
                {
                    ids.Add(parent.intValue);
                    names.Add(parent.intValue.ToString());
                }
                var dropdown = new DropdownField("Visual parent", names, Mathf.Max(0, ids.IndexOf(parent.intValue)))
                {
                    name = "visualParent"
                };
                content.Add(dropdown);
                dropdown.RegisterValueChangedCallback(change =>
                {
                    int index = names.IndexOf(change.newValue);
                    if (index < 0 || source.targetObject == null) return;
                    source.Update();
                    var current = source.FindProperty(parentPath);
                    if (current == null) return;
                    current.intValue = ids[index];
                    InitializeStyle(source.FindProperty(connectionPath));
                    source.ApplyModifiedProperties();
                    SkillTreeAssetEditor.SaveEditedAsset(layout);
                });
                content.TrackPropertyValue(parent, current =>
                {
                    int index = ids.IndexOf(current.intValue);
                    if (index >= 0) dropdown.SetValueWithoutNotify(names[index]);
                });
            }
            content.RegisterCallback<SerializedPropertyChangeEvent>(change =>
            {
                if (source.targetObject == null || change.changedProperty.propertyPath != parentPath) return;
                source.Update();
                if (InitializeStyle(source.FindProperty(connectionPath))) source.ApplyModifiedProperties();
            });
            SkillTreeInspectorFields.AddConnection(content, source, connection, layout, graph, parentPath);
            return content;
        }

        private static bool InitializeStyle(SerializedProperty connection)
        {
            if (connection == null || connection.FindPropertyRelative("_width").floatValue > 0f) return false;
            connection.FindPropertyRelative("_mode").enumValueIndex = (int)SkillTreeConnectionMode.Auto;
            connection.FindPropertyRelative("_width").floatValue = 1.2f;
            connection.FindPropertyRelative("_useNodeColors").boolValue = true;
            connection.FindPropertyRelative("_startColor").colorValue = Color.white;
            connection.FindPropertyRelative("_endColor").colorValue = Color.white;
            return true;
        }
    }
}
