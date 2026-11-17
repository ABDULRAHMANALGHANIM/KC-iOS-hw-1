using System;
using NeonRequiem.Core;
using NUnit.Framework;
using UnityEngine;

namespace NeonRequiem.Tests
{
    public sealed class NRMathTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void DampFactor_IsZeroForNonPositiveInputs()
        {
            Assert.AreEqual(0f, NRMath.DampFactor(0f, 0.016f));
            Assert.AreEqual(0f, NRMath.DampFactor(10f, 0f));
            Assert.AreEqual(0f, NRMath.DampFactor(-1f, 0.016f));
        }

        [Test]
        public void Damp_IsFrameRateIndependent()
        {
            // One 1/30 s step must land where two 1/60 s steps land.
            float single = NRMath.Damp(0f, 10f, 8f, 1f / 30f);
            float half = NRMath.Damp(0f, 10f, 8f, 1f / 60f);
            float twice = NRMath.Damp(half, 10f, 8f, 1f / 60f);
            Assert.That(twice, Is.EqualTo(single).Within(1e-3f));
        }

        [Test]
        public void Damp_NeverOvershoots()
        {
            float value = 0f;
            for (int i = 0; i < 600; i++)
            {
                value = NRMath.Damp(value, 5f, 40f, 0.05f);
                Assert.LessOrEqual(value, 5f + Tolerance);
            }

            Assert.That(value, Is.EqualTo(5f).Within(1e-3f));
        }

        [Test]
        public void DampAngle_TakesTheShortWayAround()
        {
            float result = NRMath.DampAngle(170f, -170f, 1000f, 1f);
            Assert.That(Mathf.DeltaAngle(result, -170f), Is.EqualTo(0f).Within(1e-2f));

            float partial = NRMath.DampAngle(170f, -170f, 5f, 0.1f);
            Assert.Greater(partial, 170f, "Must rotate through 180, not back through 0.");
        }

        [TestCase(0f, 0f)]
        [TestCase(180f, 180f)]
        [TestCase(-180f, 180f)]
        [TestCase(190f, -170f)]
        [TestCase(-190f, 170f)]
        [TestCase(540f, 180f)]
        [TestCase(725f, 5f)]
        public void WrapAngle180(float input, float expected)
        {
            Assert.That(NRMath.WrapAngle180(input), Is.EqualTo(expected).Within(1e-3f));
        }

        [Test]
        public void YawVectors_FollowUnityConvention()
        {
            AssertVector(new Vector3(0f, 0f, 1f), NRMath.YawToForward(0f));
            AssertVector(new Vector3(1f, 0f, 0f), NRMath.YawToForward(90f));
            AssertVector(new Vector3(0f, 0f, -1f), NRMath.YawToForward(180f));
            AssertVector(new Vector3(1f, 0f, 0f), NRMath.YawToRight(0f));
            AssertVector(new Vector3(0f, 0f, -1f), NRMath.YawToRight(90f));
        }

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(-120f)]
        [TestCase(179f)]
        public void DirectionToYaw_InvertsYawToForward(float yaw)
        {
            Assert.That(NRMath.DirectionToYaw(NRMath.YawToForward(yaw)), Is.EqualTo(yaw).Within(1e-3f));
        }

        [Test]
        public void Flatten_And_PlanarMagnitude_IgnoreHeight()
        {
            var value = new Vector3(3f, 99f, 4f);
            AssertVector(new Vector3(3f, 0f, 4f), NRMath.Flatten(value));
            Assert.That(NRMath.PlanarMagnitude(value), Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void RadialDeadZone_ZeroInside_FullAtEdge_AndContinuous()
        {
            Assert.AreEqual(Vector2.zero, NRMath.ApplyRadialDeadZone(new Vector2(0.05f, 0.05f), 0.12f));

            Vector2 full = NRMath.ApplyRadialDeadZone(new Vector2(0f, 1f), 0.12f);
            Assert.That(full.y, Is.EqualTo(1f).Within(Tolerance));

            // Just outside the dead zone the output must be tiny, not jump to the raw value.
            Vector2 justOutside = NRMath.ApplyRadialDeadZone(new Vector2(0.13f, 0f), 0.12f);
            Assert.Greater(justOutside.x, 0f);
            Assert.Less(justOutside.x, 0.05f);

            // Direction is preserved.
            Vector2 diagonal = NRMath.ApplyRadialDeadZone(new Vector2(0.5f, 0.5f), 0.12f);
            Assert.That(diagonal.x, Is.EqualTo(diagonal.y).Within(Tolerance));
        }

        [Test]
        public void RadialDeadZone_NeverExceedsUnitLength()
        {
            Vector2 result = NRMath.ApplyRadialDeadZone(new Vector2(3f, 4f), 0.12f);
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void MoveTowards_StepsThenSnaps()
        {
            Vector3 step = NRMath.MoveTowards(Vector3.zero, new Vector3(10f, 0f, 0f), 3f);
            AssertVector(new Vector3(3f, 0f, 0f), step);

            Vector3 snap = NRMath.MoveTowards(new Vector3(9f, 0f, 0f), new Vector3(10f, 0f, 0f), 3f);
            AssertVector(new Vector3(10f, 0f, 0f), snap);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance), "z");
        }
    }

    public sealed class AdaptiveResolutionPolicyTests
    {
        private const float Target = 1f / 60f;
        private const float InBudgetFrame = 0.016f;
        private const float OverBudgetFrame = 0.026f;

        [Test]
        public void StartsAtMaximumScale()
        {
            var policy = new AdaptiveResolutionPolicy(AdaptiveResolutionConfig.Default);
            Assert.AreEqual(1f, policy.Scale);
        }

        [Test]
        public void InvalidConfig_Throws()
        {
            AdaptiveResolutionConfig config = AdaptiveResolutionConfig.Default;
            config.minScale = 0f;
            Assert.Throws<ArgumentException>(() => new AdaptiveResolutionPolicy(config));

            config = AdaptiveResolutionConfig.Default;
            config.maxScale = 0.5f;
            Assert.Throws<ArgumentException>(() => new AdaptiveResolutionPolicy(config));

            config = AdaptiveResolutionConfig.Default;
            config.stepDown = 0f;
            Assert.Throws<ArgumentException>(() => new AdaptiveResolutionPolicy(config));
        }

        [Test]
        public void DoesNothing_WithoutEnoughSamples()
        {
            var policy = new AdaptiveResolutionPolicy(AdaptiveResolutionConfig.Default);
            var tracker = new FrameTimeTracker();
            for (int i = 0; i < 10; i++)
            {
                tracker.AddSample(OverBudgetFrame);
            }

            Assert.IsFalse(policy.Update(tracker, Target, 5f));
            Assert.AreEqual(1f, policy.Scale);
        }

        [Test]
        public void DoesNothing_BeforeTheDecisionInterval()
        {
            var policy = new AdaptiveResolutionPolicy(AdaptiveResolutionConfig.Default);
            var tracker = Filled(OverBudgetFrame);
            Assert.IsFalse(policy.Update(tracker, Target, 0.2f));
            Assert.AreEqual(1f, policy.Scale);
        }

        [Test]
        public void StaysAtMaximum_WhileInBudget()
        {
            var policy = new AdaptiveResolutionPolicy(AdaptiveResolutionConfig.Default);
            for (int second = 0; second < 120; second++)
            {
                Assert.IsFalse(policy.Update(Filled(InBudgetFrame), Target, 1f));
            }

            Assert.AreEqual(1f, policy.Scale);
        }

        [Test]
        public void StepsDown_WhenOverBudget_AndStopsAtMinimum()
        {
            AdaptiveResolutionConfig config = AdaptiveResolutionConfig.Default;
            var policy = new AdaptiveResolutionPolicy(config);

            Assert.IsTrue(policy.Update(Filled(OverBudgetFrame), Target, 1f));
            Assert.That(policy.Scale, Is.EqualTo(0.9f).Within(1e-5f));

            for (int i = 0; i < 10; i++)
            {
                policy.Update(Filled(OverBudgetFrame), Target, 1f);
            }

            Assert.That(policy.Scale, Is.EqualTo(config.minScale).Within(1e-5f));
            Assert.IsFalse(policy.Update(Filled(OverBudgetFrame), Target, 1f), "No change is reported once the floor is reached.");
        }

        [Test]
        public void Update_ClearsTheTracker_WhenTheScaleChanges()
        {
            var policy = new AdaptiveResolutionPolicy(AdaptiveResolutionConfig.Default);
            FrameTimeTracker tracker = Filled(OverBudgetFrame);
            Assert.IsTrue(policy.Update(tracker, Target, 1f));
            Assert.AreEqual(0, tracker.SampleCount);
        }

        [Test]
        public void ProbesUpward_AfterSustainedInBudgetPeriod()
        {
            AdaptiveResolutionConfig config = AdaptiveResolutionConfig.Default;
            var policy = new AdaptiveResolutionPolicy(config);
            policy.Update(Filled(OverBudgetFrame), Target, 1f);
            Assert.That(policy.Scale, Is.EqualTo(0.9f).Within(1e-5f));

            int seconds = 0;
            while (!policy.Update(Filled(InBudgetFrame), Target, 1f))
            {
                seconds++;
                Assert.Less(seconds, 60, "The policy never probed upward.");
            }

            Assert.That(policy.Scale, Is.EqualTo(0.95f).Within(1e-5f));
            Assert.GreaterOrEqual(seconds + 1, (int)config.initialProbeDelay);
        }

        [Test]
        public void FailedProbe_DoublesTheWait_SuccessfulProbeResetsIt()
        {
            AdaptiveResolutionConfig config = AdaptiveResolutionConfig.Default;
            var policy = new AdaptiveResolutionPolicy(config);

            policy.Update(Filled(OverBudgetFrame), Target, 1f);          // 1.0 -> 0.9
            RunInBudgetUntilChange(policy);                               // probe: 0.9 -> 0.95
            policy.Update(Filled(OverBudgetFrame), Target, 1f);          // probe failed: back to 0.85
            Assert.That(policy.Scale, Is.EqualTo(0.85f).Within(1e-5f));
            Assert.That(policy.CurrentProbeDelay, Is.EqualTo(config.initialProbeDelay * 2f).Within(1e-5f));

            int waited = RunInBudgetUntilChange(policy);                  // second probe takes twice as long
            Assert.GreaterOrEqual(waited, (int)(config.initialProbeDelay * 2f));

            RunInBudgetUntilChange(policy);                               // previous probe held: delay resets
            Assert.That(policy.CurrentProbeDelay, Is.EqualTo(config.initialProbeDelay).Within(1e-5f));
        }

        [Test]
        public void ProbeDelay_IsCapped()
        {
            AdaptiveResolutionConfig config = AdaptiveResolutionConfig.Default;
            var policy = new AdaptiveResolutionPolicy(config);
            policy.Update(Filled(OverBudgetFrame), Target, 1f);
            for (int i = 0; i < 12; i++)
            {
                RunInBudgetUntilChange(policy);
                policy.Update(Filled(OverBudgetFrame), Target, 1f);
            }

            Assert.LessOrEqual(policy.CurrentProbeDelay, config.maxProbeDelay);
        }

        [Test]
        public void Reset_ReturnsToMaximum()
        {
            var policy = new AdaptiveResolutionPolicy(AdaptiveResolutionConfig.Default);
            policy.Update(Filled(OverBudgetFrame), Target, 1f);
            policy.Reset();
            Assert.AreEqual(1f, policy.Scale);
        }

        [Test]
        public void InvalidTimeInputs_AreIgnored()
        {
            var policy = new AdaptiveResolutionPolicy(AdaptiveResolutionConfig.Default);
            Assert.IsFalse(policy.Update(Filled(OverBudgetFrame), 0f, 1f));
            Assert.IsFalse(policy.Update(Filled(OverBudgetFrame), Target, 0f));
            Assert.IsFalse(policy.Update(Filled(OverBudgetFrame), Target, float.NaN));
            Assert.Throws<ArgumentNullException>(() => policy.Update(null, Target, 1f));
            Assert.AreEqual(1f, policy.Scale);
        }

        private static FrameTimeTracker Filled(float frameSeconds)
        {
            var tracker = new FrameTimeTracker();
            for (int i = 0; i < 60; i++)
            {
                tracker.AddSample(frameSeconds);
            }

            return tracker;
        }

        private static int RunInBudgetUntilChange(AdaptiveResolutionPolicy policy)
        {
            int seconds = 0;
            while (!policy.Update(Filled(InBudgetFrame), Target, 1f))
            {
                seconds++;
                Assert.Less(seconds, 600, "The policy never changed scale.");
            }

            return seconds + 1;
        }
    }
}
