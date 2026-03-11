using System.Collections.Generic;
using UnityEngine;
using YuukiDev.Input;

namespace YuukiDev.Controller
{
    /*
     * Collision and game-over handler
     * by: YuukiDev
     *
     * Isolates obstacle collision logic from core movement code.
     */
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
