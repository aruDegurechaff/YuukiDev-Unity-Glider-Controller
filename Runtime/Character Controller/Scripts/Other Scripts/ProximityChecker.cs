using UnityEngine;

/*
 * Yung pwede natin gawin dito is since may bool declaration, 
 * pwede natin gawin sa scoring na script is multipliers ex:
 * 
 * using YuukiDev.OtherScripts;
 * 
 * ProximityChecker prox = player.GetComponent<ProximityChecker>();

    if (prox.IsClose)
    {
        score * 3;
    }
 * 
 * Also don't forget to set the obstacles to the tag "Obstacle"
 */
namespace YuukiDev.OtherScripts
{
    public class ProximityChecker : MonoBehaviour
    {
        [Header("Ranges")]
        public float closeRange = 3f;
        public float midRange = 6f;
        public float farRange = 10f;

        // Public read-only properties.
        public bool IsClose { get; private set; }
        public bool IsMid { get; private set; }
        public bool IsFar { get; private set; }

        void Update()
        {
            DetectProximity();
        }

        private void DetectProximity()
        {
            // Reset states
            IsClose = IsMid = IsFar = false;

            GameObject[] obstacles = GameObject.FindGameObjectsWithTag("Obstacle");

            foreach (GameObject obstacle in obstacles)
            {
                float distance = Vector3.Distance(transform.position, obstacle.transform.position);

                if (distance <= closeRange)
                {
                    IsClose = true;
                }
                else if (distance <= midRange)
                {
                    IsMid = true;
                }
                else if (distance <= farRange)
                {
                    IsFar = true;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, closeRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, midRange);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, farRange);
        }
    }

}