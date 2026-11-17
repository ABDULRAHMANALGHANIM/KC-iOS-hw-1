using UnityEngine;
using UnityEngine.UI;

namespace NeonRequiem.UI
{
    /// <summary>
    /// A graphic that receives pointer events across its whole rectangle but renders (almost)
    /// nothing. Large touch zones (movement area, camera drag area) use it instead of a
    /// transparent Image, which would submit a full-screen quad and cost fill rate on mobile GPUs.
    /// <para>
    /// It emits a single fully transparent one-unit quad rather than no geometry at all: uGUI's
    /// raycaster ignores graphics the canvas has not registered for drawing, and a graphic with
    /// real (if negligible) geometry is always registered. Hit testing uses the RectTransform,
    /// not the mesh, so the whole zone stays touchable.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RaycastZone : Graphic
    {
        private static readonly Color32 k_Transparent = new Color32(0, 0, 0, 0);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect();
            float x = rect.xMin;
            float y = rect.yMin;
            vh.AddVert(new Vector3(x, y), k_Transparent, Vector2.zero);
            vh.AddVert(new Vector3(x, y + 1f), k_Transparent, Vector2.zero);
            vh.AddVert(new Vector3(x + 1f, y + 1f), k_Transparent, Vector2.zero);
            vh.AddVert(new Vector3(x + 1f, y), k_Transparent, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
