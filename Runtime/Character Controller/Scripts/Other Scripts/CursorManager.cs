using UnityEngine;

namespace YuukiDev.OtherScripts
{
    /*
     * Cursor state controller
     * by: YuukiDev
     *
     * Centralizes lock and visibility behavior for gameplay and menus.
     */
    public class CursorManager : MonoBehaviour
    {
        [SerializeField] private bool hideCursor = true;

        private void Start()
        {
            SetCursorState(hideCursor);
        }

        public void SetCursorState(bool hide)
        {
            if (hide)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
