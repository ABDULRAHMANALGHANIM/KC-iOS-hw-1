using System;
using System.Collections.Generic;
using System.Reflection;
using NeonRequiem.Core;
using NUnit.Framework;

namespace NeonRequiem.Tests
{
    public sealed class GameStateMachineTests
    {
        [Test]
        public void StartsInBoot()
        {
            Assert.AreEqual(GameFlowState.Boot, new GameStateMachine().Current);
        }

        [TestCase(GameFlowState.Boot, GameFlowState.Loading, true)]
        [TestCase(GameFlowState.Boot, GameFlowState.Playing, false)]
        [TestCase(GameFlowState.Boot, GameFlowState.Paused, false)]
        [TestCase(GameFlowState.Loading, GameFlowState.Playing, true)]
        [TestCase(GameFlowState.Loading, GameFlowState.Paused, false)]
        [TestCase(GameFlowState.Loading, GameFlowState.Boot, false)]
        [TestCase(GameFlowState.Playing, GameFlowState.Paused, true)]
        [TestCase(GameFlowState.Playing, GameFlowState.Loading, true)]
        [TestCase(GameFlowState.Playing, GameFlowState.Boot, false)]
        [TestCase(GameFlowState.Paused, GameFlowState.Playing, true)]
        [TestCase(GameFlowState.Paused, GameFlowState.Loading, true)]
        [TestCase(GameFlowState.Paused, GameFlowState.Boot, false)]
        [TestCase(GameFlowState.Playing, GameFlowState.Playing, false)]
        public void TransitionTable(GameFlowState from, GameFlowState to, bool allowed)
        {
            Assert.AreEqual(allowed, GameStateMachine.IsTransitionAllowed(from, to));
        }

        [Test]
        public void TryTransition_ChangesStateAndRaisesEvent()
        {
            var machine = new GameStateMachine();
            var log = new List<string>();
            machine.Changed += (previous, current) => log.Add(previous + ">" + current);

            Assert.IsTrue(machine.TryTransition(GameFlowState.Loading));
            Assert.IsTrue(machine.TryTransition(GameFlowState.Playing));
            Assert.IsTrue(machine.TryTransition(GameFlowState.Paused));
            Assert.IsTrue(machine.TryTransition(GameFlowState.Playing));

            Assert.AreEqual(GameFlowState.Playing, machine.Current);
            CollectionAssert.AreEqual(
                new[] { "Boot>Loading", "Loading>Playing", "Playing>Paused", "Paused>Playing" }, log);
        }

        [Test]
        public void TryTransition_Rejected_LeavesStateAndStaysSilent()
        {
            var machine = new GameStateMachine();
            int events = 0;
            machine.Changed += (previous, current) => events++;

            Assert.IsFalse(machine.TryTransition(GameFlowState.Paused));
            Assert.AreEqual(GameFlowState.Boot, machine.Current);
            Assert.AreEqual(0, events);
        }
    }

    public sealed class GameSettingsTests
    {
        [Test]
        public void Sanitize_ClampsOutOfRangeValues()
        {
            var settings = new GameSettings
            {
                lookSensitivity = 99f,
                touchControlScale = 0.01f,
                touchControlOpacity = -4f,
                graphicsPreset = (GraphicsPreset)42,
                frameRateTarget = (FrameRateTarget)45,
            };

            settings.Sanitize();

            Assert.AreEqual(GameSettings.MaxLookSensitivity, settings.lookSensitivity);
            Assert.AreEqual(GameSettings.MinTouchControlScale, settings.touchControlScale);
            Assert.AreEqual(GameSettings.MinTouchControlOpacity, settings.touchControlOpacity);
            Assert.AreEqual(GraphicsPreset.Balanced, settings.graphicsPreset);
            Assert.AreEqual(FrameRateTarget.Fps60, settings.frameRateTarget);
        }

        [Test]
        public void Sanitize_ReplacesNonFiniteValues()
        {
            var settings = new GameSettings
            {
                lookSensitivity = float.NaN,
                touchControlScale = float.PositiveInfinity,
            };

            settings.Sanitize();

            Assert.AreEqual(1f, settings.lookSensitivity);
            Assert.AreEqual(1f, settings.touchControlScale);
        }

        [Test]
        public void Clone_CopiesEveryField()
        {
            var original = new GameSettings
            {
                lookSensitivity = 1.7f,
                invertLookY = true,
                leftHandedLayout = true,
                floatingJoystick = false,
                touchControlScale = 1.3f,
                touchControlOpacity = 0.5f,
                cameraAssist = false,
                reduceCameraMotion = true,
                graphicsPreset = GraphicsPreset.Quality,
                frameRateTarget = FrameRateTarget.Fps30,
                adaptiveResolution = false,
                showPerformanceOverlay = false,
            };

            GameSettings copy = original.Clone();

            // Reflection guards against a field being added to GameSettings but forgotten in CopyFrom.
            foreach (FieldInfo field in typeof(GameSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(field.GetValue(original), field.GetValue(copy), "Field not copied: " + field.Name);
            }
        }
    }

    public sealed class SettingsManagerTests
    {
        [Test]
        public void Load_OnEmptyStore_UsesDefaults()
        {
            var manager = new SettingsManager(new InMemorySettingsStore());
            manager.Load();

            Assert.IsFalse(manager.HasStoredSettings);
            Assert.AreEqual(1f, manager.Current.lookSensitivity);
            Assert.AreEqual(GraphicsPreset.Balanced, manager.Current.graphicsPreset);
            Assert.IsTrue(manager.Current.floatingJoystick);
        }

        [Test]
        public void Load_OnEmptyStore_UsesProvidedDefaults()
        {
            var defaults = new GameSettings { graphicsPreset = GraphicsPreset.Performance, showPerformanceOverlay = false };
            var manager = new SettingsManager(new InMemorySettingsStore(), defaults);
            manager.Load();

            Assert.AreEqual(GraphicsPreset.Performance, manager.Current.graphicsPreset);
            Assert.IsFalse(manager.Current.showPerformanceOverlay);
        }

        [Test]
        public void Modify_PersistsAndNotifiesOnce()
        {
            var store = new InMemorySettingsStore();
            var manager = new SettingsManager(store);
            manager.Load();
            int notifications = 0;
            manager.Changed += settings => notifications++;

            manager.Modify(s =>
            {
                s.lookSensitivity = 2.2f;
                s.leftHandedLayout = true;
                s.graphicsPreset = GraphicsPreset.Quality;
            });

            Assert.AreEqual(1, notifications);
            Assert.AreEqual(1, store.FlushCount);

            var reloaded = new SettingsManager(store);
            reloaded.Load();
            Assert.IsTrue(reloaded.HasStoredSettings);
            Assert.That(reloaded.Current.lookSensitivity, Is.EqualTo(2.2f).Within(1e-5f));
            Assert.IsTrue(reloaded.Current.leftHandedLayout);
            Assert.AreEqual(GraphicsPreset.Quality, reloaded.Current.graphicsPreset);
        }

        [Test]
        public void EveryField_RoundTripsThroughTheStore()
        {
            var store = new InMemorySettingsStore();
            var manager = new SettingsManager(store);
            manager.Load();
            manager.Modify(s =>
            {
                s.lookSensitivity = 0.6f;
                s.invertLookY = true;
                s.leftHandedLayout = true;
                s.floatingJoystick = false;
                s.touchControlScale = 1.4f;
                s.touchControlOpacity = 0.45f;
                s.cameraAssist = false;
                s.reduceCameraMotion = true;
                s.graphicsPreset = GraphicsPreset.Performance;
                s.frameRateTarget = FrameRateTarget.Fps30;
                s.adaptiveResolution = false;
                s.showPerformanceOverlay = false;
            });

            var reloaded = new SettingsManager(store);
            reloaded.Load();

            foreach (FieldInfo field in typeof(GameSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(
                    field.GetValue(manager.Current), field.GetValue(reloaded.Current),
                    "Field not persisted: " + field.Name);
            }
        }

        [Test]
        public void Modify_SanitizesValues()
        {
            var manager = new SettingsManager(new InMemorySettingsStore());
            manager.Load();
            manager.Modify(s => s.lookSensitivity = 50f);
            Assert.AreEqual(GameSettings.MaxLookSensitivity, manager.Current.lookSensitivity);
        }

        [Test]
        public void Load_SanitizesCorruptStoredValues()
        {
            var store = new InMemorySettingsStore();
            store.SetInt("nr.settings.schema", SettingsManager.SchemaVersion);
            store.SetFloat("nr.settings.lookSensitivity", -7f);
            store.SetInt("nr.settings.graphicsPreset", 99);
            store.SetInt("nr.settings.frameRate", 17);

            var manager = new SettingsManager(store);
            manager.Load();

            Assert.AreEqual(GameSettings.MinLookSensitivity, manager.Current.lookSensitivity);
            Assert.AreEqual(GraphicsPreset.Balanced, manager.Current.graphicsPreset);
            Assert.AreEqual(FrameRateTarget.Fps60, manager.Current.frameRateTarget);
        }

        [Test]
        public void Load_FromNewerSchema_StillReadsKnownKeys()
        {
            var store = new InMemorySettingsStore();
            store.SetInt("nr.settings.schema", SettingsManager.SchemaVersion + 5);
            store.SetFloat("nr.settings.lookSensitivity", 1.5f);

            var sink = new CapturingLogSink();
            ILogSink previous = NRLog.Sink;
            NRLog.Sink = sink;
            try
            {
                var manager = new SettingsManager(store);
                manager.Load();
                Assert.That(manager.Current.lookSensitivity, Is.EqualTo(1.5f).Within(1e-5f));
                Assert.AreEqual(1, sink.Count(LogLevel.Warning));
            }
            finally
            {
                NRLog.Sink = previous;
            }
        }

        [Test]
        public void ResetToDefaults_RestoresAndPersists()
        {
            var store = new InMemorySettingsStore();
            var manager = new SettingsManager(store);
            manager.Load();
            manager.Modify(s => s.invertLookY = true);

            manager.ResetToDefaults();

            Assert.IsFalse(manager.Current.invertLookY);
            var reloaded = new SettingsManager(store);
            reloaded.Load();
            Assert.IsFalse(reloaded.Current.invertLookY);
        }

        [Test]
        public void NullArguments_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new SettingsManager(null));
            var manager = new SettingsManager(new InMemorySettingsStore());
            Assert.Throws<ArgumentNullException>(() => manager.Modify(null));
        }
    }

    public sealed class LocalizationTests
    {
        [Test]
        public void EveryDeclaredKey_HasAnEnglishString()
        {
            foreach (FieldInfo field in typeof(LocKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var key = (string)field.GetValue(null);
                Assert.IsTrue(Loc.HasKey(key), "No English text for key: " + key);
                Assert.IsNotEmpty(Loc.Get(key));
            }
        }

        [Test]
        public void MissingKey_ReturnsTheKeyItself()
        {
            Assert.AreEqual("does.not.exist", Loc.Get("does.not.exist"));
            Assert.AreEqual(string.Empty, Loc.Get(null));
            Assert.IsFalse(Loc.HasKey(null));
        }
    }

    public sealed class DeviceTierHeuristicTests
    {
        [TestCase(false, 8192, 8, GraphicsPreset.Quality)]
        [TestCase(true, 3072, 8, GraphicsPreset.Performance)]
        [TestCase(true, 4096, 8, GraphicsPreset.Performance)]
        [TestCase(true, 8192, 4, GraphicsPreset.Performance)]
        [TestCase(true, 8192, 8, GraphicsPreset.Balanced)]
        [TestCase(true, 0, 0, GraphicsPreset.Balanced)]
        [TestCase(true, 16384, 8, GraphicsPreset.Balanced)]
        public void SuggestPreset(bool mobile, int memoryMb, int cores, GraphicsPreset expected)
        {
            Assert.AreEqual(expected, DeviceTierHeuristic.SuggestPreset(mobile, memoryMb, cores));
        }
    }

    /// <summary>Log sink that records entries instead of calling into the engine.</summary>
    public sealed class CapturingLogSink : ILogSink
    {
        private readonly List<LogLevel> m_Levels = new List<LogLevel>();

        public void Write(LogLevel level, string category, string message, UnityEngine.Object context)
        {
            m_Levels.Add(level);
        }

        public int Count(LogLevel level)
        {
            int count = 0;
            foreach (LogLevel entry in m_Levels)
            {
                if (entry == level)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
