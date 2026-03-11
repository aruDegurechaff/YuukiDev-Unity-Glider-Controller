using UnityEngine;

namespace YuukiDev.Controller
{
    public partial class PlayerController
    {
        #region Movement
        // Physics
        public void ApplyNaturalMovement()
        {
            float turn01 = GetTurnInput01();
            float turnDragScale = Mathf.Lerp(1f, Mathf.Max(1f, turnDragMultiplier), turn01);

            // Pitch as -180 to 180
            float pitch = transform.eulerAngles.x;
            if (pitch > 180) pitch -= 360;
            float pitchRad = pitch * Mathf.Deg2Rad;

            // Downward pitch adds speed, upward pitch trims speed.
            float pitchAccel = Mathf.Sin(pitchRad) * thrustFactor;
            if (pitchAccel > 0f)
                pitchAccel *= Mathf.Max(1f, diveAccelerationMultiplier);
            else if (pitchAccel < 0f)
                pitchAccel *= Mathf.Clamp(climbDecelerationMultiplier, 0.5f, 1f);
            currentSpeed += pitchAccel * Time.fixedDeltaTime;

            // Natural air drag on speed (always)
            float speed01 = Mathf.Clamp01(currentSpeed / Mathf.Max(maxSpeed, 0.01f));
            float curveDrag = dragCurve != null ? dragCurve.Evaluate(speed01) : speed01;
            float naturalDrag = curveDrag * Mathf.Max(0.1f, baseDragMultiplier) * turnDragScale;
            currentSpeed -= naturalDrag * Time.fixedDeltaTime;

            // Clamp after all changes
            float adjustedMaxSpeed = maxSpeed * activeSpeedScale;
            currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, adjustedMaxSpeed);

            float lowSpeedFall01 = GetLowSpeedFallBlend();

            // Forward movement using current speed
            Vector3 targetForward = transform.forward * currentSpeed;
            if (lowSpeedFall01 > 0f)
                targetForward *= Mathf.Lerp(1f, lowSpeedForwardKeep, lowSpeedFall01);

            // Lift increases with speed
            float lift = Mathf.Clamp01(currentSpeed / maxSpeed) * liftStrength;
            if (lowSpeedFall01 > 0f)
                lift *= Mathf.Lerp(1f, 0.2f, lowSpeedFall01);
            Vector3 liftForce = transform.up * lift;

            // Extra turn-sensitive lateral damping makes steering feel more grounded.
            float dragAmount = curveDrag * Mathf.Max(0.1f, baseDragMultiplier);
            Vector3 dragForce = -rb.linearVelocity * dragAmount;
            Vector3 lateralVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.forward);
            float lateralDragAmount = Mathf.Max(0f, lateralDragStrength) * turnDragScale;
            Vector3 lateralDragForce = -lateralVelocity * lateralDragAmount;

            // Combined movement forces
            Vector3 finalVelocity = targetForward + liftForce + dragForce + lateralDragForce;
            if (lowSpeedFall01 > 0f)
            {
                float flutterPhase = Time.time * Mathf.Max(0f, lowSpeedFlutterFrequency);
                float flutter = Mathf.Sin(flutterPhase) * Mathf.Max(0f, lowSpeedFlutterStrength) * lowSpeedFall01;
                Vector3 downwardFall = Vector3.down * Mathf.Max(0f, lowSpeedFallStrength) * lowSpeedFall01;
                Vector3 flutterSway = transform.right * flutter;
                finalVelocity += downwardFall + flutterSway;
            }

            // Smooth velocity for less jitter
            rb.linearVelocity = Vector3.SmoothDamp(
                rb.linearVelocity,
                finalVelocity,
                ref smoothVel,
                0.15f
            );
        }

        private float GetLowSpeedFallBlend()
        {
            if (!enableLowSpeedPaperFall)
                return 0f;

            float start = minSpeed + Mathf.Max(0.01f, lowSpeedFallStartOffset);
            return 1f - Mathf.InverseLerp(minSpeed, start, currentSpeed);
        }

        // ROTATION & BANKING
        private void HandleRotation()
        {
            float lookX = input.LookInput.x; // Horizontal look input

            float controlFactor = 1f;

            // Harder to control when speeding up / softer when slowing down
            if (input.IsSpeedingUp)
                controlFactor = controlHardnessFast;    // Reduced steering power
            else if (input.IsSlowingDown)
                controlFactor = controlSoftnessSlow;    // Increased steering power

            float adjustedBankStrength = bankStrength * controlFactor * activeManeuverabilityMultiplier;

            // Banking left/right
            if (Mathf.Abs(lookX) > 0.01f)
                bank = Mathf.Lerp(bank, -lookX * adjustedBankStrength, Time.deltaTime * 4f);
            else
                bank = Mathf.Lerp(bank, 0, Time.deltaTime * bankReturnSpeed); // Return to center

            // Rotation based on camera pivot + banking
            Quaternion desiredRot = Quaternion.Euler(
                camPivot.eulerAngles.x,
                camPivot.eulerAngles.y,
                bank
            );

            // Smooth rotation
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                desiredRot,
                rotationSpeed * activeManeuverabilityMultiplier * Time.deltaTime
            );
        }

        private float GetTurnInput01()
        {
            if (input == null)
                return 0f;

            float horizontalTurn = Mathf.Abs(input.LookInput.x);
            float deadzone = Mathf.Clamp01(turnInputDeadzone);
            return Mathf.InverseLerp(deadzone, 1f, horizontalTurn);
        }
        #endregion
    }
}
