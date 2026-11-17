using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.World
{
    /// <summary>
    /// Builds greybox geometry from Unity's built-in primitives. PLACEHOLDER ART: everything made
    /// here is stand-in content until the modular environment kit exists.
    /// </summary>
    public static class PrimitiveFactory
    {
        /// <summary>
        /// GameObject.CreatePrimitive adds these components by name inside the engine. Player
        /// builds strip engine classes that no script or asset references, which would make the
        /// primitives come out without meshes or colliders on a device. Referencing the types
        /// here (together with Assets/_NeonRequiem/link.xml) keeps them in the build.
        /// </summary>
        private static readonly System.Type[] k_PrimitiveComponents =
        {
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(BoxCollider),
            typeof(SphereCollider),
            typeof(CapsuleCollider),
        };

        /// <summary>Number of engine component types primitives depend on (kept alive for build stripping).</summary>
        public static int RequiredComponentCount => k_PrimitiveComponents.Length;

        /// <summary>Creates a primitive. Its collider is removed unless <paramref name="solid"/> is set.</summary>
        public static GameObject Create(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material,
            bool solid,
            int layer)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;

            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;

            if (!solid)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    // Disable first: while playing, destruction is deferred to the end of the frame.
                    collider.enabled = false;
                    ObjectUtility.SafeDestroy(collider);
                }
            }

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return go;
        }

        /// <summary>Axis-aligned box.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool solid, int layer)
        {
            return Create(PrimitiveType.Cube, name, parent, center, Quaternion.identity, size, material, solid, layer);
        }
    }
}
