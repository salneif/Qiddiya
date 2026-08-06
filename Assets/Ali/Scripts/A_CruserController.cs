using UnityEngine;

public class A_CruserController : MonoBehaviour
{
    private void Start()
    {
        // Hide the cursor
        Cursor.visible = false;

        // Lock it to the center of the screen so it doesn't float around
        Cursor.lockState = CursorLockMode.Locked;
    }
}
