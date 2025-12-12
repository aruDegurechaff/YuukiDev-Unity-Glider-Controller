using UnityEngine;

namespace YuukiDev.OtherScripts
{
    public class MovementTracker : MonoBehaviour
    {
        private Rigidbody rb;

        public float CurrentSpeed { get; private set; }   // m/s

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        // Changed it to fix update
        void FixedUpdate()
        {
            // Track actual movement speed of the player
            CurrentSpeed = rb.linearVelocity.magnitude;
        }
    }
}