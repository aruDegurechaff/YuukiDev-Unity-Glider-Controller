using UnityEngine;
using YuukiDev.Input;
using static YuukiDev.Input.IGlideState;
using System;

namespace YuukiDev.Controller
{
    [DefaultExecutionOrder(-1)]
    public partial class PlayerController : MonoBehaviour
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
    }
}
