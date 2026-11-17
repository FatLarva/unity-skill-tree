using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.SkillTree.Tests
{
    public sealed class SkillNodeIdentityTests
    {
        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void NewNodesReceivePositiveStableIdsWithoutReusingRemovedIds()
        {
            var tree = Asset<SkillTreeConfig>();
            var first = Asset<SkillNodeConfig>();
            var removed = Asset<SkillNodeConfig>();
            Assert.That(tree.AddNode(first), Is.EqualTo(1));
            Assert.That(tree.AddNode(removed), Is.EqualTo(2));

            AssignNodes(tree, first);
            tree.SynchronizeNodeIds();
            var added = Asset<SkillNodeConfig>();
            Assert.That(tree.AddNode(added), Is.EqualTo(3));
            Assert.That(first.Id, Is.EqualTo(1));
            Assert.That(first.Owner, Is.SameAs(tree));
            Assert.That(added.Owner, Is.SameAs(tree));
            Assert.That(tree.AddNode(removed), Is.EqualTo(2), "Reattaching the same node preserves its identity.");
        }

        [Test]
        public void RemovingNodeDoesNotAllowItToBeSharedWithAnotherTree()
        {
            var original = Asset<SkillTreeConfig>();
            var other = Asset<SkillTreeConfig>();
            var node = Asset<SkillNodeConfig>();
            original.AddNode(node);
            Assert.Throws<InvalidOperationException>(() => other.AddNode(node));

            AssignNodes(original);
            Assert.Throws<InvalidOperationException>(() => other.AddNode(node));
            Assert.That(node.Owner, Is.SameAs(original));
            Assert.That(other.Nodes, Is.Empty);
        }

        [Test]
        public void ReconfiguringOwnedNodeCannotChangeItsId()
        {
            var tree = Asset<SkillTreeConfig>();
            var node = Asset<SkillNodeConfig>();
            tree.AddNode(node);

            Assert.Throws<InvalidOperationException>(() => node.Configure(Properties(node.Id + 1)));
            Assert.That(node.Id, Is.EqualTo(1));
            node.Configure(Properties(node.Id));
            Assert.That(node.Id, Is.EqualTo(1));
        }

        [Test]
        public void SynchronizationRejectsDuplicateAssetsBeforeBindingAnyNewNode()
        {
            var tree = Asset<SkillTreeConfig>();
            var duplicate = Asset<SkillNodeConfig>();
            var unassigned = Asset<SkillNodeConfig>();
            tree.AddNode(duplicate);
            AssignNodes(tree, unassigned, duplicate, duplicate);

            var error = Assert.Throws<InvalidOperationException>(() => tree.SynchronizeNodeIds());
            Assert.That(error.Message, Does.Contain("listed twice"));
            Assert.That(unassigned.Id, Is.Zero);
            Assert.That(unassigned.Owner, Is.Null);
        }

        [Test]
        public void SynchronizationRejectsDuplicateIdsAndForeignOwners()
        {
            var tree = Asset<SkillTreeConfig>();
            var first = Asset<SkillNodeConfig>();
            var second = Asset<SkillNodeConfig>();
            first.Configure(Properties(42));
            second.Configure(Properties(42));
            AssignNodes(tree, first, second);

            var duplicate = Assert.Throws<InvalidOperationException>(() => tree.SynchronizeNodeIds());
            Assert.That(duplicate.Message, Does.Contain("duplicate ID 42"));
            Assert.That(first.Owner, Is.Null);
            Assert.That(second.Owner, Is.Null);

            var otherTree = Asset<SkillTreeConfig>();
            otherTree.AddNode(first);
            AssignNodes(tree, second, first);
            Assert.Throws<InvalidOperationException>(() => tree.SynchronizeNodeIds());
            Assert.That(second.Owner, Is.Null);
        }

        [Test]
        public void ExhaustedCounterCannotWrapToZeroOrNegativeIds()
        {
            var tree = Asset<SkillTreeConfig>();
            var last = Asset<SkillNodeConfig>();
            last.Configure(Properties(int.MaxValue));
            tree.AddNode(last);
            var next = Asset<SkillNodeConfig>();

            Assert.Throws<InvalidOperationException>(() => tree.AddNode(next));
            Assert.That(next.Id, Is.Zero);
            Assert.That(next.Owner, Is.Null);
            Assert.That(tree.Nodes, Has.Count.EqualTo(1));
        }

        private static SkillNodeProperties Properties(int id) => new(id, "Node", "Node", string.Empty,
            string.Empty, SkillTreeNodeKind.Skill, false, new[] { new SkillLevel(0) });

        private static void AssignNodes(SkillTreeConfig tree, params SkillNodeConfig[] nodes)
        {
            var serialized = new SerializedObject(tree);
            var list = serialized.FindProperty("_nodes");
            list.arraySize = nodes.Length;
            for (int index = 0; index < nodes.Length; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = nodes[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
