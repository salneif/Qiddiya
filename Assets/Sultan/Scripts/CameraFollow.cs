using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 3.8f, -14f);
    [SerializeField] private Vector3 cameraRotation = new Vector3(3f, 0f, 0f);
    [SerializeField] private float smoothSpeed = 7f;
    [SerializeField] private bool snapOnStart = true;
    [SerializeField] private float roomMinX = -10f;
    [SerializeField] private float roomMaxX = 10f;
    [SerializeField] private bool autoCalculateBounds = true;

    private float clampMinX;
    private float clampMaxX;

    private void Start()
    {
        transform.eulerAngles = cameraRotation;
        RecalculateClampLimits();

        if (snapOnStart && target != null)
        {
            float snappedX = Mathf.Clamp(target.position.x + offset.x, clampMinX, clampMaxX);
            transform.position = new Vector3(snappedX, offset.y, offset.z);
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float desiredX = Mathf.Clamp(target.position.x + offset.x, clampMinX, clampMaxX);
        Vector3 desiredPos = new Vector3(desiredX, offset.y, offset.z);

        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void RecalculateClampLimits()
    {
        if (!autoCalculateBounds)
        {
            clampMinX = roomMinX;
            clampMaxX = roomMaxX;
            return;
        }

        Camera cam = GetComponent<Camera>();
        if (cam == null)
        {
            clampMinX = roomMinX;
            clampMaxX = roomMaxX;
            return;
        }

        float distToGameplay = Mathf.Abs(offset.z);
        float visibleHalfWidth;

        if (cam.orthographic)
        {
            visibleHalfWidth = cam.orthographicSize * cam.aspect;
        }
        else
        {
            float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            visibleHalfWidth = distToGameplay * Mathf.Tan(halfFovRad) * cam.aspect;
        }

        clampMinX = roomMinX + visibleHalfWidth;
        clampMaxX = roomMaxX - visibleHalfWidth;

        if (clampMinX > clampMaxX)
        {
            float center = (roomMinX + roomMaxX) * 0.5f;
            clampMinX = center;
            clampMaxX = center;
        }
    }


    private void OnDrawGizmosSelected()
    {
        float y = offset.y;
        float z = offset.z;

        RecalculateClampLimits();

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        Gizmos.DrawLine(new Vector3(clampMinX, y - 3f, z), new Vector3(clampMinX, y + 3f, z));
        Gizmos.DrawLine(new Vector3(clampMaxX, y - 3f, z), new Vector3(clampMaxX, y + 3f, z));
        Gizmos.DrawLine(new Vector3(clampMinX, y, z), new Vector3(clampMaxX, y, z));

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
        Gizmos.DrawLine(new Vector3(roomMinX, y - 4f, z), new Vector3(roomMinX, y + 4f, z));
        Gizmos.DrawLine(new Vector3(roomMaxX, y - 4f, z), new Vector3(roomMaxX, y + 4f, z));
    }
}
