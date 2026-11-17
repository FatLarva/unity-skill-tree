using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    internal static class SkillTreeInspectorFields
    {
        private const string StylePath = "Packages/com.fatlarva.skilltree/Editor/SkillTreeInspector.uss";

        public static VisualElement Root()
        {
            var root = new VisualElement();
            root.AddToClassList("skill-tree-inspector");
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null)
            {
                root.styleSheets.Add(style);
            }
            return root;
        }

        public static VisualElement Gameplay(SkillNodeConfig node, List<SerializedObject> bindings)
        {
            var content = new VisualElement();
            var serialized = new SerializedObject(node);
            bindings.Add(serialized);
            var properties = serialized.FindProperty("_properties");
            Add(content, properties, "_id", "ID (assigned by tree)").SetEnabled(false);
            Add(content, properties, "_kind", "Kind");
            Add(content, properties, "_title", "Title");
            Add(content, properties, "_label", "Graph label");
            Add(content, properties, "_category", "Category");
            Add(content, properties, "_description", "Description");
            Add(content, properties, "_hideUntilAvailable", "Hide if unavailable");
            Add(content, properties, "_showRequirements", "Show Requirements");
            var levelHelp = new HelpBox("Each array element is one level. Its price and requirements apply to that purchase. An empty level description uses the node description.", HelpBoxMessageType.Info);
            levelHelp.AddToClassList("skill-tree-help");
            content.Add(levelHelp);
            Add(content, properties, "_levels", "Levels");
            BindAsset(content, serialized, node);
            return content;
        }

        public static VisualElement Appearance(SkillNodeLayoutConfig layout, List<SerializedObject> bindings, SkillTreeGraph graph = null)
        {
            var content = new VisualElement();
            var serialized = new SerializedObject(layout);
            bindings.Add(serialized);
            var properties = serialized.FindProperty("_properties");
            Add(content, properties, "_nodeId", "Node ID").SetEnabled(false);
            Add(content, properties, "_parentId", "Visual parent ID");
            Add(content, properties, "_position", "Position");
            Add(content, properties, "_color", "Color");
            var iconMode = properties.FindPropertyRelative("_iconMode");
            Add(content, properties, "_iconMode", "Icon mode");
            var graphicIcon = new VisualElement { name = "graphicIconSettings" };
            content.Add(graphicIcon);
            AddRendererDropdown(graphicIcon, serialized, properties.FindPropertyRelative("_iconRenderer"), layout);
            var spriteIcon = Add(content, properties, "_iconSprite", "Icon sprite");
            void UpdateIconMode(SkillTreeIconMode mode)
            {
                graphicIcon.EnableInClassList("skill-tree-hidden", mode != SkillTreeIconMode.GraphicIcon);
                spriteIcon.EnableInClassList("skill-tree-hidden", mode != SkillTreeIconMode.Sprite);
            }
            UpdateIconMode((SkillTreeIconMode)iconMode.enumValueIndex);
            content.TrackPropertyValue(iconMode, updated => UpdateIconMode((SkillTreeIconMode)updated.enumValueIndex));
            Add(content, properties, "_iconScale", "Map icon scale");
            Add(content, properties, "_iconRotation", "Icon rotation");
            AddConnection(content, serialized, properties.FindPropertyRelative("_connection"), layout, graph,
                properties.FindPropertyRelative("_parentId").propertyPath);
            Add(content, properties, "_additionalParents", "Additional visual parents");
            BindAsset(content, serialized, layout);
            return content;
        }

        public static PropertyField Add(VisualElement content, SerializedProperty parent, string name, string label)
        {
            var property = parent.FindPropertyRelative(name);
            var field = new PropertyField(property, label) { name = name.TrimStart('_') };
            content.Add(field);
            return field;
        }

        private static void BindAsset(VisualElement content, SerializedObject serialized, SkillTreeConfigAsset asset)
        {
            bool queued = false;
            content.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (queued)
                {
                    return;
                }
                queued = true;
                EditorApplication.delayCall += Save;
            });
            void Save()
            {
                queued = false;
                if (asset != null)
                {
                    SkillTreeAssetEditor.SaveEditedAsset(asset);
                }
            }
            content.Bind(serialized);
            content.RegisterCallback<AttachToPanelEvent>(_ => content.Bind(serialized));
            content.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                content.Unbind();
                if (queued)
                {
                    EditorApplication.delayCall -= Save;
                    Save();
                }
            });
        }

        private static void AddRendererDropdown(VisualElement content, SerializedObject serialized,
            SerializedProperty property, SkillNodeLayoutConfig layout)
        {
            var renderers = new List<SkillTreeNodeIconRenderer>();
            var labels = new List<string>();
            string path = property.propertyPath;
            var dropdown = new DropdownField("Graphic icon") { name = "iconRenderer" };
            content.Add(dropdown);
            var unavailable = new HelpBox("This Graphic Icon is not included in the tree layout's icon renderer list.", HelpBoxMessageType.Warning)
            {
                name = "unassignedIconRenderer"
            };
            content.Add(unavailable);
            void UpdateSelection(SkillTreeNodeIconRenderer renderer)
            {
                int selected = renderers.IndexOf(renderer);
                bool registered = selected >= 0;
                if (selected < 0)
                {
                    selected = renderers.Count;
                    renderers.Add(renderer);
                    labels.Add($"Not registered ({renderer.name})");
                    dropdown.choices = labels;
                }
                dropdown.SetValueWithoutNotify(labels[selected]);
                unavailable.EnableInClassList("skill-tree-hidden", registered);
            }
            void RefreshChoices()
            {
                if (serialized.targetObject == null) return;
                renderers.Clear();
                labels.Clear();
                foreach (var choice in SkillTreeRendererChoices.Get(layout))
                {
                    renderers.Add(choice.Renderer);
                    labels.Add(choice.Label);
                }
                dropdown.choices = labels;
                serialized.Update();
                var current = serialized.FindProperty(path).objectReferenceValue as SkillTreeNodeIconRenderer;
                dropdown.SetEnabled(renderers.Count > 1 || current != null);
                UpdateSelection(current);
            }
            RefreshChoices();
            dropdown.RegisterCallback<PointerDownEvent>(_ => RefreshChoices(), TrickleDown.TrickleDown);
            dropdown.RegisterCallback<FocusInEvent>(_ => RefreshChoices());
            content.RegisterCallback<PointerEnterEvent>(_ => RefreshChoices());
            dropdown.RegisterValueChangedCallback(change =>
            {
                int selected = labels.IndexOf(change.newValue);
                if (selected < 0 || serialized.targetObject == null)
                {
                    return;
                }
                serialized.Update();
                serialized.FindProperty(path).objectReferenceValue = renderers[selected];
                serialized.ApplyModifiedProperties();
                SkillTreeAssetEditor.SaveEditedAsset((SkillTreeConfigAsset)serialized.targetObject);
                RefreshChoices();
            });
            content.TrackPropertyValue(property, _ => RefreshChoices());
        }

        internal static void AddConnection(VisualElement content, SerializedObject serialized, SerializedProperty connection,
            SkillNodeLayoutConfig layout, SkillTreeGraph graph, string parentPath)
        {
            var foldout = new Foldout { text = "Incoming connection", value = true, name = "connection" };
            content.Add(foldout);
            string path = connection.propertyPath;
            var modeProperty = connection.FindPropertyRelative("_mode");
            var mode = new EnumField("Mode", (SkillTreeConnectionMode)modeProperty.enumValueIndex) { name = "connectionMode" };
            foldout.Add(mode);
            var help = new HelpBox("Auto calculates both handles. Mirrored reflects them around the connection midpoint. Custom edits each handle independently.", HelpBoxMessageType.Info);
            help.AddToClassList("skill-tree-help");
            foldout.Add(help);
            var start = Add(foldout, connection, "_startHandle", "Start handle");
            var end = Add(foldout, connection, "_endHandle", "End handle");
            var useNodeColors = Add(foldout, connection, "_useNodeColors", "Use endpoint colors");
            var startColor = Add(foldout, connection, "_startColor", "Start color");
            var endColor = Add(foldout, connection, "_endColor", "End color");
            Add(foldout, connection, "_width", "Width");
            bool synchronizing = false;

            void UpdateState()
            {
                if (serialized.targetObject == null)
                {
                    return;
                }
                var current = serialized.FindProperty(path);
                if (current == null) return;
                var selected = (SkillTreeConnectionMode)current.FindPropertyRelative("_mode").enumValueIndex;
                mode.SetValueWithoutNotify(selected);
                start.SetEnabled(selected != SkillTreeConnectionMode.Auto);
                end.SetEnabled(selected != SkillTreeConnectionMode.Auto);
                bool endpointColors = current.FindPropertyRelative("_useNodeColors").boolValue;
                startColor.SetEnabled(!endpointColors);
                endColor.SetEnabled(!endpointColors);
            }

            mode.RegisterValueChangedCallback(change =>
            {
                serialized.Update();
                var current = serialized.FindProperty(path);
                if (current == null) return;
                var oldMode = (SkillTreeConnectionMode)current.FindPropertyRelative("_mode").enumValueIndex;
                var next = (SkillTreeConnectionMode)change.newValue;
                var first = current.FindPropertyRelative("_startHandle");
                var last = current.FindPropertyRelative("_endHandle");
                if (oldMode == SkillTreeConnectionMode.Auto && next != SkillTreeConnectionMode.Auto &&
                    graph != null && graph.GetConnectionHandles(layout.NodeId, serialized.FindProperty(parentPath)?.intValue ?? 0,
                        out var firstOffset, out var lastOffset))
                {
                    first.vector2Value = firstOffset;
                    last.vector2Value = lastOffset;
                }
                current.FindPropertyRelative("_mode").enumValueIndex = (int)next;
                if (next == SkillTreeConnectionMode.Mirrored)
                {
                    last.vector2Value = -first.vector2Value;
                }
                serialized.ApplyModifiedProperties();
                SkillTreeAssetEditor.SaveEditedAsset(layout);
                UpdateState();
            });
            foldout.RegisterCallback<SerializedPropertyChangeEvent>(change =>
            {
                if (synchronizing || serialized.targetObject == null)
                {
                    return;
                }
                string changed = change.changedProperty.propertyPath;
                bool startChanged = changed == path + "._startHandle" || changed.StartsWith(path + "._startHandle.", StringComparison.Ordinal);
                bool endChanged = changed == path + "._endHandle" || changed.StartsWith(path + "._endHandle.", StringComparison.Ordinal);
                if (!startChanged && !endChanged)
                {
                    UpdateState();
                    return;
                }
                serialized.Update();
                var current = serialized.FindProperty(path);
                if (current == null) return;
                if ((SkillTreeConnectionMode)current.FindPropertyRelative("_mode").enumValueIndex != SkillTreeConnectionMode.Mirrored)
                {
                    return;
                }
                synchronizing = true;
                var first = current.FindPropertyRelative("_startHandle");
                var last = current.FindPropertyRelative("_endHandle");
                if (startChanged)
                {
                    last.vector2Value = -first.vector2Value;
                }
                else
                {
                    first.vector2Value = -last.vector2Value;
                }
                serialized.ApplyModifiedProperties();
                synchronizing = false;
                SkillTreeAssetEditor.SaveEditedAsset(layout);
            });
            content.TrackPropertyValue(connection, _ => UpdateState());
            UpdateState();
        }
    }
}
