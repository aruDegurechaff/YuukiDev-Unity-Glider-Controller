using UnityEngine;

namespace YuukiDev.Controller
{
    public partial class PlayerController
    {
        #region Collider
        private void ResolveDynamicWidthCollider()
        {
            if (dynamicWidthCollider == null)
            {
                dynamicWidthCollider = GetComponent<BoxCollider>();
                if (dynamicWidthCollider == null)
                    dynamicWidthCollider = GetComponentInChildren<BoxCollider>(true);
            }

            if (dynamicWidthCollider == null)
                return;

            defaultColliderWidth = Mathf.Max(0.01f, dynamicWidthCollider.size.x);
            hasDynamicWidthCollider = true;
        }

        private void UpdateDynamicColliderWidth()
        {
            if (!hasDynamicWidthCollider || dynamicWidthCollider == null)
                return;

            float widthMultiplier = 1f;
            if (CurrentGlideMode == GlideMode.SpeedingUp)
                widthMultiplier = Mathf.Max(0.2f, speedUpWidthMultiplier);
            else if (CurrentGlideMode == GlideMode.SlowingDown)
                widthMultiplier = Mathf.Max(1f, slowDownWidthMultiplier);

            Vector3 size = dynamicWidthCollider.size;
            float targetWidth = defaultColliderWidth * widthMultiplier;
            float step = Mathf.Max(0.01f, colliderWidthTransitionSpeed) * Time.fixedDeltaTime;
            size.x = Mathf.MoveTowards(size.x, targetWidth, step);
            dynamicWidthCollider.size = size;
        }
        #endregion
    }
}
