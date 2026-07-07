using UnityEngine;

public class PlayerRotation : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;

    private void LateUpdate()
    {
        // get the y rotation becuse in first person we want only the y for now 
        float cameraY = cameraTransform.eulerAngles.y;

        transform.rotation = Quaternion.Euler(0, cameraY, 0);

    }
}
