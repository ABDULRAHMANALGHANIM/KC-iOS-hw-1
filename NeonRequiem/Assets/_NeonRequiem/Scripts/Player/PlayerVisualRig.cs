using NeonRequiem.Core;
using NeonRequiem.World;
using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>
    /// PLACEHOLDER PRESENTATION for Kael: a figure assembled from primitives and animated
    /// procedurally (stride, lean, squash and stretch, dodge tuck, invulnerability glow). It
    /// exists so movement is readable before the real character model, rig and animation clips
    /// are delivered (see ASSET_REQUIREMENTS.md). It only reads <see cref="PlayerLocomotion"/>;
    /// nothing here influences gameplay.
    /// </summary>
    public sealed class PlayerVisualRig : MonoBehaviour
    {
        private const float StrideRadiansPerMetre = 3.6f;
        private const float MaxLegSwingDegrees = 40f;
        private const float MaxArmSwingDegrees = 32f;
        private const float LimbSharpness = 18f;
        private const float LeanSharpness = 12f;
        private const float SquashStiffness = 180f;
        private const float SquashDamping = 16f;
        private const float MinLandingImpact = 3f;

        private static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly Color k_Suit = new Color(0.07f, 0.08f, 0.12f);
        private static readonly Color k_SuitLight = new Color(0.14f, 0.16f, 0.22f);
        private static readonly Color k_Skin = new Color(0.85f, 0.72f, 0.64f);
        private static readonly Color k_Hair = new Color(0.03f, 0.03f, 0.05f);
        private static readonly Color k_Silver = new Color(0.78f, 0.82f, 0.90f);
        private static readonly Color k_Circuit = new Color(0.20f, 0.90f, 1.00f);
        private static readonly Color k_Device = new Color(1.00f, 0.25f, 0.70f);

        private PlayerLocomotion m_Locomotion;
        private Transform m_Pose;
        private Transform m_LegLeft;
        private Transform m_LegRight;
        private Transform m_ArmLeft;
        private Transform m_ArmRight;
        private Material m_AccentMaterial;
        private float m_AccentBaseIntensity = 2.2f;

        private float m_StridePhase;
        private float m_LegAngle;
        private float m_ArmAngle;
        private float m_Lean;
        private float m_Squash = 1f;
        private float m_SquashVelocity;
        private float m_Time;
        private bool m_GlowActive;

        /// <summary>Creates the placeholder body. Call once, before <see cref="Bind"/>.</summary>
        public void Build(NeonMaterialLibrary materials, MovementTuning tuning)
        {
            if (m_Pose != null)
            {
                return;
            }

            var poseObject = new GameObject("Pose");
            poseObject.layer = GameLayers.Player;
            m_Pose = poseObject.transform;
            m_Pose.SetParent(transform, false);

            Material suit = materials.GetSurface(k_Suit, 0.55f, 0.2f);
            Material suitLight = materials.GetSurface(k_SuitLight, 0.5f, 0.1f);
            Material skin = materials.GetSurface(k_Skin, 0.3f);
            Material hair = materials.GetSurface(k_Hair, 0.6f);
            Material silver = materials.GetSurface(k_Silver, 0.7f, 0.6f);
            Material device = materials.GetEmissive(k_Suit, k_Device, 2.5f);

            // Own instance of the circuitry material so the invulnerability glow never leaks onto other objects.
            Material sharedCircuit = materials.GetEmissive(k_Suit, k_Circuit, m_AccentBaseIntensity);
            if (sharedCircuit != null)
            {
                m_AccentMaterial = new Material(sharedCircuit) { name = "NR_Player_Circuit" };
            }

            // Proportions assume the default 1.8 m capsule and scale with the configured height.
            float s = tuning.height / 1.8f;

            Part(PrimitiveType.Capsule, "Torso", m_Pose, new Vector3(0f, 1.08f, 0f) * s, new Vector3(0.50f, 0.36f, 0.32f) * s, suit);
            Part(PrimitiveType.Cube, "Hips", m_Pose, new Vector3(0f, 0.74f, 0f) * s, new Vector3(0.40f, 0.20f, 0.26f) * s, suitLight);
            Part(PrimitiveType.Sphere, "Head", m_Pose, new Vector3(0f, 1.60f, 0.01f) * s, new Vector3(0.27f, 0.30f, 0.28f) * s, skin);
            Part(PrimitiveType.Sphere, "Hair", m_Pose, new Vector3(0f, 1.67f, -0.03f) * s, new Vector3(0.31f, 0.27f, 0.33f) * s, hair);
            Part(PrimitiveType.Cube, "HairSilverAccent", m_Pose, new Vector3(0.09f, 1.77f, 0.04f) * s, new Vector3(0.05f, 0.05f, 0.20f) * s, silver);
            Part(PrimitiveType.Cube, "Visor", m_Pose, new Vector3(0f, 1.62f, 0.135f) * s, new Vector3(0.22f, 0.045f, 0.03f) * s, m_AccentMaterial);
            Part(PrimitiveType.Cube, "ChestCircuit", m_Pose, new Vector3(0f, 1.16f, 0.165f) * s, new Vector3(0.05f, 0.34f, 0.02f) * s, m_AccentMaterial);
            Part(PrimitiveType.Cube, "SpineCircuit", m_Pose, new Vector3(0f, 1.12f, -0.165f) * s, new Vector3(0.05f, 0.44f, 0.02f) * s, m_AccentMaterial);

            // Energy blade, sheathed diagonally across the back.
            GameObject blade = Part(PrimitiveType.Cube, "EnergyBlade", m_Pose, new Vector3(0.05f, 1.10f, -0.21f) * s, new Vector3(0.045f, 0.95f, 0.03f) * s, m_AccentMaterial);
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);

            m_LegLeft = Limb("LegLeft", new Vector3(-0.12f, 0.70f, 0f) * s, 0.62f * s, 0.19f * s, suit);
            m_LegRight = Limb("LegRight", new Vector3(0.12f, 0.70f, 0f) * s, 0.62f * s, 0.19f * s, suit);
            m_ArmLeft = Limb("ArmLeft", new Vector3(-0.32f, 1.36f, 0f) * s, 0.56f * s, 0.13f * s, suitLight);
            m_ArmRight = Limb("ArmRight", new Vector3(0.32f, 1.36f, 0f) * s, 0.56f * s, 0.13f * s, suitLight);

            // Neural device on the left forearm.
            Part(PrimitiveType.Cube, "NeuralDevice", m_ArmLeft, new Vector3(0f, -0.40f, 0f) * s, new Vector3(0.17f, 0.16f, 0.17f) * s, device);
        }

        /// <summary>Starts listening to movement events.</summary>
        public void Bind(PlayerLocomotion locomotion)
        {
            Unbind();
            m_Locomotion = locomotion;
            if (m_Locomotion == null)
            {
                return;
            }

            m_Locomotion.Jumped += OnJumped;
            m_Locomotion.AirJumped += OnJumped;
            m_Locomotion.Landed += OnLanded;
        }

        /// <summary>Advances the procedural animation. Called by the player controller after movement.</summary>
        public void Tick(float deltaTime)
        {
            if (m_Locomotion == null || m_Pose == null || deltaTime <= 0f)
            {
                return;
            }

            float dt = Mathf.Min(deltaTime, 0.05f);
            m_Time += dt;

            LocomotionMode mode = m_Locomotion.Mode;
            bool grounded = mode == LocomotionMode.Grounded;
            bool dodging = mode == LocomotionMode.Dodging;
            float speed = m_Locomotion.PlanarSpeed;
            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, m_Locomotion.Tuning.sprintSpeed));

            float legTarget;
            float armTarget;
            float leanTarget;
            float bob = 0f;
            if (dodging)
            {
                legTarget = -35f;
                armTarget = 40f;
                leanTarget = 34f;
            }
            else if (grounded)
            {
                if (speed > 0.2f)
                {
                    m_StridePhase += speed * StrideRadiansPerMetre * dt;
                }

                float swing = Mathf.Sin(m_StridePhase) * Mathf.Clamp01(speed01 * 1.6f);
                legTarget = swing * MaxLegSwingDegrees;
                armTarget = -swing * MaxArmSwingDegrees;
                leanTarget = speed01 * 11f;
                bob = Mathf.Abs(Mathf.Cos(m_StridePhase)) * 0.045f * speed01;
            }
            else
            {
                bool rising = m_Locomotion.Velocity.y > 0f;
                legTarget = rising ? -22f : 14f;
                armTarget = rising ? -55f : -30f;
                leanTarget = rising ? -5f : 9f;
            }

            m_LegAngle = NRMath.Damp(m_LegAngle, legTarget, LimbSharpness, dt);
            m_ArmAngle = NRMath.Damp(m_ArmAngle, armTarget, LimbSharpness, dt);
            m_Lean = NRMath.Damp(m_Lean, leanTarget, LeanSharpness, dt);

            // Squash and stretch: a damped spring around 1, kicked by jump and landing events.
            float squashRest = dodging ? 0.84f : 1f;
            m_SquashVelocity += ((squashRest - m_Squash) * SquashStiffness - m_SquashVelocity * SquashDamping) * dt;
            m_Squash = Mathf.Clamp(m_Squash + m_SquashVelocity * dt, 0.6f, 1.35f);

            float breathing = grounded && speed < 0.2f ? Mathf.Sin(m_Time * 2.1f) * 0.012f : 0f;
            float vertical = m_Squash + breathing;
            float lateral = 1f / Mathf.Sqrt(Mathf.Max(0.3f, m_Squash));

            m_Pose.localPosition = new Vector3(0f, bob, 0f);
            m_Pose.localRotation = Quaternion.Euler(m_Lean, 0f, 0f);
            m_Pose.localScale = new Vector3(lateral, vertical, lateral);

            bool legsMirrored = grounded && !dodging;
            m_LegLeft.localRotation = Quaternion.Euler(m_LegAngle, 0f, 0f);
            m_LegRight.localRotation = Quaternion.Euler(legsMirrored ? -m_LegAngle : m_LegAngle * 0.5f, 0f, 0f);
            m_ArmLeft.localRotation = Quaternion.Euler(m_ArmAngle, 0f, legsMirrored ? 0f : -12f);
            m_ArmRight.localRotation = Quaternion.Euler(legsMirrored ? -m_ArmAngle : m_ArmAngle, 0f, legsMirrored ? 0f : 12f);

            UpdateGlow(m_Locomotion.IsInvulnerable);
        }

        private void OnDestroy()
        {
            Unbind();
            ObjectUtility.SafeDestroy(m_AccentMaterial);
        }

        private void Unbind()
        {
            if (m_Locomotion == null)
            {
                return;
            }

            m_Locomotion.Jumped -= OnJumped;
            m_Locomotion.AirJumped -= OnJumped;
            m_Locomotion.Landed -= OnLanded;
            m_Locomotion = null;
        }

        private void OnJumped()
        {
            m_SquashVelocity += 3.2f;
        }

        private void OnLanded(float impactSpeed)
        {
            if (impactSpeed >= MinLandingImpact)
            {
                m_SquashVelocity -= Mathf.Clamp(impactSpeed * 0.3f, 0.8f, 4.5f);
            }
        }

        /// <summary>The suit circuitry flares while the dodge's invulnerability window is open.</summary>
        private void UpdateGlow(bool active)
        {
            if (active == m_GlowActive || m_AccentMaterial == null)
            {
                return;
            }

            m_GlowActive = active;
            float intensity = active ? m_AccentBaseIntensity * 3f : m_AccentBaseIntensity;
            m_AccentMaterial.SetColor(k_EmissionColor, k_Circuit * intensity);
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            return PrimitiveFactory.Create(type, name, parent, position, Quaternion.identity, scale, material, false, GameLayers.Player);
        }

        /// <summary>Creates a limb that rotates around its top end (hip or shoulder).</summary>
        private Transform Limb(string name, Vector3 jointPosition, float length, float thickness, Material material)
        {
            var joint = new GameObject(name);
            joint.layer = GameLayers.Player;
            Transform t = joint.transform;
            t.SetParent(m_Pose, false);
            t.localPosition = jointPosition;

            // Unity's capsule primitive is 2 units tall, so half the length is the Y scale.
            Part(PrimitiveType.Capsule, name + "Mesh", t, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.5f, thickness), material);
            return t;
        }
    }
}
