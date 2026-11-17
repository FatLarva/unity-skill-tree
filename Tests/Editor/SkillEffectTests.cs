using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.SkillTree.Tests
{
    public sealed class SkillEffectTests
    {
        private readonly List<ScriptableObject> _objects = new();

        [Serializable]
        public sealed class Parameters : ISkillEffectParameters
        {
            public int Amount = 1;
        }

        [Serializable]
        public sealed class OtherParameters : ISkillEffectParameters
        {
            public string Name;
        }

        public abstract class ForeignAssetBase : ScriptableObject { }

        public sealed class ForeignEffectType : ForeignAssetBase, ISkillEffectType
        {
            public Type ParametersType => typeof(Parameters);
            public ISkillEffectParameters CreateParameters() => new Parameters();

            public bool ValidateParameters(ISkillEffectParameters parameters, out string error)
            {
                error = parameters is Parameters { Amount: > 0 } ? null : "Amount must be positive.";
                return error == null;
            }

            public string Describe(ISkillEffectParameters parameters) => ((Parameters)parameters).Amount.ToString();
        }

        public sealed class UnrelatedAsset : ScriptableObject { }

        public sealed class EffectHost : ScriptableObject
        {
            public SkillEffect[] Effects;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _objects) UnityEngine.Object.DestroyImmediate(value);
            _objects.Clear();
        }

        [Test]
        public void DescriptorCanInheritForeignBaseWithoutAdapter()
        {
            var descriptor = Create<ForeignEffectType>();
            var effect = SkillEffect.Create(descriptor, new Parameters { Amount = 10 });

            Assert.That(effect.TypeAsset, Is.SameAs(descriptor));
            Assert.That(effect.Type, Is.SameAs(descriptor));
            Assert.That(effect.Validate(out string error), Is.True, error);
        }

        [Test]
        public void ReferenceRejectsUnrelatedScriptableObject()
        {
            var unrelated = Create<UnrelatedAsset>();
            Assert.Throws<ArgumentException>(() => new SkillEffectTypeReference(unrelated));
        }

        [Test]
        public void EffectRejectsMissingWrongOrInvalidParameters()
        {
            var descriptor = Create<ForeignEffectType>();
            var reference = SkillEffectTypeReference.Create(descriptor);

            Assert.Throws<ArgumentException>(() => new SkillEffect(reference, null));
            Assert.Throws<ArgumentException>(() => new SkillEffect(reference, new OtherParameters()));
            Assert.Throws<ArgumentException>(() => new SkillEffect(reference, new Parameters { Amount = 0 }));
        }

        [Test]
        public void SharedDescriptorCreatesIndependentDefaultData()
        {
            var descriptor = Create<ForeignEffectType>();
            var first = SkillEffect.Create(descriptor);
            var second = SkillEffect.Create(descriptor);

            Assert.That(first.TypeAsset, Is.SameAs(second.TypeAsset));
            Assert.That(first.Parameters, Is.Not.SameAs(second.Parameters));
            ((Parameters)first.Parameters).Amount = 25;
            Assert.That(((Parameters)second.Parameters).Amount, Is.EqualTo(1));
        }

        [Test]
        public void SerializedEntriesKeepSharedTypeAndIndependentInlineValues()
        {
            var descriptor = Create<ForeignEffectType>();
            var original = Create<EffectHost>();
            original.Effects = new[]
            {
                SkillEffect.Create(descriptor, new Parameters { Amount = 10 }),
                SkillEffect.Create(descriptor, new Parameters { Amount = 25 })
            };
            var restored = Create<EffectHost>();

            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(original), restored);

            Assert.That(restored.Effects, Has.Length.EqualTo(2));
            Assert.That(restored.Effects[0].TypeAsset, Is.SameAs(descriptor));
            Assert.That(restored.Effects[1].TypeAsset, Is.SameAs(descriptor));
            Assert.That(((Parameters)restored.Effects[0].Parameters).Amount, Is.EqualTo(10));
            Assert.That(((Parameters)restored.Effects[1].Parameters).Amount, Is.EqualTo(25));
            Assert.That(restored.Effects[0].Parameters, Is.Not.SameAs(restored.Effects[1].Parameters));
            Assert.That(restored.Effects[0].Parameters, Is.Not.SameAs(original.Effects[0].Parameters));
        }

        private T Create<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            _objects.Add(value);
            return value;
        }
    }
}
