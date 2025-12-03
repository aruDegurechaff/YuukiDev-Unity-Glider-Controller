using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace YuukiDev.Controller
{
    [DefaultExecutionOrder(-1)]
    public class PlayerController : MonoBehaviour
    {
        [Header("Glide Speeds")]
        [SerializeField] private float baseSpeed = 25f;
        [SerializeField] private float maxSpeed = 45f;
        [SerializeField] private float minSpeed = 5f;

        [Header("Glide Forces")]
        [SerializeField] private float liftStrength = 8f;
        [SerializeField] private float thrustFactor = 10f;
        [SerializeField] private AnimationCurve dragCurve;

        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 4f;
        [SerializeField] private float bankStrength = 55f;
        [SerializeField] private float bankReturnSpeed = 2f;

        private Rigidbody rb;
        private YuukiPlayerInput input;
        private Transform camPivot;

        private float currentSpeed;
        private float bank;
        private Vector3 smoothVel;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<YuukiPlayerInput>();

            if (Camera.main != null)
                camPivot = Camera.main.transform.parent;

            currentSpeed = baseSpeed;
        }

        private void Update()
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

            HandleRotation();
        }

        private void FixedUpdate()
        {
            HandleGlideMovement();
        }

        //  GLIDING PHYSICS
        private void HandleGlideMovement()
        {
            // Pitch as -180 to 180
            float pitch = transform.eulerAngles.x;
            if (pitch > 180) pitch -= 360;

            float pitchRad = pitch * Mathf.Deg2Rad;

            // Thrust from pitching downward
            float pitchAccel = Mathf.Sin(pitchRad) * thrustFactor;

            // Adjust speed
            currentSpeed += pitchAccel * Time.fixedDeltaTime;
            currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);

            // Forward movement
            Vector3 targetForward = transform.forward * currentSpeed;

            // Lift force (more speed = more lift)
            float lift = Mathf.Clamp01(currentSpeed / maxSpeed) * liftStrength;
            Vector3 liftForce = transform.up * lift;

            // Drag based on speed (curve gives nicer realism I'm horny)
            float dragAmount = dragCurve.Evaluate(currentSpeed / maxSpeed);
            Vector3 dragForce = -rb.linearVelocity * dragAmount;

            // Final velocity
            Vector3 finalVelocity = targetForward + liftForce + dragForce;

            rb.linearVelocity = Vector3.SmoothDamp(
                rb.linearVelocity,
                finalVelocity,
                ref smoothVel,
                0.15f
            );
        }

        //  ROTATION & BANKING
        private void HandleRotation()
        {
            float lookX = input.LookInput.x;

            // Banking left/right
            if (Mathf.Abs(lookX) > 0.01f)
            {
                bank = Mathf.Lerp(bank, -lookX * bankStrength, Time.deltaTime * 4f);
            }
            else
            {
                bank = Mathf.Lerp(bank, 0, Time.deltaTime * bankReturnSpeed);
            }

            // Apply rotation based on camera
            Quaternion desiredRot = Quaternion.Euler(
                camPivot.eulerAngles.x,
                camPivot.eulerAngles.y,
                bank
            );

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                desiredRot,
                rotationSpeed * Time.deltaTime
            );
        }
    }
}
