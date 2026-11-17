using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using Game.SkillTree.Requirements;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.SkillTree.Tests
{
    public sealed class SkillTreeViewTests
    {
        private const string PrefabPath = "Packages/com.fatlarva.skilltree/Runtime/Defaults/Prefabs/SkillTreeWindow.prefab";
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
                if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
            _objects.Clear();
        }

        [Test]
        public void DefaultPrefabAcceptsContentWhileInactiveAndKeepsAuthoredIconGeometry()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.SetActive(false);
                var window = root.GetComponent<SkillTreeWindow>();
                Assert.That(window.Validate(out string error), Is.True, error);
                var icon = (RectTransform)new SerializedObject(window)
                    .FindProperty("_detailsIcon").objectReferenceValue;
                icon.anchoredPosition = new Vector2(31, -47);
                icon.sizeDelta = new Vector2(65, 39);
                var node = new SkillTreeNode(1)
                {
                    Title = "Inactive preview", IconMode = SkillTreeIconMode.Sprite,
                    MaxLevel = 5, Level = 2, CanPurchase = true
                };

                Assert.DoesNotThrow(() => window.SetContent(new[] { node }, 12));
                Assert.That(window.Graph.SelectedId, Is.EqualTo(1));
                Assert.That(icon.anchoredPosition, Is.EqualTo(new Vector2(31, -47)));
                Assert.That(icon.sizeDelta, Is.EqualTo(new Vector2(65, 39)));
                Assert.That(new SerializedObject(window).FindProperty("_rankPips").arraySize, Is.EqualTo(5));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void InvalidPipReferenceNamesItsArrayIndex()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.SetActive(false);
                var window = root.GetComponent<SkillTreeWindow>();
                var serialized = new SerializedObject(window);
                serialized.FindProperty("_rankPips").GetArrayElementAtIndex(1).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(window.Validate(out string error), Is.False);
                Assert.That(error, Does.Contain("Rank Pips").And.Contain("index 1"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void PresenterKeepsPrivateSnapshotsAndProjectsWalletAndProgressChanges()
        {
            var view = CreateView();
            var config = CreateConfig();
            var levels = new Levels();
            int wallet = 10;
            using var presenter = new SkillTreePresenter(config, view, levels, levels, () => wallet);
            Assert.That(view.Changes.Count, Is.EqualTo(1));
            Assert.That(presenter.GetNodeInfo(1).CanPurchase, Is.True);

            // A graph can edit its own object; it must not corrupt the presenter's previous snapshot.
            view.Changes[0].Position = new Vector2(555, 444);
            Assert.That(presenter.Refresh(), Is.True);
            Assert.That(view.Changes, Is.Empty);

            wallet = 0;
            presenter.Refresh();
            Assert.That(view.Changes.Count, Is.EqualTo(1));
            Assert.That(view.Changes[0].Position, Is.EqualTo(Vector2.zero));
            Assert.That(presenter.GetNodeInfo(1).CanPurchase, Is.False);

            levels.Value = 1;
            presenter.Refresh();
            Assert.That(view.Changes.Count, Is.EqualTo(1));
            Assert.That(presenter.GetNodeInfo(1).Level, Is.EqualTo(1));
            Assert.That(presenter.GetNodeInfo(1).NextLevel, Is.Null);
        }

        [Test]
        public void FailedConstructionDetachesFromCallerOwnedView()
        {
            var view = CreateView();
            var config = Asset<SkillTreeConfig>();
            var levels = new Levels();
            Assert.Throws<InvalidOperationException>(() =>
                new SkillTreePresenter(config, view, levels, levels, () => -1));
            view.EmitClose();
            Assert.That(view.CloseCalls, Is.Zero);
        }

        private RecordingView CreateView()
        {
            var root = new GameObject("View test", typeof(RectTransform));
            _objects.Add(root);
            root.SetActive(false);
            var view = root.AddComponent<RecordingView>();
            view.SetGraph(root.AddComponent<SkillTreeGraph>());
            return view;
        }

        private SkillTreeConfig CreateConfig()
        {
            var node = Asset<SkillNodeConfig>();
            node.Configure(new SkillNodeProperties(1, "Test", "Test", "Description", "Category",
                SkillTreeNodeKind.Root, false, new[] { new SkillLevel(10) }));
            var appearance = Asset<SkillNodeLayoutConfig>();
            appearance.Configure(new SkillTreeNodeLayout(1, 0, Vector2.zero, Color.white,
                iconMode: SkillTreeIconMode.Sprite));
            var layout = Asset<SkillTreeLayoutConfig>();
            layout.Configure(new[] { appearance });
            var config = Asset<SkillTreeConfig>();
            config.ConfigureRequirements(Array.Empty<SkillRequirementType>(), Asset<SkillNodeLevelRequirementResolver>());
            config.Configure(new[] { node }, layout, 10);
            return config;
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            _objects.Add(value);
            return value;
        }

        private sealed class Levels : ISkillLevelReader
        {
            public int Value;
            public int GetLevel(SkillNodeConfig node) => Value;
        }

        public sealed class RecordingView : SkillTreeView
        {
            public readonly List<SkillTreeNode> Changes = new();
            public int CloseCalls;
            public void SetGraph(SkillTreeGraph graph) => _graph = graph;
            public void EmitClose() => RequestClose();
            public override void Close() => CloseCalls++;
            public override void SetFeedback(string message) { }
            public override void SetContent(IEnumerable<SkillTreeNode> nodes, int wallet, bool allowPurchase = true)
                => UpdateContent(nodes, wallet, allowPurchase);
            public override void UpdateContent(IEnumerable<SkillTreeNode> changes, int wallet, bool allowPurchase = true)
            {
                Changes.Clear();
                Changes.AddRange(changes);
            }
        }
    }
}
