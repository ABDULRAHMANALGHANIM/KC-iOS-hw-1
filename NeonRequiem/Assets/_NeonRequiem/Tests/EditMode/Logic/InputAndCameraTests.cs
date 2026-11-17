using NeonRequiem.Cameras;
using NeonRequiem.Controls;
using NeonRequiem.UI;
using NUnit.Framework;
using UnityEngine;

namespace NeonRequiem.Tests
{
    public sealed class LookMathTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void TouchScale_UsesPhysicalDistance_WhenDpiIsTrusted()
        {
            var tuning = new LookTuning();
            float perPixel = LookMath.TouchDegreesPerPixel(tuning, 400f, 2400f);
            Assert.That(perPixel, Is.EqualTo(tuning.touchDegreesPerInch / 400f).Within(Tolerance));
        }

        [TestCase(0f)]
        [TestCase(12f)]
        [TestCase(5000f)]
        public void TouchScale_FallsBackToScreenWidth_WhenDpiIsImplausible(float dpi)
        {
            var tuning = new LookTuning();
            float perPixel = LookMath.TouchDegreesPerPixel(tuning, dpi, 2000f);
            Assert.That(perPixel, Is.EqualTo(tuning.touchDegreesPerScreenWidth / 2000f).Within(Tolerance));
        }

        [Test]
        public void TouchScale_IsZero_WhenNothingIsKnown()
        {
            Assert.AreEqual(0f, LookMath.TouchDegreesPerPixel(new LookTuning(), 0f, 0f));
        }

        [Test]
        public void SamePhysicalSwipe_TurnsTheSameAngle_OnDifferentScreens()
        {
            var tuning = new LookTuning();
            // One inch of drag on a 300 dpi phone and on a 264 dpi tablet.
            float phone = 300f * LookMath.TouchDegreesPerPixel(tuning, 300f, 2400f);
            float tablet = 264f * LookMath.TouchDegreesPerPixel(tuning, 264f, 2732f);
            Assert.That(phone, Is.EqualTo(tablet).Within(1e-3f));
        }

        [Test]
        public void StickCurve_KeepsDirection_AndEndpoints()
        {
            Assert.AreEqual(Vector2.zero, LookMath.ApplyStickCurve(Vector2.zero, 1.6f));

            Vector2 full = LookMath.ApplyStickCurve(new Vector2(1f, 0f), 1.6f);
            Assert.That(full.x, Is.EqualTo(1f).Within(Tolerance));

            Vector2 half = LookMath.ApplyStickCurve(new Vector2(0.5f, 0f), 2f);
            Assert.That(half.x, Is.EqualTo(0.25f).Within(Tolerance));

            Vector2 diagonal = LookMath.ApplyStickCurve(new Vector2(0.3f, 0.3f), 1.6f);
            Assert.That(diagonal.x, Is.EqualTo(diagonal.y).Within(Tolerance));
        }

        [Test]
        public void ComputeLookDegrees_CombinesTouchMouseAndStick()
        {
            var tuning = new LookTuning();
            var sample = new PlayerInputSample
            {
                LookTouchPixels = new Vector2(100f, 0f),
                LookMousePixels = new Vector2(50f, 0f),
                LookStick = new Vector2(1f, 0f),
            };

            Vector2 degrees = LookMath.ComputeLookDegrees(sample, tuning, 1f, false, 400f, 2400f, 0.02f);

            float expected = 100f * tuning.touchDegreesPerInch / 400f
                             + 50f * tuning.mouseDegreesPerPixel
                             + tuning.stickYawDegreesPerSecond * 0.02f;
            Assert.That(degrees.x, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(degrees.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ComputeLookDegrees_AppliesSensitivityAndInvert()
        {
            var tuning = new LookTuning();
            var sample = new PlayerInputSample { LookMousePixels = new Vector2(10f, 10f) };

            Vector2 normal = LookMath.ComputeLookDegrees(sample, tuning, 1f, false, 0f, 1920f, 0.016f);
            Vector2 doubled = LookMath.ComputeLookDegrees(sample, tuning, 2f, false, 0f, 1920f, 0.016f);
            Vector2 inverted = LookMath.ComputeLookDegrees(sample, tuning, 1f, true, 0f, 1920f, 0.016f);

            Assert.That(doubled.x, Is.EqualTo(normal.x * 2f).Within(Tolerance));
            Assert.That(inverted.y, Is.EqualTo(-normal.y).Within(Tolerance));
            Assert.That(inverted.x, Is.EqualTo(normal.x).Within(Tolerance));
            Assert.That(normal.y, Is.EqualTo(normal.x * tuning.pointerPitchScale).Within(Tolerance));
        }

        [Test]
        public void ComputeLookDegrees_StickScalesWithTime_PointerDoesNot()
        {
            var tuning = new LookTuning();
            var stick = new PlayerInputSample { LookStick = new Vector2(1f, 0f) };
            var pointer = new PlayerInputSample { LookTouchPixels = new Vector2(40f, 0f) };

            float stickShort = LookMath.ComputeLookDegrees(stick, tuning, 1f, false, 300f, 2000f, 0.01f).x;
            float stickLong = LookMath.ComputeLookDegrees(stick, tuning, 1f, false, 300f, 2000f, 0.02f).x;
            Assert.That(stickLong, Is.EqualTo(stickShort * 2f).Within(1e-3f));

            float pointerShort = LookMath.ComputeLookDegrees(pointer, tuning, 1f, false, 300f, 2000f, 0.01f).x;
            float pointerLong = LookMath.ComputeLookDegrees(pointer, tuning, 1f, false, 300f, 2000f, 0.02f).x;
            Assert.That(pointerLong, Is.EqualTo(pointerShort).Within(Tolerance));
        }
    }

    public sealed class PlayerInputSampleTests
    {
        [Test]
        public void MergeMove_KeepsTheStrongestSource()
        {
            var sample = default(PlayerInputSample);
            sample.MergeMove(new Vector2(0.2f, 0f));
            sample.MergeMove(new Vector2(0f, 0.9f));
            sample.MergeMove(new Vector2(0.1f, 0.1f));
            Assert.AreEqual(new Vector2(0f, 0.9f), sample.Move);
        }
    }

    public sealed class VirtualJoystickMathTests
    {
        [Test]
        public void Centre_IsZero()
        {
            Assert.AreEqual(Vector2.zero, VirtualJoystick.ComputeValue(Vector2.zero, 100f, 0.12f));
        }

        [Test]
        public void InsideDeadZone_IsZero()
        {
            Assert.AreEqual(Vector2.zero, VirtualJoystick.ComputeValue(new Vector2(8f, 0f), 100f, 0.12f));
        }

        [Test]
        public void FullDeflection_IsUnitLength_EvenWhenDraggedPastTheRim()
        {
            Vector2 atRim = VirtualJoystick.ComputeValue(new Vector2(0f, 100f), 100f, 0.12f);
            Vector2 beyond = VirtualJoystick.ComputeValue(new Vector2(0f, 400f), 100f, 0.12f);
            Assert.That(atRim.y, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(beyond.y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void HalfDeflection_IsProportional_AfterDeadZoneRemap()
        {
            Vector2 value = VirtualJoystick.ComputeValue(new Vector2(56f, 0f), 100f, 0.12f);
            Assert.That(value.x, Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void InvalidRadius_IsZero()
        {
            Assert.AreEqual(Vector2.zero, VirtualJoystick.ComputeValue(new Vector2(10f, 0f), 0f, 0.12f));
        }
    }

    public sealed class SafeAreaMathTests
    {
        [Test]
        public void FullScreenSafeArea_GivesFullAnchors()
        {
            Assert.IsTrue(SafeAreaMath.TryComputeAnchors(new Rect(0f, 0f, 2400f, 1080f), 2400, 1080, out Vector2 min, out Vector2 max));
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }

        [Test]
        public void NotchAndHomeIndicator_AreExcluded()
        {
            // Landscape phone: 132 px notch on the left, 63 px home indicator at the bottom.
            Assert.IsTrue(SafeAreaMath.TryComputeAnchors(new Rect(132f, 63f, 2268f, 1017f), 2532, 1170, out Vector2 min, out Vector2 max));
            Assert.That(min.x, Is.EqualTo(132f / 2532f).Within(1e-5f));
            Assert.That(min.y, Is.EqualTo(63f / 1170f).Within(1e-5f));
            Assert.That(max.x, Is.EqualTo(2400f / 2532f).Within(1e-5f));
            Assert.That(max.y, Is.EqualTo(1080f / 1170f).Within(1e-5f));
        }

        [Test]
        public void UnusableInput_FallsBackToFullScreen()
        {
            Assert.IsFalse(SafeAreaMath.TryComputeAnchors(new Rect(0f, 0f, 0f, 0f), 1920, 1080, out Vector2 min, out Vector2 max));
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
            Assert.IsFalse(SafeAreaMath.TryComputeAnchors(new Rect(0f, 0f, 100f, 100f), 0, 0, out min, out max));
        }

        [Test]
        public void SafeAreaLargerThanScreen_IsClamped()
        {
            Assert.IsTrue(SafeAreaMath.TryComputeAnchors(new Rect(-50f, -50f, 3000f, 2000f), 1920, 1080, out Vector2 min, out Vector2 max));
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }
    }

    public sealed class OrbitCameraModelTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void Reset_PlacesCameraBehindTheGivenHeading()
        {
            var tuning = new CameraTuning();
            var model = new OrbitCameraModel(tuning);
            model.Reset(450f);

            Assert.That(model.Yaw, Is.EqualTo(90f).Within(Tolerance));
            Assert.That(model.Pitch, Is.EqualTo(tuning.defaultPitch).Within(Tolerance));
            Assert.That(model.Distance, Is.EqualTo(tuning.distance).Within(Tolerance));
        }

        [Test]
        public void Forward_MatchesUnityEulerConvention()
        {
            var tuning = new CameraTuning { defaultPitch = 0f, minPitch = -89f, maxPitch = 89f };
            var model = new OrbitCameraModel(tuning);

            AssertVector(new Vector3(0f, 0f, 1f), model.Forward);

            model.Reset(90f);
            AssertVector(new Vector3(1f, 0f, 0f), model.Forward);

            // Positive pitch looks down.
            model.Reset(0f);
            model.ApplyLook(new Vector2(0f, -45f), 0.016f);
            Assert.That(model.Pitch, Is.EqualTo(45f).Within(Tolerance));
            Assert.Less(model.Forward.y, 0f);
            Assert.That(model.Forward.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void ApplyLook_TurnsAndWrapsYaw()
        {
            var model = new OrbitCameraModel(new CameraTuning());
            model.Reset(170f);
            model.ApplyLook(new Vector2(20f, 0f), 0.016f);
            Assert.That(model.Yaw, Is.EqualTo(-170f).Within(Tolerance));
        }

        [Test]
        public void ApplyLook_ClampsPitchAtBothLimits()
        {
            var tuning = new CameraTuning();
            var model = new OrbitCameraModel(tuning);

            model.ApplyLook(new Vector2(0f, 500f), 0.016f);
            Assert.That(model.Pitch, Is.EqualTo(tuning.minPitch).Within(Tolerance));

            model.ApplyLook(new Vector2(0f, -500f), 0.016f);
            Assert.That(model.Pitch, Is.EqualTo(tuning.maxPitch).Within(Tolerance));
        }

        [Test]
        public void FollowAssist_WaitsForTheDelayAfterManualLook()
        {
            var tuning = new CameraTuning();
            var model = new OrbitCameraModel(tuning);
            model.Reset(0f);
            model.ApplyLook(new Vector2(1f, 0f), 0.016f);
            float yaw = model.Yaw;

            Assert.IsFalse(model.ApplyFollowAssist(90f, 6f, 0.016f));
            Assert.AreEqual(yaw, model.Yaw);

            // Idle look input for longer than the delay.
            for (float t = 0f; t < tuning.assistDelay + 0.1f; t += 0.05f)
            {
                model.ApplyLook(Vector2.zero, 0.05f);
            }

            Assert.IsTrue(model.ApplyFollowAssist(90f, 6f, 0.016f));
            Assert.Greater(model.Yaw, yaw);
        }

        [Test]
        public void FollowAssist_IgnoresSlowMovement_AndRunningTowardTheCamera()
        {
            var tuning = new CameraTuning();
            var model = new OrbitCameraModel(tuning);
            model.Reset(0f);

            Assert.IsFalse(model.ApplyFollowAssist(90f, tuning.assistMinSpeed * 0.5f, 0.016f), "Below minimum speed.");
            Assert.IsFalse(model.ApplyFollowAssist(175f, 6f, 0.016f), "Running toward the camera must not swing the view around.");
            Assert.IsFalse(model.ApplyFollowAssist(1f, 6f, 0.016f), "Already aligned.");
            Assert.That(model.Yaw, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void FollowAssist_ConvergesWithoutOvershoot()
        {
            var tuning = new CameraTuning();
            var model = new OrbitCameraModel(tuning);
            model.Reset(0f);

            float previous = model.Yaw;
            for (int i = 0; i < 600; i++)
            {
                model.ApplyFollowAssist(80f, 6f, 1f / 60f);
                Assert.GreaterOrEqual(model.Yaw, previous - Tolerance);
                Assert.LessOrEqual(model.Yaw, 80f + Tolerance);
                previous = model.Yaw;
            }

            Assert.That(model.Yaw, Is.EqualTo(80f).Within(2.5f));
        }

        [Test]
        public void Obstruction_PullsCameraInImmediately()
        {
            var tuning = new CameraTuning();
            var model = new OrbitCameraModel(tuning);
            model.UpdateDistance(1.5f, 1f / 60f);
            Assert.That(model.Distance, Is.EqualTo(1.5f).Within(Tolerance));
        }

        [Test]
        public void ClearedObstruction_EasesBackOut()
        {
            var tuning = new CameraTuning();
            var model = new OrbitCameraModel(tuning);
            model.UpdateDistance(1.5f, 1f / 60f);

            model.UpdateDistance(100f, 1f / 60f);
            Assert.Greater(model.Distance, 1.5f);
            Assert.Less(model.Distance, tuning.distance, "Must not pop straight back to full distance.");

            for (int i = 0; i < 300; i++)
            {
                model.UpdateDistance(100f, 1f / 60f);
            }

            Assert.That(model.Distance, Is.EqualTo(tuning.distance).Within(0.01f));
        }

        [Test]
        public void Distance_NeverDropsBelowTheMinimum()
        {
            var tuning = new CameraTuning();
            var model = new OrbitCameraModel(tuning);
            model.UpdateDistance(-3f, 1f / 60f);
            Assert.That(model.Distance, Is.EqualTo(tuning.minDistance).Within(Tolerance));
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance), "z");
        }
    }
}
