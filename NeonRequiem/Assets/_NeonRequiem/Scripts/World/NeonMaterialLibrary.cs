using System;
using System.Collections.Generic;
using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.World
{
    /// <summary>
    /// Creates and caches the runtime materials used by procedurally built content (test arenas,
    /// the placeholder character). Every material is an instance of a template asset stored in
    /// Resources: referencing the templates from an asset is what makes Unity include the URP
    /// shaders and their keyword variants in device builds, which <c>Shader.Find</c> alone does not.
    /// </summary>
    public sealed class NeonMaterialLibrary : IDisposable
    {
        public const string SurfaceTemplatePath = "NeonRequiem/Materials/NR_Surface";
        public const string EmissiveTemplatePath = "NeonRequiem/Materials/NR_Emissive";
        public const string NeonTemplatePath = "NeonRequiem/Materials/NR_Neon";

        private const string LogCategory = "Materials";
        private const int GridTextureSize = 256;

        private static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int k_Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int k_Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        private readonly Dictionary<string, Material> m_Cache = new Dictionary<string, Material>();
        private readonly List<UnityEngine.Object> m_Owned = new List<UnityEngine.Object>();
        private readonly Material m_SurfaceTemplate;
        private readonly Material m_EmissiveTemplate;
        private readonly Material m_NeonTemplate;
        private Texture2D m_GridTexture;
        private bool m_Disposed;

        public NeonMaterialLibrary()
        {
            m_SurfaceTemplate = LoadTemplate(SurfaceTemplatePath);
            m_EmissiveTemplate = LoadTemplate(EmissiveTemplatePath);
            m_NeonTemplate = LoadTemplate(NeonTemplatePath);
        }

        /// <summary>True when every template material was found. Content still builds without them, untextured.</summary>
        public bool HasAllTemplates => m_SurfaceTemplate != null && m_EmissiveTemplate != null && m_NeonTemplate != null;

        /// <summary>Opaque lit surface.</summary>
        public Material GetSurface(Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            string key = "S|" + ColorKey(color) + "|" + smoothness.ToString("F2") + "|" + metallic.ToString("F2");
            if (TryGetCached(key, out Material material))
            {
                return material;
            }

            material = Instantiate(m_SurfaceTemplate, key);
            if (material != null)
            {
                material.SetColor(k_BaseColor, color);
                material.SetFloat(k_Smoothness, smoothness);
                material.SetFloat(k_Metallic, metallic);
            }

            return Store(key, material);
        }

        /// <summary>Lit surface that also emits light of <paramref name="emission"/> times <paramref name="intensity"/>.</summary>
        public Material GetEmissive(Color color, Color emission, float intensity, float smoothness = 0.5f)
        {
            string key = "E|" + ColorKey(color) + "|" + ColorKey(emission) + "|" + intensity.ToString("F2") + "|" + smoothness.ToString("F2");
            if (TryGetCached(key, out Material material))
            {
                return material;
            }

            material = Instantiate(m_EmissiveTemplate, key);
            if (material != null)
            {
                material.SetColor(k_BaseColor, color);
                material.SetColor(k_EmissionColor, emission * intensity);
                material.SetFloat(k_Smoothness, smoothness);
            }

            return Store(key, material);
        }

        /// <summary>Unlit, self-illuminated colour. Values above 1 bloom on presets with HDR enabled.</summary>
        public Material GetNeon(Color color, float intensity)
        {
            string key = "N|" + ColorKey(color) + "|" + intensity.ToString("F2");
            if (TryGetCached(key, out Material material))
            {
                return material;
            }

            material = Instantiate(m_NeonTemplate, key);
            if (material != null)
            {
                Color hdr = color * intensity;
                hdr.a = 1f;
                material.SetColor(k_BaseColor, hdr);
            }

            return Store(key, material);
        }

        /// <summary>
        /// Lit surface with a procedural line grid. A plain floor gives no sense of speed or
        /// distance, which makes movement impossible to judge; the grid fixes that for greybox work.
        /// </summary>
        /// <param name="tiling">Grid cells across the mesh's 0..1 UV range.</param>
        public Material GetGrid(Color tint, Vector2 tiling, float smoothness = 0.45f)
        {
            string key = "G|" + ColorKey(tint) + "|" + tiling.x.ToString("F2") + "|" + tiling.y.ToString("F2") + "|" + smoothness.ToString("F2");
            if (TryGetCached(key, out Material material))
            {
                return material;
            }

            material = Instantiate(m_SurfaceTemplate, key);
            if (material != null)
            {
                material.SetColor(k_BaseColor, tint);
                material.SetFloat(k_Smoothness, smoothness);
                material.SetTexture(k_BaseMap, GetGridTexture());
                material.SetTextureScale(k_BaseMap, tiling);
            }

            return Store(key, material);
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            for (int i = 0; i < m_Owned.Count; i++)
            {
                if (m_Owned[i] != null)
                {
                    UnityEngine.Object.Destroy(m_Owned[i]);
                }
            }

            m_Owned.Clear();
            m_Cache.Clear();
            m_GridTexture = null;
        }

        private static Material LoadTemplate(string path)
        {
            Material template = Resources.Load<Material>(path);
            if (template == null)
            {
                NRLog.Error(LogCategory, "Template material missing at Resources/" + path + ". Affected objects render with the default material.");
            }

            return template;
        }

        private static string ColorKey(Color color)
        {
            Color32 c = color;
            return c.r + "," + c.g + "," + c.b + "," + c.a;
        }

        private bool TryGetCached(string key, out Material material)
        {
            return m_Cache.TryGetValue(key, out material) && material != null;
        }

        private Material Instantiate(Material template, string key)
        {
            if (template == null)
            {
                return null;
            }

            var material = new Material(template) { name = "NR_Runtime_" + key };
            m_Owned.Add(material);
            return material;
        }

        private Material Store(string key, Material material)
        {
            if (material != null)
            {
                m_Cache[key] = material;
            }

            return material;
        }

        private Texture2D GetGridTexture()
        {
            if (m_GridTexture != null)
            {
                return m_GridTexture;
            }

            const int size = GridTextureSize;
            const int line = 3;
            var pixels = new Color32[size * size];
            var fill = new Color32(150, 156, 176, 255);
            var edge = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = x < line || y < line || x >= size - line || y >= size - line;
                    pixels[y * size + x] = onLine ? edge : fill;
                }
            }

            m_GridTexture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "NR_GridTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            m_GridTexture.SetPixels32(pixels);
            m_GridTexture.Apply(true, true);
            m_Owned.Add(m_GridTexture);
            return m_GridTexture;
        }
    }
}
