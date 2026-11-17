using UnityEngine;
using UnityEngine.UI;

namespace NeonRequiem.UI
{
    /// <summary>
    /// A graphic that receives pointer events but draws nothing. Large touch zones (movement
    /// area, camera drag area) use it instead of a transparent Image, which would still submit
    /// a full-screen quad and cost fill rate on mobile GPUs.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RaycastZone : Graphic
    {
        public override void SetMaterialDirty()
        {
        }

        public override void SetVerticesDirty()
        {
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
