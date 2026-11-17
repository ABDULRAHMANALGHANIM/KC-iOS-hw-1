using NeonRequiem.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonRequiem.World
{
    /// <summary>Which greybox layout to build.</summary>
    public enum ArenaLayout
    {
        /// <summary>Full movement course: stairs, slopes, jump platforms, gaps, dodge lane, camera obstacles, open edge.</summary>
        MovementCourse = 0,

        /// <summary>Small flat room for exercising the touch controls without distractions.</summary>
        ControlsLab = 1,
    }

    /// <summary>What a built arena tells the rest of the scene.</summary>
    public struct ArenaInfo
    {
        public Transform Root;
        public Vector3 SpawnPosition;
        public float SpawnYaw;

        /// <summary>Falling below this height triggers fall recovery.</summary>
        public float KillPlaneY;
    }

    /// <summary>
    /// Builds the Phase 1 test environments from primitives. PLACEHOLDER CONTENT: the layouts are
    /// measurement tools, not level art. Every feature exists to verify one movement or camera
    /// behaviour (listed in TEST_REPORT.md) and dimensions are chosen against the default
    /// movement tuning (jump 1.7 m, air jump 1.35 m, dodge 4.2 m).
    /// </summary>
    public static class TestArenaBuilder
    {
        private const float KillPlaneDepth = -22f;
        private const float RampThickness = 0.3f;

        private static readonly Color k_Fog = new Color(0.045f, 0.040f, 0.095f);
        private static readonly Color k_AmbientSky = new Color(0.16f, 0.19f, 0.34f);
        private static readonly Color k_AmbientEquator = new Color(0.12f, 0.10f, 0.20f);
        private static readonly Color k_AmbientGround = new Color(0.04f, 0.04f, 0.07f);
        private static readonly Color k_Moonlight = new Color(0.62f, 0.72f, 1.00f);

        private static readonly Color k_Floor = new Color(0.20f, 0.22f, 0.30f);
        private static readonly Color k_Structure = new Color(0.16f, 0.17f, 0.24f);
        private static readonly Color k_StructureLight = new Color(0.30f, 0.32f, 0.42f);
        private static readonly Color k_Tower = new Color(0.05f, 0.055f, 0.09f);
        private static readonly Color k_Cyan = new Color(0.20f, 0.90f, 1.00f);
        private static readonly Color k_Magenta = new Color(1.00f, 0.22f, 0.70f);
        private static readonly Color k_Amber = new Color(1.00f, 0.62f, 0.18f);

        /// <summary>
        /// Builds <paramref name="layout"/> and applies the scene atmosphere.
        /// </summary>
        /// <param name="sun">Directional light to restyle as moonlight; may be null.</param>
        /// <param name="camera">Camera whose background is matched to the fog; may be null.</param>
        public static ArenaInfo Build(ArenaLayout layout, NeonMaterialLibrary materials, Light sun, Camera camera)
        {
            var rootObject = new GameObject(layout == ArenaLayout.ControlsLab ? "Arena_ControlsLab" : "Arena_MovementCourse");
            Transform root = rootObject.transform;
            var builder = new Builder(root, materials);

            ApplyAtmosphere(sun, camera);

            ArenaInfo info = layout == ArenaLayout.ControlsLab ? BuildControlsLab(builder) : BuildMovementCourse(builder);
            info.Root = root;
            info.KillPlaneY = KillPlaneDepth;
            return info;
        }

        /// <summary>
        /// Night-city lighting. The same fog mode is also enabled in the scene files: Unity strips
        /// fog shader variants from builds unless a built scene uses them.
        /// </summary>
        private static void ApplyAtmosphere(Light sun, Camera camera)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = k_Fog;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = k_AmbientSky;
            RenderSettings.ambientEquatorColor = k_AmbientEquator;
            RenderSettings.ambientGroundColor = k_AmbientGround;

            if (sun != null)
            {
                sun.color = k_Moonlight;
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }

            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = k_Fog;
            }
        }

        private static ArenaInfo BuildMovementCourse(Builder b)
        {
            Material floor = b.Materials.GetGrid(k_Floor, new Vector2(36f, 36f));
            Material structure = b.Materials.GetSurface(k_Structure, 0.4f);
            Material light = b.Materials.GetSurface(k_StructureLight, 0.4f);
            Material cyan = b.Materials.GetNeon(k_Cyan, 2.4f);
            Material magenta = b.Materials.GetNeon(k_Magenta, 2.4f);
            Material amber = b.Materials.GetNeon(k_Amber, 2.2f);
            Material dimCyan = b.Materials.GetNeon(k_Cyan, 0.9f);

            // --- Ground slab (72 x 72 m, top at y = 0) with walls on three sides. The north side is
            // open: running off it exercises the kill plane and safe-position respawn.
            b.Solid("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(72f, 1f, 72f), floor);
            b.Solid("Wall_West", new Vector3(-36.5f, 2.5f, 0f), new Vector3(1f, 5f, 74f), structure);
            b.Solid("Wall_East", new Vector3(36.5f, 2.5f, 0f), new Vector3(1f, 5f, 74f), structure);
            b.Solid("Wall_South", new Vector3(0f, 2.5f, -36.5f), new Vector3(74f, 5f, 1f), structure);
            b.Strip("WallGlow_West", new Vector3(-35.95f, 4.6f, 0f), new Vector3(0.08f, 0.16f, 72f), cyan);
            b.Strip("WallGlow_East", new Vector3(35.95f, 4.6f, 0f), new Vector3(0.08f, 0.16f, 72f), magenta);
            b.Strip("WallGlow_South", new Vector3(0f, 4.6f, -35.95f), new Vector3(72f, 0.16f, 0.08f), cyan);
            b.Strip("EdgeWarning_North", new Vector3(0f, 0.02f, 35.7f), new Vector3(72f, 0.04f, 0.3f), amber);

            // Balcony over the void, for a deliberate walk-off.
            b.Solid("Balcony", new Vector3(0f, -0.25f, 39f), new Vector3(8f, 0.5f, 6f), light);
            b.Strip("BalconyEdge", new Vector3(0f, 0.02f, 41.85f), new Vector3(8f, 0.04f, 0.2f), amber);

            // --- Zone A (west): steps and slopes.
            // Stairs: 10 steps of 0.2 m (below the 0.35 m step offset) up to a 2 m deck.
            for (int i = 0; i < 10; i++)
            {
                float top = 0.2f * (i + 1);
                b.Solid("Stair_" + i, new Vector3(-24f, top * 0.5f, -14f + 0.45f * (i + 0.5f)), new Vector3(4f, top, 0.45f), light);
            }

            b.Solid("Deck", new Vector3(-24f, 1f, -6.5f), new Vector3(8f, 2f, 6f), structure);
            b.Strip("DeckEdge", new Vector3(-24f, 2.02f, -9.4f), new Vector3(8f, 0.04f, 0.12f), cyan);

            // Walkable ramps down from the deck: 20 degrees (easy) and 35 degrees (near the 48 degree limit).
            b.Ramp("Ramp_20deg", new Vector3(-26f, 0f, 1.99f), 180f, 3.6f, 5.49f, 2f, light);
            b.Ramp("Ramp_35deg", new Vector3(-22f, 0f, -0.64f), 180f, 3.6f, 2.86f, 2f, light);
            // 58 degrees: steeper than the slope limit, the character must slide off.
            b.Ramp("Slide_58deg", new Vector3(-30f, 0f, 8f), 0f, 5f, 2.5f, 4f, structure);
            b.Strip("SlideMarker", new Vector3(-30f, 0.02f, 7.6f), new Vector3(5f, 0.04f, 0.15f), magenta);

            // --- Zone B (centre): jump heights and gaps.
            JumpPlatform(b, "Jump_1.0m", new Vector3(-4f, 0f, -6f), 3f, 1.0f, structure, cyan);
            JumpPlatform(b, "Jump_1.6m", new Vector3(0f, 0f, -2f), 3f, 1.6f, structure, cyan);
            JumpPlatform(b, "Jump_2.9m_DoubleJump", new Vector3(4f, 0f, 2f), 3f, 2.9f, structure, magenta);
            JumpPlatform(b, "Jump_4.2m", new Vector3(0f, 0f, 7f), 3f, 4.2f, structure, magenta);
            // Floating slabs at the same height: a 4 m gap (sprint jump or double jump) then a 6 m gap
            // (needs sprint plus double jump, or an air dodge).
            b.Solid("Gap_4m", new Vector3(0f, 4f, 14f), new Vector3(4f, 0.4f, 3f), light);
            b.Strip("Gap_4m_Edge", new Vector3(0f, 4.22f, 12.56f), new Vector3(4f, 0.04f, 0.12f), amber);
            b.Solid("Gap_6m", new Vector3(0f, 4f, 23f), new Vector3(4f, 0.4f, 3f), light);
            b.Strip("Gap_6m_Edge", new Vector3(0f, 4.22f, 21.56f), new Vector3(4f, 0.04f, 0.12f), amber);

            // --- Zone C (east): dodge lane with a marker every metre and a bright one every 5 m.
            for (int i = 0; i <= 20; i++)
            {
                bool major = i % 5 == 0;
                b.Strip(
                    "DodgeMark_" + i,
                    new Vector3(22f, 0.015f, -26f + i),
                    new Vector3(major ? 3.4f : 2.6f, 0.03f, major ? 0.12f : 0.05f),
                    major ? cyan : dimCyan);
            }

            // Pillar field: tight spacing to exercise camera collision and occlusion.
            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 4; iz++)
                {
                    b.Solid(
                        "Pillar_" + ix + "_" + iz,
                        new Vector3(18.8f + ix * 3.2f, 3f, 1.2f + iz * 3.2f),
                        new Vector3(0.9f, 6f, 0.9f),
                        structure);
                }
            }

            // Tunnel: 2.6 m wide with a 2.7 m ceiling. The camera must pull in and stay inside.
            b.Solid("Tunnel_WallA", new Vector3(22.45f, 1.5f, 22f), new Vector3(0.5f, 3f, 12f), structure);
            b.Solid("Tunnel_WallB", new Vector3(25.55f, 1.5f, 22f), new Vector3(0.5f, 3f, 12f), structure);
            b.Solid("Tunnel_Ceiling", new Vector3(24f, 2.85f, 22f), new Vector3(3.6f, 0.3f, 12f), structure);
            b.Strip("Tunnel_Light", new Vector3(24f, 2.68f, 22f), new Vector3(0.1f, 0.04f, 11f), cyan);

            // Low ceiling at 2.15 m: a jump underneath must be cut short by the head bump.
            b.Solid("LowCeiling", new Vector3(14f, 2.3f, 22f), new Vector3(4f, 0.3f, 4f), light);
            b.Strip("LowCeiling_Glow", new Vector3(14f, 2.13f, 22f), new Vector3(3.6f, 0.03f, 0.1f), magenta);
            for (int i = 0; i < 4; i++)
            {
                float x = 14f + ((i & 1) == 0 ? -1.85f : 1.85f);
                float z = 22f + ((i & 2) == 0 ? -1.85f : 1.85f);
                b.Solid("LowCeiling_Post_" + i, new Vector3(x, 1.075f, z), new Vector3(0.3f, 2.15f, 0.3f), structure);
            }

            // Accent lights. Three unshadowed point lights stay within every preset's per-object light limit.
            b.PointLight("Light_Jumps", new Vector3(0f, 7f, 2f), k_Magenta, 14f, 18f);
            b.PointLight("Light_DodgeLane", new Vector3(22f, 4.5f, -16f), k_Cyan, 12f, 16f);
            b.PointLight("Light_Stairs", new Vector3(-24f, 5f, -8f), k_Amber, 12f, 16f);

            BuildSkyline(b);

            return new ArenaInfo { SpawnPosition = new Vector3(0f, 0.1f, -28f), SpawnYaw = 0f };
        }

        private static ArenaInfo BuildControlsLab(Builder b)
        {
            Material floor = b.Materials.GetGrid(k_Floor, new Vector2(14f, 14f));
            Material structure = b.Materials.GetSurface(k_Structure, 0.4f);
            Material light = b.Materials.GetSurface(k_StructureLight, 0.4f);
            Material cyan = b.Materials.GetNeon(k_Cyan, 2.4f);
            Material magenta = b.Materials.GetNeon(k_Magenta, 2.4f);

            b.Solid("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(28f, 1f, 28f), floor);
            b.Solid("Wall_West", new Vector3(-14.5f, 2f, 0f), new Vector3(1f, 4f, 30f), structure);
            b.Solid("Wall_East", new Vector3(14.5f, 2f, 0f), new Vector3(1f, 4f, 30f), structure);
            b.Solid("Wall_South", new Vector3(0f, 2f, -14.5f), new Vector3(30f, 4f, 1f), structure);
            b.Solid("Wall_North", new Vector3(0f, 2f, 14.5f), new Vector3(30f, 4f, 1f), structure);
            b.Strip("WallGlow_North", new Vector3(0f, 3.7f, 13.95f), new Vector3(28f, 0.14f, 0.08f), cyan);
            b.Strip("WallGlow_South", new Vector3(0f, 3.7f, -13.95f), new Vector3(28f, 0.14f, 0.08f), magenta);

            // Compass markers: the north marker is cyan, so camera yaw can be read at a glance.
            b.Solid("Marker_North", new Vector3(0f, 0.75f, 9f), new Vector3(1.5f, 1.5f, 1.5f), light);
            b.Strip("Marker_North_Glow", new Vector3(0f, 1.52f, 9f), new Vector3(1.5f, 0.04f, 1.5f), cyan);
            b.Solid("Marker_East", new Vector3(9f, 0.5f, 0f), new Vector3(1.5f, 1f, 1.5f), light);
            b.Solid("Marker_West", new Vector3(-9f, 0.5f, 0f), new Vector3(1.5f, 1f, 1.5f), light);
            b.Strip("CentreRing", new Vector3(0f, 0.015f, 0f), new Vector3(0.12f, 0.03f, 8f), magenta);
            b.Strip("CentreRing2", new Vector3(0f, 0.015f, 0f), new Vector3(8f, 0.03f, 0.12f), magenta);

            b.PointLight("Light_Centre", new Vector3(0f, 6f, 0f), k_Cyan, 12f, 18f);

            return new ArenaInfo { SpawnPosition = new Vector3(0f, 0.1f, -6f), SpawnYaw = 0f };
        }

        private static void JumpPlatform(Builder b, string name, Vector3 basePosition, float width, float height, Material body, Material edge)
        {
            b.Solid(name, basePosition + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width), body);
            b.Strip(name + "_Edge", basePosition + new Vector3(0f, height + 0.02f, -width * 0.5f + 0.08f), new Vector3(width, 0.04f, 0.12f), edge);
        }

        /// <summary>Distant tower silhouettes so the slab reads as a place inside NOCTIS rather than a void.</summary>
        private static void BuildSkyline(Builder b)
        {
            Material tower = b.Materials.GetSurface(k_Tower, 0.6f);
            Material[] bands =
            {
                b.Materials.GetNeon(k_Cyan, 1.8f),
                b.Materials.GetNeon(k_Magenta, 1.8f),
                b.Materials.GetNeon(k_Amber, 1.6f),
            };

            // Fixed seed: the skyline is identical on every run, which keeps screenshots comparable.
            var random = new System.Random(2189);
            const int count = 16;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
                float distance = 62f + (float)random.NextDouble() * 34f;
                float width = 9f + (float)random.NextDouble() * 9f;
                float depth = 9f + (float)random.NextDouble() * 9f;
                float height = 34f + (float)random.NextDouble() * 52f;
                var position = new Vector3(Mathf.Sin(angle) * distance, 0f, Mathf.Cos(angle) * distance);

                // Towers start well below the slab: the arena floats high inside the vertical city.
                const float foot = -40f;
                b.Decor("Tower_" + i, position + new Vector3(0f, foot + (height - foot) * 0.5f, 0f), new Vector3(width, height - foot, depth), tower);

                int bandCount = 2 + random.Next(2);
                for (int j = 0; j < bandCount; j++)
                {
                    float y = height * (0.35f + 0.55f * (float)random.NextDouble());
                    b.Decor(
                        "Tower_" + i + "_Band_" + j,
                        position + new Vector3(0f, y, 0f),
                        new Vector3(width + 0.2f, 0.5f, depth + 0.2f),
                        bands[random.Next(bands.Length)]);
                }
            }
        }

        /// <summary>Small helper that carries the root and material library through the build.</summary>
        private sealed class Builder
        {
            private readonly Transform m_Root;

            public Builder(Transform root, NeonMaterialLibrary materials)
            {
                m_Root = root;
                Materials = materials;
            }

            public NeonMaterialLibrary Materials { get; }

            /// <summary>Collidable box the player can stand on and the camera collides with.</summary>
            public void Solid(string name, Vector3 center, Vector3 size, Material material)
            {
                PrimitiveFactory.Box(name, m_Root, center, size, material, true, GameLayers.Default);
            }

            /// <summary>Thin emissive marker without collision or shadows.</summary>
            public void Strip(string name, Vector3 center, Vector3 size, Material material)
            {
                GameObject go = PrimitiveFactory.Box(name, m_Root, center, size, material, false, GameLayers.Default);
                DisableShadows(go);
            }

            /// <summary>Non-colliding scenery.</summary>
            public void Decor(string name, Vector3 center, Vector3 size, Material material)
            {
                GameObject go = PrimitiveFactory.Box(name, m_Root, center, size, material, false, GameLayers.Default);
                DisableShadows(go);
            }

            /// <summary>
            /// Inclined slab whose top surface starts on the ground at <paramref name="basePosition"/>
            /// and climbs <paramref name="rise"/> metres over <paramref name="run"/> metres in the
            /// direction given by <paramref name="yaw"/>.
            /// </summary>
            public void Ramp(string name, Vector3 basePosition, float yaw, float width, float run, float rise, Material material)
            {
                float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
                float length = Mathf.Sqrt(run * run + rise * rise);
                Quaternion rotation = Quaternion.Euler(-angle, yaw, 0f);
                Vector3 surfaceCentre = basePosition + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, rise * 0.5f, run * 0.5f);
                Vector3 centre = surfaceCentre - rotation * Vector3.up * (RampThickness * 0.5f);
                PrimitiveFactory.Create(
                    PrimitiveType.Cube, name, m_Root, centre, rotation,
                    new Vector3(width, RampThickness, length), material, true, GameLayers.Default);
            }

            public void PointLight(string name, Vector3 position, Color color, float intensity, float range)
            {
                var go = new GameObject(name);
                go.transform.SetParent(m_Root, false);
                go.transform.localPosition = position;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.intensity = intensity;
                light.range = range;
                light.shadows = LightShadows.None;
            }

            private static void DisableShadows(GameObject go)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }
    }
}
