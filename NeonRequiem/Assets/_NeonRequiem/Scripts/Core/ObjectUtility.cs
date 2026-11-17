using UnityEngine;

namespace NeonRequiem.Core
{
    /// <summary>Helpers for working with engine objects.</summary>
    public static class ObjectUtility
    {
        /// <summary>
        /// Destroys an engine object in whichever way the current mode allows: deferred while
        /// playing, immediate in edit mode (where <c>Object.Destroy</c> is an error). Null is ignored.
        /// </summary>
        public static void SafeDestroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
