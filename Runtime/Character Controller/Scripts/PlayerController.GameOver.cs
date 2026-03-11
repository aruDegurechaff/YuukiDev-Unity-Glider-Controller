using UnityEngine;

namespace YuukiDev.Controller
{
    public partial class PlayerController
    {
        #region Game Over
        internal void NotifyGameOverTriggeredByCollision()
        {
            canBoost = false;
            CurrentGlideMode = GlideMode.Normal;
            GlideModeChanged?.Invoke(CurrentGlideMode);
            GameOverTriggered?.Invoke(this);
        }

        public bool ReviveFromGameOver()
        {
            if (collisionGameOverHandler == null || !collisionGameOverHandler.RecoverFromGameOverState())
                return false;

            CurrentGlideMode = GlideMode.Normal;
            GlideModeChanged?.Invoke(CurrentGlideMode);

            if (NormalState != null)
                SwitchState(NormalState);

            collisionGameOverHandler.BeginReviveCollisionImmunity();
            ApplyPhantomGrace(Mathf.Max(0.1f, revivePhantomGraceDuration));
            RefreshBoostAvailability();
            return true;
        }

        public bool RestartFromGameOver(Transform respawnAnchor)
        {
            if (collisionGameOverHandler == null || !collisionGameOverHandler.RecoverFromGameOverState())
                return false;

            CurrentGlideMode = GlideMode.Normal;
            GlideModeChanged?.Invoke(CurrentGlideMode);

            if (NormalState != null)
                SwitchState(NormalState);

            ResetRunState();
            if (respawnAnchor != null)
                transform.SetPositionAndRotation(respawnAnchor.position, respawnAnchor.rotation);

            return true;
        }

        private void ResetRunState()
        {
            currentSpeed = baseSpeed;
            currentBoost = boostSettings != null ? GetBoostCapacity() : currentBoost;
            regenTimer = 0f;
            bank = 0f;
            smoothVel = Vector3.zero;
            activeSpeedScale = 1f;
            windOrbTimer = 0f;
            windOrbDurationTotal = 0f;
            activeManeuverabilityMultiplier = 1f;
            featherSlipTimer = 0f;
            featherSlipDurationTotal = 0f;
            lanternSparksTimer = 0f;
            lanternSparksDurationTotal = 0f;
            phantomGraceTimer = 0f;
            phantomGraceDurationTotal = 0f;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            collisionGameOverHandler?.ResetRuntimeState();
            RefreshBoostAvailability();
        }
        #endregion
    }
}
