using UnityEngine;

namespace NeonRequiem.UI
{
    /// <summary>
    /// UI sprites generated at runtime (disc, ring, rounded rectangle). PLACEHOLDER ART: these
    /// stand in for the final icon set listed in ASSET_REQUIREMENTS.md and keep the HUD free of
    /// external texture dependencies. Sprites are created once and cached.
    /// </summary>
    public static class ProceduralSprites
    {
        private const int DiscSize = 128;
        private const int RingSize = 256;
        private const int RoundedSize = 64;
        private const int RoundedRadius = 22;

        private static Sprite s_Disc;
        private static Sprite s_Ring;
        private static Sprite s_RoundedRect;

        /// <summary>Filled circle with an anti-aliased edge.</summary>
        public static Sprite Disc
        {
            get
            {
                if (s_Disc == null)
                {
                    s_Disc = CreateRadial("NR_Disc", DiscSize, 0f);
                }

                return s_Disc;
            }
        }

        /// <summary>Thin circle outline with a faint fill.</summary>
        public static Sprite Ring
        {
            get
            {
                if (s_Ring == null)
                {
                    s_Ring = CreateRadial("NR_Ring", RingSize, 0.9f);
                }

                return s_Ring;
            }
        }

        /// <summary>Nine-sliced rounded rectangle for panels and buttons.</summary>
        public static Sprite RoundedRect
        {
            get
            {
                if (s_RoundedRect == null)
                {
                    s_RoundedRect = CreateRoundedRect();
                }

                return s_RoundedRect;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            s_Disc = null;
            s_Ring = null;
            s_RoundedRect = null;
        }

        private static Sprite CreateRadial(string name, int size, float innerRadius01)
        {
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float outer = half - 1f;
            float inner = outer * innerRadius01;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(outer - distance);
                    if (innerRadius01 > 0f)
                    {
                        // Outline at full strength, interior at a faint fill so the control reads on bright scenes.
                        float outline = Mathf.Clamp01(distance - inner + 1f);
                        alpha *= Mathf.Lerp(0.18f, 1f, outline);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            return CreateSprite(name, size, pixels, Vector4.zero);
        }

        private static Sprite CreateRoundedRect()
        {
            int size = RoundedSize;
            var pixels = new Color32[size * size];
            float radius = RoundedRadius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the nearest corner circle centre; inside the cross-shaped body it is zero.
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            return CreateSprite("NR_RoundedRect", size, pixels, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite CreateSprite(string name, int size, Color32[] pixels, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
