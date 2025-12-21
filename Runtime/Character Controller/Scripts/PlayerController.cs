using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YuukiDev.Input;

namespace YuukiDev.Controller
{
    [DefaultExecutionOrder(-1)]
    public class PlayerController : MonoBehaviour
    {
        [Header("Glide Speeds")]
        [SerializeField] private float baseSpeed = 3f;
        [SerializeField] private float maxSpeed = 45f;
        [SerializeField] private float minSpeed = 1.5f;
        [SerializeField] private float acceleration = 6f;

        [Header("Glide Forces")]
        [SerializeField] private float liftStrength = 12f;
        [SerializeField] private float thrustFactor = 18f;
        [SerializeField] private AnimationCurve dragCurve;

        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 5.5f;
        [SerializeField] private float bankStrength = 20f;
        [SerializeField] private float bankReturnSpeed = 1f;

        [Header("Speed Change Settings")]
        [SerializeField] private float speedUpMultiplier = 1.8f;
        [SerializeField] private float slowDownMultiplier = 0.65f;

        // Faster = harder to control Slower = easier to control
        [SerializeField] private float controlHardnessFast = 0.55f;
        [SerializeField] private float controlSoftnessSlow = 1.35f;

        [Header("Speed Boost")]
        [SerializeField] private float boostCapacity = 100f;
        [SerializeField] private float boostRegen = 10f;
        [SerializeField] private float drainRate = 20f;


        private Rigidbody rb;
        private YuukiPlayerInput input;
        private Transform camPivot;

        private float currentSpeed;
        private float currentBoost;
        private float bank;
        private Vector3 smoothVel;

        // Read Only
        public float MaxSpeed => maxSpeed;
        public float BoostNormalized => currentBoost / boostCapacity;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<YuukiPlayerInput>();

            if (Camera.main != null)
                camPivot = Camera.main.transform.parent;

            currentSpeed = baseSpeed;
            currentBoost = boostCapacity;
        }

        private void FixedUpdate()
        {
            HandleGlideMovement();
            HandleRotation();
        }

        //  GLIDING PHYSICS
        private void HandleGlideMovement()
        {
            // Pitch as -180 to 180
            float pitch = transform.eulerAngles.x;
            if (pitch > 180) pitch -= 360;
            float pitchRad = pitch * Mathf.Deg2Rad;

            // Downward pitch adds thrust
            float pitchAccel = Mathf.Sin(pitchRad) * thrustFactor;
            currentSpeed += pitchAccel * Time.fixedDeltaTime;

            // Natural air drag on speed (always)
            float naturalDrag = dragCurve.Evaluate(currentSpeed / maxSpeed);
            currentSpeed -= naturalDrag * Time.fixedDeltaTime;

            // Player-controlled speed assist (ONLY when pressing)
            if (input.IsSpeedingUp && currentBoost > 0f)
            {
                ConsumeBoosters();

                // Makes it so you accelerate towards the max speed
                float tempSpeed = currentSpeed / maxSpeed;
                float targetSpeed = Mathf.Lerp(
                    baseSpeed * speedUpMultiplier,
                    maxSpeed,
                    tempSpeed
                );

                float boostFactor = currentBoost / boostCapacity;
                float effectiveAcceleration = acceleration * boostFactor;

                currentSpeed = Mathf.MoveTowards(
                    currentSpeed,
                    targetSpeed,
                    effectiveAcceleration * Time.fixedDeltaTime
                );
            }
            else if (input.IsSlowingDown)
            {
                float targetSpeed = baseSpeed * slowDownMultiplier;
                currentSpeed = Mathf.MoveTowards(
                    currentSpeed,
                    targetSpeed,
                    acceleration * Time.fixedDeltaTime
                );
            }
            else if (!input.IsSpeedingUp)
            {
                float regenMultiplier = input.IsSlowingDown ? 1.75f : 1f;
                RegenBoosters(regenMultiplier);
            }

            // Clamp after all changes
            currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);

            // Forward movement using current speed
            Vector3 targetForward = transform.forward * currentSpeed;

            // Lift increases with speed
            float lift = Mathf.Clamp01(currentSpeed / maxSpeed) * liftStrength;
            Vector3 liftForce = transform.up * lift;

            // Drag curve scales with normalized speed MMMMMMMMMMMMMMM CURVESSSS
            float dragAmount = dragCurve.Evaluate(currentSpeed / maxSpeed);
            Vector3 dragForce = -rb.linearVelocity * dragAmount;

            // Combined movement forces
            Vector3 finalVelocity = targetForward + liftForce + dragForce;

            // Smooth velocity for less jitter
            rb.linearVelocity = Vector3.SmoothDamp(
                rb.linearVelocity,
                finalVelocity,
                ref smoothVel,
                0.15f
            );
        }

        public void ConsumeBoosters()
        {
            // Consume boost
            currentBoost -= Time.fixedDeltaTime * drainRate;
            currentBoost = Mathf.Max(currentBoost, 0f);

            Debug.Log($"[Consume] Boost: {currentBoost:F2}");
        }

        public float RegenBoosters(float Multiplier)
        {
            // Regen Boosters
            currentBoost += (boostRegen * Multiplier) * Time.fixedDeltaTime;
            currentBoost = Mathf.Min(currentBoost, boostCapacity);

            Debug.Log($"[Consume] Boost: {currentBoost:F2}");
            return currentBoost;
        }

        // ROTATION & BANKING
        private void HandleRotation()
        {
            float lookX = input.LookInput.x; // Horizontal look input

            float controlFactor = 1f;

            // Harder to control when speeding up / softer when slowing down
            if (input.IsSpeedingUp)
                controlFactor = controlHardnessFast;      // Reduced steering power
            else if (input.IsSlowingDown)
                controlFactor = controlSoftnessSlow;      // Increased steering power

            float adjustedBankStrength = bankStrength * controlFactor;

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
                rotationSpeed * Time.deltaTime
            );
        }
    }
}
