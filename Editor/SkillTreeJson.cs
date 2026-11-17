using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.SkillTree.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.SkillTree.Editor
{
    public static class SkillTreeJson
    {
        private const string Format = "skill-tree";
        private const int Version = 2;
        private const string TreeKey = "tree";
        private const string LayoutKey = "layout";
        private const string NodePrefix = "nodes/";
        private const string AppearancePrefix = "appearance/";
        private const string AssetPrefix = "asset/";
        private const string ImportLabel = "Import Skill Tree JSON";

        [MenuItem("CONTEXT/SkillTreeConfig/Export JSON…")]
        private static void ExportMenu(MenuCommand command)
        {
            var tree = (SkillTreeConfig)command.context;
            var path = EditorUtility.SaveFilePanel("Export Skill Tree JSON", "", tree.name, "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                File.WriteAllText(path, Export(tree));
            }
            catch (Exception error)
            {
                EditorUtility.DisplayDialog("Could not export skill tree", error.Message, "OK");
            }
        }

        [MenuItem("CONTEXT/SkillTreeConfig/Import JSON…")]
        private static void ImportMenu(MenuCommand command)
        {
            var path = EditorUtility.OpenFilePanel(ImportLabel, "", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                Import((SkillTreeConfig)command.context, File.ReadAllText(path));
            }
            catch (Exception error)
            {
                EditorUtility.DisplayDialog("Could not import skill tree", error.Message, "OK");
            }
        }

        public static string Export(SkillTreeConfig tree)
        {
            var assets = GetAssets(tree);
            if (!tree.Validate(out string error)) throw new InvalidOperationException(error);

            var keysByAsset = assets.ToDictionary(pair => (Object)pair.Value, pair => pair.Key);
            var snapshots = new JObject();
            foreach (var pair in assets)
            {
                var data = ReadObject(ParseObject(EditorJsonUtility.ToJson(pair.Value)), "MonoBehaviour");
                // Unity's asset identity is owned by the destination project.
                foreach (var property in data.Properties().Where(property => property.Name.StartsWith("m_", StringComparison.Ordinal)).ToArray())
                    property.Remove();

                var references = new JObject();
                VisitReferences(pair.Value, property =>
                {
                    var value = property.objectReferenceValue;
                    if (value != null)
                    {
                        references.Add(property.propertyPath, ReferenceKey(value, keysByAsset));
                    }
                    else if (property.objectReferenceEntityIdValue != EntityId.None)
                    {
                        throw new InvalidOperationException($"'{pair.Value.name}' has a missing reference at {property.propertyPath}.");
                    }
                });
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Value, out string guid, out long localId))
                    throw new InvalidOperationException($"Save '{pair.Value.name}' as a project asset before exporting it.");
                snapshots.Add(pair.Key, new JObject
                {
                    ["assetGuid"] = guid,
                    ["assetLocalId"] = localId,
                    ["data"] = data,
                    ["references"] = references
                });
            }

            return new JObject { ["format"] = Format, ["version"] = Version, ["assets"] = snapshots }
                .ToString(Formatting.Indented);
        }

        /// <summary>Applies a snapshot to this tree's existing assets. GUIDs survive asset renames;
        /// integer node identifiers remain the internal identity and cannot be changed by import.</summary>
        public static void Import(SkillTreeConfig tree, string json, bool saveAssets = true)
        {
            var archive = ParseObject(json);
            if ((string)archive["format"] != Format || (int?)archive["version"] != Version)
                throw new InvalidDataException("This JSON uses an unsupported skill tree format or version.");
            var snapshots = ReadObject(archive, "assets");
            var targets = MatchAssets(tree, snapshots);

            var staging = new Dictionary<string, SkillTreeConfigAsset>(StringComparer.Ordinal);
            var externalReferences = new Dictionary<string, Object>(StringComparer.Ordinal);
            try
            {
                foreach (var pair in targets)
                {
                    var snapshot = ReadObject(snapshots, pair.Key);
                    var data = ReadObject(snapshot, "data");
                    var staged = (SkillTreeConfigAsset)ScriptableObject.CreateInstance(pair.Value.GetType());
                    staged.hideFlags = HideFlags.HideAndDontSave;
                    staging.Add(pair.Key, staged);
                    if (data.Properties().Any(property => property.Name.StartsWith("m_", StringComparison.Ordinal)))
                        throw new InvalidDataException($"'{pair.Key}' contains Unity asset metadata.");
                    var document = new JObject { ["MonoBehaviour"] = data.DeepClone() };
                    EditorJsonUtility.FromJsonOverwrite(document.ToString(Formatting.None), staged);
                    if (SerializationUtility.HasManagedReferencesWithMissingTypes(staged))
                        throw new InvalidDataException($"'{pair.Key}' contains a parameter type missing from this project.");
                    CheckNodeId(pair.Key, staged);
                }

                foreach (var pair in staging)
                    RestoreReferences(pair.Value, ReadObject(ReadObject(snapshots, pair.Key), "references"), staging, externalReferences);

                var stagedTree = (SkillTreeConfig)staging[TreeKey];
                if (stagedTree.Layout != staging[LayoutKey] ||
                    !stagedTree.Nodes.ToHashSet().SetEquals(staging.Values.OfType<SkillNodeConfig>()) ||
                    !stagedTree.Layout.Nodes.ToHashSet().SetEquals(staging.Values.OfType<SkillNodeLayoutConfig>()))
                    throw new InvalidDataException("The snapshot contains incompatible tree or layout references.");
                if (!stagedTree.Validate(out string error)) throw new InvalidDataException(error);

                // Validation uses isolated copies, but persistent assets must point at persistent targets.
                foreach (var pair in staging)
                    RestoreReferences(pair.Value, ReadObject(ReadObject(snapshots, pair.Key), "references"), targets, externalReferences);

                Commit(targets, staging, saveAssets);
            }
            finally
            {
                foreach (var staged in staging.Values) Object.DestroyImmediate(staged);
            }
        }

        private static Dictionary<string, SkillTreeConfigAsset> GetAssets(SkillTreeConfig tree)
        {
            if (tree == null || tree.Layout == null)
                throw new InvalidOperationException("Select a skill tree config with a layout.");

            var assets = new Dictionary<string, SkillTreeConfigAsset>(StringComparer.Ordinal)
            {
                [TreeKey] = tree,
                [LayoutKey] = tree.Layout
            };
            foreach (var node in tree.Nodes)
            {
                if (node == null || node.Id <= 0 || !assets.TryAdd(NodePrefix + node.Id, node))
                    throw new InvalidOperationException("Every gameplay config must have a unique node ID.");
            }
            foreach (var appearance in tree.Layout.Nodes)
            {
                if (appearance == null || appearance.NodeId <= 0 ||
                    !assets.TryAdd(AppearancePrefix + appearance.NodeId, appearance))
                    throw new InvalidOperationException("Every appearance config must have a unique node ID.");
            }
            return assets;
        }

        private static Dictionary<string, SkillTreeConfigAsset> MatchAssets(SkillTreeConfig tree, JObject snapshots)
        {
            var existing = GetAssets(tree);
            if (snapshots.Count != existing.Count || existing.Keys.Any(key => snapshots[key] == null))
                throw new InvalidDataException("The snapshot and destination must have the same node IDs and appearance configs.");

            // Only assets already in the selected tree are candidates. A GUID in an archive
            // must never redirect an import into another tree or a different project asset.
            var byIdentity = new Dictionary<string, SkillTreeConfigAsset>(StringComparer.Ordinal);
            foreach (var asset in existing.Values)
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                    byIdentity.Add(guid + "/" + localId, asset);

            var targets = new Dictionary<string, SkillTreeConfigAsset>(StringComparer.Ordinal);
            var used = new HashSet<SkillTreeConfigAsset>();
            foreach (var pair in existing)
            {
                var snapshot = ReadObject(snapshots, pair.Key);
                string guid = (string)snapshot["assetGuid"];
                var localIdToken = snapshot["assetLocalId"];
                if (guid == null || guid.Length != 32 || guid.Any(character => !Uri.IsHexDigit(character)) ||
                    localIdToken?.Type != JTokenType.Integer || (long)localIdToken == 0)
                    throw new InvalidDataException($"'{pair.Key}' has invalid asset identity metadata.");

                var target = byIdentity.TryGetValue(guid + "/" + (long)localIdToken, out var identified)
                    ? identified : pair.Value;
                if (target != pair.Value)
                    throw new InvalidDataException($"The asset GUID for '{pair.Key}' belongs to a different destination entry. " +
                        "Import cannot change stable node IDs or exchange assets.");
                CheckNodeId(pair.Key, target);
                if (!used.Add(target))
                    throw new InvalidDataException($"Multiple snapshots refer to the same destination asset: '{target.name}'.");
                targets.Add(pair.Key, target);
            }
            return targets;
        }

        private static void CheckNodeId(string key, SkillTreeConfigAsset asset)
        {
            if (asset is SkillNodeConfig node && key != NodePrefix + node.Id ||
                asset is SkillNodeLayoutConfig layout && key != AppearancePrefix + layout.NodeId)
                throw new InvalidDataException($"The node ID in '{key}' does not match its snapshot key.");
        }

        private static string ReferenceKey(Object value, Dictionary<Object, string> keysByAsset)
        {
            if (keysByAsset.TryGetValue(value, out string key)) return key;

            var identifier = GlobalObjectId.GetGlobalObjectIdSlow(value);
            if (!AssetDatabase.Contains(value) || identifier.identifierType == 0)
                throw new InvalidOperationException($"'{value.name}' must be a persistent project asset to be exported.");
            return AssetPrefix + identifier;
        }

        private static void RestoreReferences(SkillTreeConfigAsset asset, JObject references,
            Dictionary<string, SkillTreeConfigAsset> internalAssets, Dictionary<string, Object> externalAssets)
        {
            var remaining = new HashSet<string>(references.Properties().Select(property => property.Name), StringComparer.Ordinal);
            var expected = new Dictionary<string, Object>(StringComparer.Ordinal);
            var serialized = VisitReferences(asset, property =>
            {
                string path = property.propertyPath;
                var token = references[path];
                Object value = null;
                if (token != null)
                {
                    string key = (string)token;
                    if (key == null) throw new InvalidDataException($"Invalid asset reference at {path}.");
                    value = ResolveReference(key, internalAssets, externalAssets);
                    remaining.Remove(path);
                }
                property.objectReferenceValue = value;
                expected.Add(path, value);
            });
            if (remaining.Count > 0)
                throw new InvalidDataException($"'{asset.GetType().Name}' has no reference field at {remaining.First()}.");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var pair in expected)
            {
                if (serialized.FindProperty(pair.Key)?.objectReferenceValue != pair.Value)
                    throw new InvalidDataException($"Incompatible asset reference at {pair.Key}.");
            }
        }

        private static Object ResolveReference(string key, Dictionary<string, SkillTreeConfigAsset> internalAssets,
            Dictionary<string, Object> externalAssets)
        {
            if (internalAssets.TryGetValue(key, out var internalAsset)) return internalAsset;
            if (externalAssets.TryGetValue(key, out var externalAsset)) return externalAsset;
            if (key.StartsWith(AssetPrefix, StringComparison.Ordinal) &&
                GlobalObjectId.TryParse(key[AssetPrefix.Length..], out var identifier))
            {
                var asset = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identifier);
                if (asset != null && AssetDatabase.Contains(asset))
                {
                    externalAssets.Add(key, asset);
                    return asset;
                }
            }
            throw new InvalidDataException($"The snapshot references an asset missing from this project: {key}.");
        }

        private static SerializedObject VisitReferences(SkillTreeConfigAsset asset, Action<SerializedProperty> visit)
        {
            var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator();
            var visited = new HashSet<long>();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.depth == 0 && property.name.StartsWith("m_", StringComparison.Ordinal))
                {
                    enterChildren = false;
                    continue;
                }
                if (property.propertyType == SerializedPropertyType.ManagedReference)
                    enterChildren = visited.Add(property.managedReferenceId);
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    visit(property);
                    enterChildren = false;
                }
            }
            return serialized;
        }

        private static void Commit(Dictionary<string, SkillTreeConfigAsset> targets,
            Dictionary<string, SkillTreeConfigAsset> staging, bool saveAssets)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(ImportLabel);
            Undo.RegisterCompleteObjectUndo(targets.Values.Cast<Object>().ToArray(), ImportLabel);
            try
            {
                foreach (var pair in targets)
                    EditorUtility.CopySerializedManagedFieldsOnly(staging[pair.Key], pair.Value);
                if (!((SkillTreeConfig)targets[TreeKey]).Validate(out string error))
                    throw new InvalidDataException(error);
                Undo.FlushUndoRecordObjects();
                foreach (var asset in targets.Values)
                    SkillTreeAssetEditor.MarkEditedAsset(asset);
                if (saveAssets) AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
            finally
            {
                Undo.IncrementCurrentGroup();
            }
        }

        private static JObject ReadObject(JObject source, string key) =>
            source[key] as JObject ?? throw new InvalidDataException($"The snapshot is missing an object named '{key}'.");

        private static JObject ParseObject(string json)
        {
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
            var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Unexpected data after the JSON object.");
            return result;
        }
    }
}
