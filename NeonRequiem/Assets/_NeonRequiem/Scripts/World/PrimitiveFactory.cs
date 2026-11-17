using UnityEngine;

namespace NeonRequiem.World
{
    /// <summary>
    /// Builds greybox geometry from Unity's built-in primitives. PLACEHOLDER ART: everything made
    /// here is stand-in content until the modular environment kit exists.
    /// </summary>
    public static class PrimitiveFactory
    {
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
                    // Disable first: Destroy is deferred to the end of the frame.
                    collider.enabled = false;
                    Object.Destroy(collider);
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
