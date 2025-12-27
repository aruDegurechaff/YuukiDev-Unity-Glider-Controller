using UnityEngine;
using YuukiDev.Input;
using static YuukiDev.Input.IGlideState;

namespace YuukiDev.Controller
{
    [DefaultExecutionOrder(-1)]
    public class PlayerController : MonoBehaviour
    {
        #region Variables
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
        [SerializeField] private float regenDelay = 0.25f;
        [SerializeField] private BoostSettings boostSettings;

        private Rigidbody rb;
        private YuukiPlayerInput input;
        private Transform camPivot;

        private float currentSpeed;
        private float currentBoost;
        private float regenTimer = 0f;
        private float bank;
        private Vector3 smoothVel;

        private IGlideState currentState;

        private BoostingGlideState boostingState;
        private SlowGlideState slowState;
        public NormalGlideState NormalState;
        public bool canBoost = true;

        // Read Only... Use when you need a reference for something.
        public float MaxSpeed => maxSpeed;
        public float BoostNormalized => currentBoost / boostSettings.capacity;
        public float CurrentBoost => currentBoost;
        #endregion

        #region Updates and Init
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<YuukiPlayerInput>();

            if (Camera.main != null)
                camPivot = Camera.main.transform.parent;

            currentSpeed = baseSpeed;
            currentBoost = boostSettings.capacity;

            NormalState = new NormalGlideState();
            boostingState = new BoostingGlideState();
            slowState = new SlowGlideState();

            SwitchState(NormalState);

        }

        private void FixedUpdate()
        {
            UpdateState();
            currentState.Tick(this);
            HandleRotation();
        }

        #endregion

        #region State stuff
        // Helper
        public void SwitchState(IGlideState newState)
        {
            currentState?.Exit(this);
            currentState = newState;
            currentState.Enter(this);
        }

        // State Selector
        private void UpdateState()
        {
            IGlideState targetState =
                input.IsSpeedingUp && canBoost ? boostingState :
                input.IsSlowingDown ? slowState :
                NormalState;

            if (targetState != currentState)
                SwitchState(targetState);
        }
        #endregion

        #region Movement
        // Physics
        public void ApplyNaturalMovement()
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

        public void ConsumeBoosters()
        {
            currentBoost -= boostSettings.drainRate * Time.fixedDeltaTime;
            currentBoost = Mathf.Max(currentBoost, 0f);

            // Reset regen cooldown whenever boost is used
            regenTimer = regenDelay;
        }

        public void RegenBoosters(float multiplier)
        {
            float normalized = Mathf.Max(currentBoost / boostSettings.capacity, 0.01f);
            float curveValue = boostSettings.regenCurve.Evaluate(normalized);

            float speedFactor = currentSpeed / baseSpeed; // normalized movement influence

            // In this version the regen is also faster the faster the character naturally moves... High risk, high reward.
            float regenAmount =
                Mathf.Max(curveValue, 0f) *
                boostSettings.regenRate *
                multiplier *
                speedFactor *
                Time.fixedDeltaTime;

            currentBoost = Mathf.Clamp(currentBoost + regenAmount, 0f, boostSettings.capacity);

            // Re-enable boosting once enough has regenerated
            if (currentBoost > 0.1f)
                canBoost = true;
        }


        public void ApplyBoostAcceleration()
        {
            // Makes it so you accelerate towards the max speed
            float tempSpeed = currentSpeed / maxSpeed;
            float targetSpeed = Mathf.Lerp(
                baseSpeed * speedUpMultiplier,
                maxSpeed,
                tempSpeed
            );

            float boostFactor = currentBoost / boostSettings.capacity;
            float effectiveAcceleration = acceleration * boostFactor;

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                effectiveAcceleration * Time.fixedDeltaTime
            );
        }

        public void ApplySlowDown()
        {
            float targetSpeed = baseSpeed * slowDownMultiplier;
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                acceleration * Time.fixedDeltaTime
            );
        }
        #endregion
    }
}
