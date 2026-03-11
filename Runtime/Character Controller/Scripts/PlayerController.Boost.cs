using UnityEngine;

namespace YuukiDev.Controller
{
    public partial class PlayerController
    {
        #region Boost
        public void ConsumeBoosters()
        {
            if (boostSettings == null)
                return;

            currentBoost -= Mathf.Max(0f, boostSettings.drainRate) * Time.fixedDeltaTime;
            currentBoost = Mathf.Max(currentBoost, 0f);

            // Reset regen cooldown whenever boost is used
            regenTimer = Mathf.Max(0f, regenDelay);

            RefreshBoostAvailability();
        }

        public void RegenBoosters(float multiplier)
        {
            if (regenTimer > 0f || boostSettings == null)
                return;

            float capacity = GetBoostCapacity();
            float normalized = Mathf.Clamp01(currentBoost / capacity);
            float curveValue = boostSettings.regenCurve != null ? boostSettings.regenCurve.Evaluate(normalized) : 1f;
            if (currentBoost <= 0.001f)
            {
                float emptyRegenFloor = minRegenFromEmpty > 0f ? Mathf.Clamp01(minRegenFromEmpty) : EmptyStaminaRegenFallback;
                curveValue = Mathf.Max(curveValue, emptyRegenFloor);
            }
            float regenAmount =
                Mathf.Max(curveValue, 0f) *
                Mathf.Max(0f, boostSettings.regenRate) *
                Mathf.Max(0f, multiplier) *
                Time.fixedDeltaTime;

            currentBoost = Mathf.Clamp(currentBoost + regenAmount, 0f, capacity);
            RefreshBoostAvailability();
        }

        public void ApplyBoostAcceleration()
        {
            // Makes it so you accelerate towards the max speed
            float adjustedMaxSpeed = maxSpeed * activeSpeedScale;
            float tempSpeed = currentSpeed / Mathf.Max(adjustedMaxSpeed, 0.01f);
            float targetSpeed = Mathf.Lerp(
                baseSpeed * speedUpMultiplier * activeSpeedScale,
                adjustedMaxSpeed,
                tempSpeed
            );

            float boostFactor = Mathf.Clamp01(currentBoost / GetBoostCapacity());
            float effectiveAcceleration = acceleration * boostFactor;

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                effectiveAcceleration * Time.fixedDeltaTime
            );
        }

        public void ApplySlowDown()
        {
            float targetSpeed = baseSpeed * slowDownMultiplier * activeSpeedScale;
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                acceleration * Time.fixedDeltaTime
            );
        }

        private float GetBoostCapacity()
        {
            return boostSettings != null ? Mathf.Max(0.01f, boostSettings.capacity) : 1f;
        }

        private bool CanEnterBoostState()
        {
            RefreshBoostAvailability();
            return canBoost;
        }

        private void RefreshBoostAvailability()
        {
            if (IsGameOver)
            {
                canBoost = false;
                return;
            }

            canBoost = currentBoost >= Mathf.Max(0.01f, minBoostToStart);
        }
        #endregion
    }
}
