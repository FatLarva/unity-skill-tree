using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using UnityEditor;

namespace Game.SkillTree.Editor
{
    internal static class SkillTreeRendererChoices
    {
        internal readonly struct Choice
        {
            public SkillTreeNodeIconRenderer Renderer { get; }
            public string Label { get; }

            public Choice(SkillTreeNodeIconRenderer renderer, string label)
            {
                Renderer = renderer;
                Label = label;
            }
        }

        public static List<Choice> Get(SkillNodeLayoutConfig layout)
        {
            var renderers = new HashSet<SkillTreeNodeIconRenderer>();
            bool hasLibrary = false;
            void AddLibrary(SkillTreeLayoutConfig library)
            {
                if (library == null) return;
                hasLibrary = true;
                foreach (var renderer in library.IconRenderers)
                {
                    if (renderer != null) renderers.Add(renderer);
                }
            }

            if (Selection.activeObject is SkillTreeNodeInspectorContext context && context.Layout == layout)
            {
                AddLibrary(context.Tree?.Layout);
            }
            else
            {
                foreach (string guid in AssetDatabase.FindAssets("t:SkillTreeLayoutConfig"))
                {
                    var library = AssetDatabase.LoadAssetAtPath<SkillTreeLayoutConfig>(AssetDatabase.GUIDToAssetPath(guid));
                    if (library == null) continue;
                    foreach (var node in library.Nodes)
                    {
                        if (node != layout) continue;
                        AddLibrary(library);
                        break;
                    }
                }
            }
            if (!hasLibrary)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:SkillTreeNodeIconRenderer"))
                {
                    var renderer = AssetDatabase.LoadAssetAtPath<SkillTreeNodeIconRenderer>(AssetDatabase.GUIDToAssetPath(guid));
                    if (renderer != null) renderers.Add(renderer);
                }
            }

            var labelCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            string Name(SkillTreeNodeIconRenderer renderer) => string.IsNullOrWhiteSpace(renderer.name) ? renderer.DisplayName : renderer.name;
            foreach (var renderer in renderers)
            {
                string label = Name(renderer);
                labelCounts.TryGetValue(label, out int count);
                labelCounts[label] = count + 1;
            }
            var choices = new List<Choice>(renderers.Count + 1);
            foreach (var renderer in renderers)
            {
                string label = Name(renderer);
                if (labelCounts[label] > 1) label = $"{label} ({AssetDatabase.GetAssetPath(renderer)})";
                choices.Add(new Choice(renderer, label));
            }
            choices.Sort((first, second) => StringComparer.Ordinal.Compare(first.Label, second.Label));
            choices.Insert(0, new Choice(null, "None"));
            return choices;
        }
    }
}
