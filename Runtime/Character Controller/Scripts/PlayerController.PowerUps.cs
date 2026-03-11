using UnityEngine;

namespace YuukiDev.Controller
{
    public partial class PlayerController
    {
        #region Power Ups
        public void ApplyWindOrbBurst(float speedMultiplier, float duration, float instantSpeedGain, float fovBoost)
        {
            if (IsGameOver)
                return;

            float safeMultiplier = Mathf.Max(1f, speedMultiplier);
            float safeDuration = Mathf.Max(0.01f, duration);
            activeSpeedScale = Mathf.Max(activeSpeedScale, safeMultiplier);
            windOrbTimer = Mathf.Max(windOrbTimer, safeDuration);
            windOrbDurationTotal = Mathf.Max(windOrbDurationTotal, safeDuration);

            float boostedMaxSpeed = maxSpeed * activeSpeedScale;
            currentSpeed = Mathf.Clamp(currentSpeed + Mathf.Max(0f, instantSpeedGain), minSpeed, boostedMaxSpeed);

            if (cameraFollow != null)
                cameraFollow.TriggerTemporaryFovBoost(fovBoost, duration);
        }

        public void ApplyPhantomGrace(float duration)
        {
            if (IsGameOver)
                return;

            float targetDuration = Mathf.Max(0.1f, duration);
            bool wasInactive = phantomGraceTimer <= 0f;
            phantomGraceTimer = Mathf.Max(phantomGraceTimer, targetDuration);
            phantomGraceDurationTotal = Mathf.Max(phantomGraceDurationTotal, targetDuration);

            // Pre-apply collision ignores so phasing is immediate on first contact.
            if (wasInactive)
                collisionGameOverHandler?.IgnoreAllObstacleCollisionsInScene();
        }

        public void ApplyFeatherSlip(float maneuverabilityMultiplier, float duration)
        {
            if (IsGameOver)
                return;

            activeManeuverabilityMultiplier = Mathf.Max(activeManeuverabilityMultiplier, maneuverabilityMultiplier);
            float targetDuration = Mathf.Max(0.1f, duration);
            featherSlipTimer = Mathf.Max(featherSlipTimer, targetDuration);
            featherSlipDurationTotal = Mathf.Max(featherSlipDurationTotal, targetDuration);
        }

        public void ApplyLanternSparksTimer(float duration)
        {
            if (IsGameOver)
                return;

            float targetDuration = Mathf.Max(0.1f, duration);
            lanternSparksTimer = Mathf.Max(lanternSparksTimer, targetDuration);
            lanternSparksDurationTotal = Mathf.Max(lanternSparksDurationTotal, targetDuration);
        }

        public void ApplyPickupFeedback(float boostRefillPercentOverride = -1f)
        {
            if (IsGameOver)
                return;

            float adjustedMaxSpeed = maxSpeed * activeSpeedScale;
            currentSpeed = Mathf.Clamp(currentSpeed + pickupSpeedPulse, minSpeed, adjustedMaxSpeed);

            if (boostSettings == null)
                return;

            float refillPercent = boostRefillPercentOverride >= 0f
                ? boostRefillPercentOverride
                : pickupBoostRefillPercent;
            float refill = GetBoostCapacity() * Mathf.Clamp01(refillPercent);
            currentBoost = Mathf.Clamp(currentBoost + refill, 0f, GetBoostCapacity());
            RefreshBoostAvailability();
        }

        private void UpdateTimedEffects()
        {
            if (regenTimer > 0f)
            {
                regenTimer -= Time.fixedDeltaTime;
                if (regenTimer < 0f)
                    regenTimer = 0f;
            }

            if (windOrbTimer > 0f)
            {
                windOrbTimer -= Time.fixedDeltaTime;
                if (windOrbTimer <= 0f)
                {
                    windOrbTimer = 0f;
                    windOrbDurationTotal = 0f;
                    activeSpeedScale = 1f;
                }
            }

            if (featherSlipTimer > 0f)
            {
                featherSlipTimer -= Time.fixedDeltaTime;
                if (featherSlipTimer <= 0f)
                {
                    featherSlipTimer = 0f;
                    featherSlipDurationTotal = 0f;
                    activeManeuverabilityMultiplier = 1f;
                }
            }

            if (lanternSparksTimer > 0f)
            {
                lanternSparksTimer -= Time.fixedDeltaTime;
                if (lanternSparksTimer <= 0f)
                {
                    lanternSparksTimer = 0f;
                    lanternSparksDurationTotal = 0f;
                }
            }

            if (phantomGraceTimer > 0f)
            {
                phantomGraceTimer -= Time.fixedDeltaTime;
                if (phantomGraceTimer <= 0f)
                {
                    phantomGraceTimer = 0f;
                    phantomGraceDurationTotal = 0f;
                    collisionGameOverHandler?.RestoreIgnoredObstacleCollisions();
                }
            }
        }
        #endregion
    }
}
