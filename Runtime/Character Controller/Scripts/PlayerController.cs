using UnityEngine;
using YuukiDev.Input;
using static YuukiDev.Input.IGlideState;
using System;
using System.Collections.Generic;

namespace YuukiDev.Controller
{
    [DefaultExecutionOrder(-1)]
    public class PlayerController : MonoBehaviour
    {
        public enum GlideMode
        {
            Normal,
            SpeedingUp,
            SlowingDown
        }

        #region Variables
        [Header("Glide Speeds")]
        [SerializeField] private float baseSpeed = 3f;
        [SerializeField] private float maxSpeed = 45f;
        [SerializeField] private float minSpeed = 1.5f;
        [SerializeField] private float acceleration = 6f;

        [Header("Glide Forces")]
        [SerializeField] private float liftStrength = 12f;
        [SerializeField] private float thrustFactor = 18f;
        [SerializeField] private float diveAccelerationMultiplier = 1.1f;
        [SerializeField, Range(0.5f, 1f)] private float climbDecelerationMultiplier = 0.9f;
        [SerializeField] private AnimationCurve dragCurve;
        
        [Header("Aerodynamic Drag Tuning")]
        [SerializeField] private float baseDragMultiplier = 1f;
        [SerializeField] private float turnDragMultiplier = 1.35f;
        [SerializeField] private float lateralDragStrength = 2.2f;
        [SerializeField] private float turnInputDeadzone = 0.08f;

        [Header("Low Speed Fall")]
        [SerializeField] private bool enableLowSpeedPaperFall = true;
        [SerializeField] private float lowSpeedFallStartOffset = 0.45f;
        [SerializeField] private float lowSpeedFallStrength = 5.5f;
        [SerializeField, Range(0.1f, 1f)] private float lowSpeedForwardKeep = 0.55f;
        [SerializeField] private float lowSpeedFlutterStrength = 0.65f;
        [SerializeField] private float lowSpeedFlutterFrequency = 3.5f;

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
        [SerializeField] private float minBoostToStart = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minRegenFromEmpty = 0.12f;
        [SerializeField] private BoostSettings boostSettings;

        [Header("Power Up Effects")]
        [SerializeField] private float pickupSpeedPulse = 2f;
        [SerializeField] private float pickupBoostRefillPercent = 0.08f;

        [Header("Collisions & Game Over")]
        [SerializeField] private bool onlyObstacleCollisionsAreLethal = true;
        [SerializeField] private string obstacleTag = "Obstacle";
        [SerializeField] private bool freezeTimeOnGameOver = true;
        [SerializeField] private float reviveCollisionImmunityDuration = 1f;
        [SerializeField] private float revivePhantomGraceDuration = 7f;

        [Header("Dynamic Collider Width")]
        [SerializeField] private BoxCollider dynamicWidthCollider;
        [SerializeField, Range(0.2f, 1f)] private float speedUpWidthMultiplier = 0.75f;
        [SerializeField] private float slowDownWidthMultiplier = 1.25f;
        [SerializeField] private float colliderWidthTransitionSpeed = 6f;
        private const float EmptyStaminaRegenFallback = 0.12f;

        private Rigidbody rb;
        private YuukiPlayerInput input;
        private Transform camPivot;
        private CameraFollowAndRotate cameraFollow;

        private float currentSpeed;
        private float currentBoost;
        private float regenTimer = 0f;
        private float bank;
        private Vector3 smoothVel;
        private float activeSpeedScale = 1f;
        private float windOrbTimer = 0f;
        private float windOrbDurationTotal = 0f;
        private float activeManeuverabilityMultiplier = 1f;
        private float featherSlipTimer = 0f;
        private float featherSlipDurationTotal = 0f;
        private float lanternSparksTimer = 0f;
        private float lanternSparksDurationTotal = 0f;
        private float phantomGraceTimer = 0f;
        private float phantomGraceDurationTotal = 0f;
        private PlayerCollisionAndGameOverHandler collisionGameOverHandler;
        private float defaultColliderWidth = 1f;
        private bool hasDynamicWidthCollider = false;

        private IGlideState currentState;

        private BoostingGlideState boostingState;
        private SlowGlideState slowState;
        public NormalGlideState NormalState;
        public bool canBoost = true;
        public static event Action<PlayerController> GameOverTriggered;
        public event Action<GlideMode> GlideModeChanged;

        // Read Only... Use when you need a reference for something.
        public float MaxSpeed => maxSpeed;
        public float BoostNormalized => Mathf.Clamp01(currentBoost / GetBoostCapacity());
        public float CurrentBoost => currentBoost;
        public bool IsGameOver => collisionGameOverHandler != null && collisionGameOverHandler.IsGameOver;
        public bool IsPhantomGraceActive => phantomGraceTimer > 0f;
        public bool IsFeatherSlipActive => featherSlipTimer > 0f;
        public float WindOrbRemaining => Mathf.Max(0f, windOrbTimer);
        public float WindOrbNormalized => windOrbDurationTotal > 0.001f ? Mathf.Clamp01(windOrbTimer / windOrbDurationTotal) : 0f;
        public float FeatherSlipRemaining => Mathf.Max(0f, featherSlipTimer);
        public float FeatherSlipNormalized => featherSlipDurationTotal > 0.001f ? Mathf.Clamp01(featherSlipTimer / featherSlipDurationTotal) : 0f;
        public float LanternSparksRemaining => Mathf.Max(0f, lanternSparksTimer);
        public float LanternSparksNormalized => lanternSparksDurationTotal > 0.001f ? Mathf.Clamp01(lanternSparksTimer / lanternSparksDurationTotal) : 0f;
        public float PhantomGraceRemaining => Mathf.Max(0f, phantomGraceTimer);
        public float PhantomGraceNormalized => phantomGraceDurationTotal > 0.001f ? Mathf.Clamp01(phantomGraceTimer / phantomGraceDurationTotal) : 0f;
        public float SpeedNormalized => Mathf.InverseLerp(minSpeed, maxSpeed * activeSpeedScale, currentSpeed);
        public GlideMode CurrentGlideMode { get; private set; } = GlideMode.Normal;
        #endregion

        #region Updates and Init
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<YuukiPlayerInput>();
            ResolveDynamicWidthCollider();
            ResolveCollisionGameOverHandler();

            if (Camera.main != null)
            {
                camPivot = Camera.main.transform.parent;
                cameraFollow = Camera.main.GetComponentInParent<CameraFollowAndRotate>();
            }

            currentSpeed = baseSpeed;
            currentBoost = boostSettings != null ? GetBoostCapacity() : 0f;
            RefreshBoostAvailability();

            NormalState = new NormalGlideState();
            boostingState = new BoostingGlideState();
            slowState = new SlowGlideState();

            SwitchState(NormalState);

        }

        private void FixedUpdate()
        {
            if (collisionGameOverHandler != null)
                collisionGameOverHandler.Tick(Time.fixedDeltaTime);

            if (IsGameOver)
                return;

            UpdateTimedEffects();
            UpdateState();
            UpdateDynamicColliderWidth();
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
            bool boostRequestedAndAvailable = input.IsSpeedingUp && CanEnterBoostState();

            IGlideState targetState =
                boostRequestedAndAvailable ? boostingState :
                input.IsSlowingDown ? slowState :
                NormalState;

            GlideMode targetMode =
                boostRequestedAndAvailable ? GlideMode.SpeedingUp :
                input.IsSlowingDown ? GlideMode.SlowingDown :
                GlideMode.Normal;

            if (targetMode != CurrentGlideMode)
            {
                CurrentGlideMode = targetMode;
                GlideModeChanged?.Invoke(CurrentGlideMode);
            }

            if (targetState != currentState)
                SwitchState(targetState);
        }
        #endregion

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

        private float GetTurnInput01()
        {
            if (input == null)
                return 0f;

            float horizontalTurn = Mathf.Abs(input.LookInput.x);
            float deadzone = Mathf.Clamp01(turnInputDeadzone);
            return Mathf.InverseLerp(deadzone, 1f, horizontalTurn);
        }

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

        private void ResolveCollisionGameOverHandler()
        {
            collisionGameOverHandler = GetComponent<PlayerCollisionAndGameOverHandler>();
            if (collisionGameOverHandler == null)
                collisionGameOverHandler = gameObject.AddComponent<PlayerCollisionAndGameOverHandler>();

            collisionGameOverHandler.Configure(
                onlyObstacleCollisionsAreLethal,
                obstacleTag,
                freezeTimeOnGameOver,
                reviveCollisionImmunityDuration);
            collisionGameOverHandler.Initialize(this, rb, input);
        }

        #endregion
    }

    [DisallowMultipleComponent]
    public class PlayerCollisionAndGameOverHandler : MonoBehaviour
    {
        private bool onlyObstacleCollisionsAreLethal = true;
        private string obstacleTag = "Obstacle";
        private bool freezeTimeOnGameOver = true;
        private float reviveCollisionImmunityDuration = 1f;

        private PlayerController owner;
        private Rigidbody rb;
        private YuukiPlayerInput input;
        private Collider[] playerColliders;
        private readonly List<Collider> ignoredObstacleColliders = new List<Collider>();
        private float reviveCollisionImmunityTimer;
        private bool isGameOver;

        public bool IsGameOver => isGameOver;

        public void Configure(
            bool onlyObstacleLethal,
            string obstacleTagName,
            bool freezeTime,
            float reviveImmunityDuration)
        {
            onlyObstacleCollisionsAreLethal = onlyObstacleLethal;
            obstacleTag = string.IsNullOrWhiteSpace(obstacleTagName) ? "Obstacle" : obstacleTagName;
            freezeTimeOnGameOver = freezeTime;
            reviveCollisionImmunityDuration = Mathf.Max(0f, reviveImmunityDuration);
        }

        public void Initialize(PlayerController playerController, Rigidbody rigidBody, YuukiPlayerInput playerInput)
        {
            owner = playerController;
            rb = rigidBody;
            input = playerInput;
            playerColliders = GetComponentsInChildren<Collider>(true);
        }

        public void Tick(float deltaTime)
        {
            if (reviveCollisionImmunityTimer <= 0f)
                return;

            reviveCollisionImmunityTimer -= deltaTime;
            if (reviveCollisionImmunityTimer < 0f)
                reviveCollisionImmunityTimer = 0f;
        }

        public bool RecoverFromGameOverState()
        {
            if (!isGameOver)
                return false;

            isGameOver = false;

            if (input != null)
                input.enabled = true;

            ZeroBodyVelocity();
            RestoreIgnoredObstacleCollisions();

            if (freezeTimeOnGameOver && Time.timeScale <= 0f)
                Time.timeScale = 1f;

            return true;
        }

        public void BeginReviveCollisionImmunity()
        {
            reviveCollisionImmunityTimer = Mathf.Max(0f, reviveCollisionImmunityDuration);
        }

        public void ResetRuntimeState()
        {
            reviveCollisionImmunityTimer = 0f;
            RestoreIgnoredObstacleCollisions();
        }

        public void IgnoreAllObstacleCollisionsInScene()
        {
            if (playerColliders == null)
                return;

            Collider[] sceneColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            for (int i = 0; i < sceneColliders.Length; i++)
            {
                Collider candidate = sceneColliders[i];
                if (candidate == null || !IsObstacle(candidate))
                    continue;

                IgnoreObstacleCollision(candidate);
            }
        }

        public void RestoreIgnoredObstacleCollisions()
        {
            if (playerColliders == null || ignoredObstacleColliders.Count == 0)
                return;

            for (int i = ignoredObstacleColliders.Count - 1; i >= 0; i--)
            {
                Collider obstacleCollider = ignoredObstacleColliders[i];
                if (obstacleCollider == null)
                {
                    ignoredObstacleColliders.RemoveAt(i);
                    continue;
                }

                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Collider playerCollider = playerColliders[j];
                    if (playerCollider == null)
                        continue;

                    if (playerCollider.isTrigger)
                        continue;

                    Physics.IgnoreCollision(playerCollider, obstacleCollider, false);
                }

                ignoredObstacleColliders.RemoveAt(i);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (owner == null || isGameOver || collision == null || collision.collider == null)
                return;

            bool hitObstacle = IsObstacle(collision.collider);
            if (owner.IsPhantomGraceActive && hitObstacle)
            {
                IgnoreObstacleCollision(collision.collider);
                return;
            }

            if (hitObstacle && reviveCollisionImmunityTimer > 0f)
                return;

            if (onlyObstacleCollisionsAreLethal && !hitObstacle)
                return;

            EnterGameOverState();
        }

        private void EnterGameOverState()
        {
            if (isGameOver)
                return;

            isGameOver = true;

            if (input != null)
                input.enabled = false;

            ZeroBodyVelocity();
            RestoreIgnoredObstacleCollisions();
            owner.NotifyGameOverTriggeredByCollision();

            if (freezeTimeOnGameOver)
                Time.timeScale = 0f;
        }

        private void ZeroBodyVelocity()
        {
            if (rb == null)
                return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private bool IsObstacle(Collider target)
        {
            if (target == null)
                return false;

            Transform root = target.transform.root;
            return target.CompareTag(obstacleTag) || (root != null && root.CompareTag(obstacleTag));
        }

        private void IgnoreObstacleCollision(Collider obstacleCollider)
        {
            if (obstacleCollider == null || playerColliders == null)
                return;

            Transform obstacleRoot = obstacleCollider.transform.root;
            bool rootTaggedObstacle = obstacleRoot != null && obstacleRoot.CompareTag(obstacleTag);
            Collider[] obstacleColliders = rootTaggedObstacle
                ? obstacleRoot.GetComponentsInChildren<Collider>(true)
                : new[] { obstacleCollider };

            for (int i = 0; i < obstacleColliders.Length; i++)
            {
                Collider targetCollider = obstacleColliders[i];
                if (targetCollider == null)
                    continue;

                if (targetCollider.isTrigger)
                    continue;

                if (!rootTaggedObstacle && !targetCollider.CompareTag(obstacleTag))
                    continue;

                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Collider playerCollider = playerColliders[j];
                    if (playerCollider == null)
                        continue;

                    if (playerCollider.isTrigger)
                        continue;

                    Physics.IgnoreCollision(playerCollider, targetCollider, true);
                }

                if (!ignoredObstacleColliders.Contains(targetCollider))
                    ignoredObstacleColliders.Add(targetCollider);
            }
        }
    }
}
