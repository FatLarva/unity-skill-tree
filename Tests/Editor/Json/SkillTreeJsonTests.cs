using System;
using System.IO;
using Game.SkillTree.Data;
using Game.SkillTree.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.SkillTree.Tests
{
    public sealed class SkillTreeJsonTests
    {
        private string _folder;
        private SkillTreeConfig _tree;
        private SkillNodeConfig _node;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/SkillTreeJsonTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
            _tree = Asset<SkillTreeConfig>("Tree");
            _node = Asset<SkillNodeConfig>("Node");
            var layout = Asset<SkillTreeLayoutConfig>("Layout");
            var appearance = Asset<SkillNodeLayoutConfig>("Appearance");
            var resolver = Asset<SkillNodeLevelRequirementResolver>("Resolver");
            _tree.AddNode(_node);
            appearance.Configure(new SkillTreeNodeLayout(_node.Id, 0, Vector2.zero, Color.white));
            layout.Configure(new[] { appearance });
            _tree.ConfigureRequirements(Array.Empty<SkillRequirementType>(), resolver);
            _tree.Configure(new[] { _node }, layout, 24);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (_folder != null) AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void RenamedAssetRetainsGuidAndIntegerIdentityDuringImport()
        {
            string json = SkillTreeJson.Export(_tree);
            var archive = JObject.Parse(json);
            var snapshot = archive["assets"]["nodes/1"];
            Assert.That((int)archive["version"], Is.EqualTo(2));
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_node, out string guid, out long localId);
            Assert.That((string)snapshot["assetGuid"], Is.EqualTo(guid));
            Assert.That((long)snapshot["assetLocalId"], Is.EqualTo(localId));
            Assert.That(AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_node), "Renamed Node"), Is.Empty);

            SkillTreeJson.Import(_tree, json, saveAssets: false);

            Assert.That(_node.name, Is.EqualTo("Renamed Node"));
            Assert.That(_node.Id, Is.EqualTo(1));
            Assert.That(_node.Owner, Is.SameAs(_tree));
            Assert.That(_tree.FindNode(1), Is.SameAs(_node));
        }

        [Test]
        public void ForeignAssetGuidsFallBackToDestinationIntegerIds()
        {
            var archive = JObject.Parse(SkillTreeJson.Export(_tree));
            foreach (var property in ((JObject)archive["assets"]).Properties())
                property.Value["assetGuid"] = Guid.NewGuid().ToString("N");
            var serialized = new SerializedObject(_node);
            serialized.FindProperty("_properties._title").stringValue = "Changed";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SkillTreeJson.Import(_tree, archive.ToString(), saveAssets: false);

            Assert.That(_node.Title, Is.Empty);
            Assert.That(_tree.FindNode(1), Is.SameAs(_node));
        }

        [Test]
        public void GuidConflictDoesNotExchangeOrChangeDestinationAssets()
        {
            var archive = JObject.Parse(SkillTreeJson.Export(_tree));
            var snapshots = (JObject)archive["assets"];
            snapshots["nodes/1"]["assetGuid"] = snapshots["appearance/1"]["assetGuid"].DeepClone();
            snapshots["nodes/1"]["assetLocalId"] = snapshots["appearance/1"]["assetLocalId"].DeepClone();

            Assert.Throws<InvalidDataException>(() => SkillTreeJson.Import(_tree, archive.ToString(), false));
            Assert.That(_node.Id, Is.EqualTo(1));
            Assert.That(_node.Owner, Is.SameAs(_tree));
        }

        [Test]
        public void PreviousFormatIsRejectedWithoutMigration()
        {
            var archive = JObject.Parse(SkillTreeJson.Export(_tree));
            archive["version"] = 1;
            Assert.Throws<InvalidDataException>(() => SkillTreeJson.Import(_tree, archive.ToString(), false));
        }

        private T Asset<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            AssetDatabase.CreateAsset(asset, _folder + "/" + name + ".asset");
            return asset;
        }
    }
}
