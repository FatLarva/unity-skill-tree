using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.SkillTree.Data;
using Game.SkillTree.Requirements;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.SkillTree.Tests
{
    public sealed class RequirementResolverTests
    {
        private readonly List<ScriptableObject> _assets = new();
        private static int _bindingCalls;

        [SetUp]
        public void SetUp() => _bindingCalls = 0;

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void EditorValidationDoesNotConstructRuntimeChecks()
        {
            var factory = Asset<TestResolver>();
            var type = Asset<TestRequirement>();

            Assert.That(factory.ValidateBindings(out var error), Is.True, error);
            Assert.That(factory.Supports(type), Is.True);
            Assert.That(factory.ValidateRequirementType(type, out error), Is.True, error);
            Assert.That(_bindingCalls, Is.Zero);
        }

        [Test]
        public void OneAssetCreatesIndependentChecksOnceForEachSession()
        {
            var factory = Asset<TestResolver>();
            var requirement = new SkillRequirement(Asset<TestRequirement>(), new TestParameters(5));
            var first = new TestContext { Value = 2 };
            var second = new TestContext { Value = 8 };
            var firstResolver = factory.Create(first);
            var secondResolver = factory.Create(second);

            Assert.That(firstResolver.Resolve(requirement).IsMet, Is.False);
            Assert.That(secondResolver.Resolve(requirement).IsMet, Is.True);
            first.Value = 6;
            Assert.That(firstResolver.Resolve(requirement).IsMet, Is.True);
            Assert.That(secondResolver.Resolve(requirement).IsMet, Is.True);
            Assert.That(_bindingCalls, Is.EqualTo(2));
            Assert.That(first.CreatedChecks, Is.EqualTo(1));
            Assert.That(second.CreatedChecks, Is.EqualTo(1));
        }

        [Test]
        public void MetadataRejectsDuplicateTypesAndMismatchedParameterSchema()
        {
            Assert.That(Asset<DuplicateResolver>().ValidateBindings(out var error), Is.False);
            Assert.That(error, Does.Contain(typeof(TestRequirement).FullName).And.Contain("more than once"));

            var mismatched = Asset<MismatchedResolver>();
            Assert.That(mismatched.ValidateRequirementType(Asset<TestRequirement>(), out error), Is.False);
            Assert.That(error, Does.Contain(typeof(OtherParameters).FullName));
            Assert.That(_bindingCalls, Is.Zero);
        }

        [Test]
        public void FactoryRejectsWrongContextAndNullCallbacksWithTypeDiagnostics()
        {
            var wrongContext = Assert.Throws<InvalidOperationException>(() => Asset<TestResolver>().Create(new object()));
            Assert.That(wrongContext.Message, Does.Contain(typeof(TestContext).FullName));

            Assert.That(Asset<NullFactoryResolver>().ValidateBindings(out var error), Is.False);
            Assert.That(error, Does.Contain(typeof(TestRequirement).FullName).And.Contain("null"));

            var nullCallback = Assert.Throws<InvalidOperationException>(() =>
                Asset<NullCallbackResolver>().Create(new TestContext()));
            Assert.That(nullCallback.Message, Does.Contain(typeof(TestRequirement).FullName).And.Contain("null callback"));
        }

        [Test]
        public void RuntimeDispatchChecksParametersAndUsesExactRequirementTypes()
        {
            var factory = Asset<TestResolver>();
            var resolver = factory.Create(new TestContext { Value = 10 });
            var type = Asset<TestRequirement>();
            Assert.Throws<ArgumentException>(() => resolver.Resolve(new SkillRequirement(type, new OtherParameters())));
            Assert.Throws<ArgumentException>(() => resolver.Resolve(new SkillRequirement(type, new TestParameters(-1))));

            var derived = Asset<DerivedRequirement>();
            Assert.That(factory.Supports(derived), Is.False);
            Assert.That(resolver.Resolve(new SkillRequirement(derived, new TestParameters(1))).Handled, Is.False);
        }

        [Test]
        public void ChainStopsAtHandledFailureAndOnlySkipsUnhandled()
        {
            var requirement = new SkillRequirement(Asset<TestRequirement>(), new TestParameters(1));
            var unknown = new FixedResolver(SkillRequirementResolution.Unhandled);
            var unmet = new FixedResolver(SkillRequirementResolution.Unmet);
            var met = new FixedResolver(SkillRequirementResolution.Met);
            var chain = new SkillRequirementResolverChain(unknown, unmet, met);

            var result = chain.Resolve(requirement);
            Assert.That(result.Handled, Is.True);
            Assert.That(result.IsMet, Is.False);
            Assert.That(unknown.Calls, Is.EqualTo(1));
            Assert.That(unmet.Calls, Is.EqualTo(1));
            Assert.That(met.Calls, Is.Zero);
        }

        [Test]
        public void MissingFactoryLogsOnceAndRecoveryAllowsASecondErrorTransition()
        {
            var tree = Asset<SkillTreeConfig>();
            tree.name = "Resolver diagnostics";
            LogAssert.Expect(LogType.Error, new Regex("Skill tree 'Resolver diagnostics':.*"));
            var evaluator = new SkillRequirementEvaluator(tree, new TestContext());
            Assert.That(evaluator.Validate(out _), Is.False);
            Assert.That(evaluator.Validate(out _), Is.False);
            LogAssert.NoUnexpectedReceived();

            SetResolver(tree, Asset<TestResolver>());
            Assert.That(evaluator.Validate(out var error), Is.True, error);
            Assert.That(_bindingCalls, Is.EqualTo(1));
            Assert.That(evaluator.Validate(out _), Is.True);
            Assert.That(_bindingCalls, Is.EqualTo(1));

            SetResolver(tree, null);
            LogAssert.Expect(LogType.Error, new Regex("Skill tree 'Resolver diagnostics':.*"));
            Assert.That(evaluator.Validate(out _), Is.False);
            Assert.That(evaluator.Validate(out _), Is.False);
        }

        [Test]
        public void RuntimeFailureIsRetriedAndDoesNotDisableOtherRequirements()
        {
            var tree = Asset<SkillTreeConfig>();
            tree.name = "Runtime diagnostics";
            var type = Asset<TestRequirement>();
            tree.ConfigureRequirements(new[] { type }, Asset<TestResolver>());
            var context = new TestContext { Value = 10, ThrowOnCheck = true };
            var evaluator = new SkillRequirementEvaluator(tree, context);
            var level = new SkillLevel(0, new[] { new SkillRequirement(type, new TestParameters(5)) });

            LogAssert.Expect(LogType.Error, "Skill tree 'Runtime diagnostics': Runtime dependency unavailable.");
            Assert.That(evaluator.MeetsRequirements(level), Is.False);
            Assert.That(evaluator.MeetsRequirements(level), Is.False);
            Assert.That(evaluator.Validate(out var error), Is.True, error);
            Assert.That(evaluator.MeetsRequirements(new SkillLevel(0)), Is.True);
            LogAssert.NoUnexpectedReceived();

            context.ThrowOnCheck = false;
            Assert.That(evaluator.MeetsRequirements(level), Is.True);
            Assert.That(_bindingCalls, Is.EqualTo(1), "Recovery must reuse this session's checks.");

            context.ThrowOnCheck = true;
            LogAssert.Expect(LogType.Error, "Skill tree 'Runtime diagnostics': Runtime dependency unavailable.");
            Assert.That(evaluator.MeetsRequirements(level), Is.False);
            Assert.That(evaluator.MeetsRequirements(level), Is.False);
        }

        [Test]
        public void UnhandledRequirementDoesNotPoisonSubsequentEvaluation()
        {
            var tree = Asset<SkillTreeConfig>();
            tree.name = "Unhandled diagnostics";
            var type = Asset<TestRequirement>();
            tree.ConfigureRequirements(new[] { type }, Asset<TestResolver>());
            var evaluator = new SkillRequirementEvaluator(tree, new TestContext { Value = 10 });
            var unsupported = Asset<DerivedRequirement>();
            unsupported.name = "Unsupported";
            var level = new SkillLevel(0, new[] { new SkillRequirement(unsupported, new TestParameters(5)) });

            LogAssert.Expect(LogType.Error,
                "Skill tree 'Unhandled diagnostics' uses 'Unsupported', but the bound resolver did not handle it.");
            Assert.That(evaluator.MeetsRequirements(level), Is.False);
            Assert.That(evaluator.MeetsRequirements(level), Is.False);
            Assert.That(evaluator.MeetsRequirements(new SkillLevel(0,
                new[] { new SkillRequirement(type, new TestParameters(5)) })), Is.True);
            Assert.That(evaluator.MeetsRequirements(level), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        private static void SetResolver(SkillTreeConfig tree, SkillRequirementResolver resolver)
        {
            var serialized = new SerializedObject(tree);
            serialized.FindProperty("_requirementResolver").objectReferenceValue = resolver;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            tree.NotifyChanged();
        }

        public sealed class TestContext
        {
            public int Value;
            public int CreatedChecks;
            public bool ThrowOnCheck;
        }

        [Serializable]
        public sealed class TestParameters : SkillRequirementParameters
        {
            public int Minimum;
            public TestParameters(int minimum = 1) => Minimum = minimum;
        }

        [Serializable]
        public sealed class OtherParameters : SkillRequirementParameters { }

        public class TestRequirement : SkillRequirementType
        {
            public override SkillRequirementParameters CreateParameters() => new TestParameters();
            public override bool AreParametersValid(SkillRequirementParameters parameters) =>
                parameters is TestParameters { Minimum: >= 0 };
        }

        public sealed class DerivedRequirement : TestRequirement { }

        public sealed class TestResolver : SkillRequirementResolver<TestContext>
        {
            protected override void Configure(RequirementBindings<TestContext> bindings) =>
                bindings.Add<TestRequirement, TestParameters>(context =>
                {
                    _bindingCalls++;
                    context.CreatedChecks++;
                    return parameters => context.ThrowOnCheck
                        ? throw new InvalidOperationException("Runtime dependency unavailable.")
                        : context.Value >= parameters.Minimum;
                });
        }

        public sealed class DuplicateResolver : SkillRequirementResolver<TestContext>
        {
            protected override void Configure(RequirementBindings<TestContext> bindings)
            {
                bindings.Add<TestRequirement, TestParameters>(context => parameters => true);
                bindings.Add<TestRequirement, TestParameters>(context => parameters => true);
            }
        }

        public sealed class MismatchedResolver : SkillRequirementResolver<TestContext>
        {
            protected override void Configure(RequirementBindings<TestContext> bindings) =>
                bindings.Add<TestRequirement, OtherParameters>(context => parameters => true);
        }

        public sealed class NullFactoryResolver : SkillRequirementResolver<TestContext>
        {
            protected override void Configure(RequirementBindings<TestContext> bindings) =>
                bindings.Add<TestRequirement, TestParameters>(null);
        }

        public sealed class NullCallbackResolver : SkillRequirementResolver<TestContext>
        {
            protected override void Configure(RequirementBindings<TestContext> bindings) =>
                bindings.Add<TestRequirement, TestParameters>(_ => null);
        }

        private sealed class FixedResolver : ISkillRequirementResolver
        {
            private readonly SkillRequirementResolution _result;
            public int Calls { get; private set; }
            public FixedResolver(SkillRequirementResolution result) => _result = result;
            public SkillRequirementResolution Resolve(SkillRequirement requirement)
            {
                Calls++;
                return _result;
            }
        }
    }
}
