using UnityEngine;

/*
 * New version that utilizes triggers...
 */
namespace YuukiDev.OtherScripts
{
    public class ProximityChecker : MonoBehaviour
    {
        public bool IsClose { get; private set; }
        public bool IsMid { get; private set; }
        public bool IsFar { get; private set; }

        private int closeCount = 0;
        private int midCount = 0;
        private int farCount = 0;

        public void UpdateCount(string rangeType, bool entering)
        {
            int delta = entering ? 1 : -1;

            switch (rangeType)
            {
                case "Close":
                    closeCount += delta;
                    break;

                case "Mid":
                    midCount += delta;
                    break;

                case "Far":
                    farCount += delta;
                    break;
            }

            // Update the booleans
            IsClose = closeCount > 0;
            IsMid = midCount > 0;
            IsFar = farCount > 0;
        }
    }
}