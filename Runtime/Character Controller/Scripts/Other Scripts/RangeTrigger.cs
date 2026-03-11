using UnityEngine;

namespace YuukiDev.OtherScripts
{
    /*
     * Range trigger relay
     * by: YuukiDev
     *
     * Sends proximity enter/exit events to the main checker.
     */
    public class RangeTrigger : MonoBehaviour
    {
        public string rangeType; // "Close" / "Mid" / "Far"
        private ProximityChecker checker;

        private void Start()
        {
            checker = GetComponentInParent<ProximityChecker>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Obstacle"))
            {
                checker.UpdateCount(rangeType, true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Obstacle"))
            {
                checker.UpdateCount(rangeType, false);
            }
        }
    }
}
