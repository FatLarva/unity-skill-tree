using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.SkillTree.CodeGeneration;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Game.SkillTree.Editor
{
    /// <summary>Maintains source-generator input on disk without importing it or requesting compilation.</summary>
    [InitializeOnLoad]
    public static class SkillTreeEnumManifestWriter
    {
        public const string OutputFolder = "Assets/Game/Generated/SkillTree";
        private const string DataAssemblyName = "Game.SkillTree.Data";
        private static readonly ConcurrentQueue<SkillTreeConfigAsset> Pending = new ConcurrentQueue<SkillTreeConfigAsset>();
        private static readonly Dictionary<SkillTreeConfig, string> Errors = new Dictionary<SkillTreeConfig, string>();
        private static readonly HashSet<SkillTreeConfig> Trees = new HashSet<SkillTreeConfig>();
        private static readonly HashSet<SkillTreeConfig> DirtyTrees = new HashSet<SkillTreeConfig>();
        private static HashSet<string> _assemblyNames;
        private static HashSet<string> _analyzerAssemblyNames;
        private static bool _rescan = true;

        static SkillTreeEnumManifestWriter()
        {
            SkillTreeConfigAsset.Changed += Queue;
            EditorApplication.update += Flush;
            Undo.undoRedoPerformed += ScheduleRescan;
        }

        // OnValidate may run on a loading thread. Do not call any Unity APIs here.
        private static void Queue(SkillTreeConfigAsset asset) => Pending.Enqueue(asset);

        internal static void ScheduleRescan() => _rescan = true;

        public static bool Validate(SkillTreeConfig tree, out string error)
        {
            if (tree == null) { error = "Tree is missing."; return false; }
            if (!tree.EnumGeneration.Enabled) { error = null; return true; }
            var manifest = CreateManifest(tree);
            if (!SkillTreeEnumManifestFormat.Validate(manifest, out error)) return false;
            if (_assemblyNames == null)
            {
                _assemblyNames = new HashSet<string>(StringComparer.Ordinal);
                _analyzerAssemblyNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
                {
                    _assemblyNames.Add(assembly.name);
                    if (assembly.name == DataAssemblyName || Array.Exists(assembly.assemblyReferences,
                            reference => reference.name == DataAssemblyName))
                        _analyzerAssemblyNames.Add(assembly.name);
                }
            }
            if (!_assemblyNames.Contains(manifest.AssemblyName))
            {
                error = "Target assembly '" + manifest.AssemblyName + "' does not exist. Use an asmdef name containing C# scripts, or Assembly-CSharp for scripts without an asmdef.";
                return false;
            }
            if (!_analyzerAssemblyNames.Contains(manifest.AssemblyName))
            {
                error = "Target assembly '" + manifest.AssemblyName + "' must reference " + DataAssemblyName +
                        " in its asmdef so Unity includes the skill tree source generator.";
                return false;
            }
            return true;
        }

        private static void Flush()
        {
            // In particular, never write an additionalfile from an asset import callback: doing so can
            // restart the current refresh and turn each CreateAsset operation into script compilation.
            if (EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.IsAssetImportWorkerProcess()) return;

            bool cleanup = _rescan;
            if (_rescan)
            {
                _rescan = false;
                _assemblyNames = null;
                Trees.Clear();
                foreach (string guid in AssetDatabase.FindAssets("t:SkillTreeConfig", new[] { "Assets" }))
                {
                    var tree = AssetDatabase.LoadAssetAtPath<SkillTreeConfig>(AssetDatabase.GUIDToAssetPath(guid));
                    if (tree == null) continue;
                    Trees.Add(tree);
                    DirtyTrees.Add(tree);
                }
            }
            while (Pending.TryDequeue(out var asset))
            {
                if (asset == null) continue;
                if (asset is SkillTreeConfig tree)
                {
                    if (AssetDatabase.Contains(tree)) { Trees.Add(tree); DirtyTrees.Add(tree); }
                }
                else if (asset is SkillNodeConfig node && node.Owner != null)
                {
                    DirtyTrees.Add(node.Owner);
                }
            }
            foreach (var tree in DirtyTrees)
            {
                if (tree == null || !AssetDatabase.Contains(tree)) continue;
                try { Synchronize(tree); }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
                { Report(tree, "Cannot write enum manifest: " + exception.Message); }
            }
            DirtyTrees.Clear();
            if (cleanup) RemoveOrphanedManifests();
        }

        private static void Synchronize(SkillTreeConfig tree)
        {
            var manifest = CreateManifest(tree);
            if (string.IsNullOrEmpty(manifest.TreeGuid)) return;
            string path = GetManifestPath(manifest.TreeGuid);
            if (!tree.EnumGeneration.Enabled)
            {
                DeleteManifest(path);
                Errors.Remove(tree);
                return;
            }
            if (Validate(tree, out string error)) Errors.Remove(tree);
            else Report(tree, error);
            // Invalid data also replaces the manifest, so the next compile reports the error rather
            // than silently retaining an enum for an older, valid version of the tree.
            WriteIfChanged(path, SkillTreeEnumManifestFormat.Serialize(manifest));
        }

        private static SkillTreeEnumManifest CreateManifest(SkillTreeConfig tree)
        {
            var settings = tree.EnumGeneration;
            var manifest = new SkillTreeEnumManifest
            {
                AssemblyName = settings.AssemblyName,
                Namespace = settings.Namespace,
                TypeName = settings.TypeName,
                TreeGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tree))
            };
            foreach (var node in tree.Nodes)
            {
                string path = node == null ? string.Empty : AssetDatabase.GetAssetPath(node);
                manifest.Nodes.Add(new SkillTreeEnumEntry
                {
                    Id = node == null ? 0 : node.Id,
                    FileName = Path.GetFileNameWithoutExtension(path),
                    AssetGuid = AssetDatabase.AssetPathToGUID(path)
                });
            }
            return manifest;
        }

        private static void Report(SkillTreeConfig tree, string error)
        {
            if (Errors.TryGetValue(tree, out string previous) && previous == error) return;
            Errors[tree] = error;
            Debug.LogError("Skill tree enum '" + tree.name + "': " + error, tree);
        }

        private static void RemoveOrphanedManifests()
        {
            string folder = Path.GetFullPath(OutputFolder);
            if (!Directory.Exists(folder)) return;
            var live = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tree in Trees)
            {
                if (tree == null || !tree.EnumGeneration.Enabled) continue;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tree));
                if (!string.IsNullOrEmpty(guid)) live.Add(guid);
            }
            foreach (string file in Directory.GetFiles(folder, "*" + SkillTreeEnumManifestFormat.FileSuffix))
            {
                string name = Path.GetFileName(file);
                string guid = name.Substring(0, name.Length - SkillTreeEnumManifestFormat.FileSuffix.Length);
                if (Guid.TryParseExact(guid, "N", out _) && !live.Contains(guid)) DeleteManifest(file);
            }
        }

        private static string GetManifestPath(string guid) => Path.GetFullPath(Path.Combine(OutputFolder, guid + SkillTreeEnumManifestFormat.FileSuffix));

        private static void DeleteManifest(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
        }

        private static void WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == content) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Unity ignores files ending in '~'. The destination appears atomically when complete.
            string temporary = path + ".tmp~";
            try
            {
                File.WriteAllText(temporary, content, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    internal sealed class SkillTreeEnumAssetChanges : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (HasAssets(imported) || HasAssets(deleted) || HasAssets(moved) || HasAssets(movedFrom))
                SkillTreeEnumManifestWriter.ScheduleRescan();
        }

        private static bool HasAssets(string[] paths)
        {
            foreach (string path in paths)
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
