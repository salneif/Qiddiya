using UnityEngine;

public class CameraZoneTrigger : MonoBehaviour
{
    public enum ShotMode { Relative, Absolute }

    [SerializeField] private ShotMode mode = ShotMode.Relative;
    [SerializeField] private Vector3 overrideOffset = Vector3.zero;
    [SerializeField] private Vector3 overrideRotation = Vector3.zero;
    [SerializeField] private Vector3 absolutePosition = Vector3.zero;
    [SerializeField] private Vector3 absoluteRotation = Vector3.zero;
    [SerializeField] private float transitionSpeed = 3f;
    [SerializeField] private bool lockXPosition;
    [SerializeField] private float lockedX;
    [SerializeField] private bool centerOnPlayer;
    [SerializeField] private bool freezeX;
    [SerializeField] private bool freezeZ;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool followPlayerY;
    private CameraFollow _cameraFollow;

    private void Start()
    {
        _cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (_cameraFollow == null) return;

        _cameraFollow.SetOverride(this, overrideOffset, overrideRotation, transitionSpeed, lockXPosition, lockedX, centerOnPlayer, followPlayerY, freezeX, freezeZ, mode == ShotMode.Absolute, absolutePosition, absoluteRotation);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (_cameraFollow == null) return;

        _cameraFollow.ClearOverride(this, transitionSpeed);
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Shot From Camera")]
    private void captureShotFromCamera()
    {
        CameraFollow cam = FindFirstObjectByType<CameraFollow>();

        if (cam == null)
        {
            return;
        }

        UnityEditor.Undo.RecordObject(this, "Capture Camera Shot");

        mode = ShotMode.Absolute;
        absolutePosition = cam.transform.position;
        absoluteRotation = cam.transform.eulerAngles;

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            Gizmos.DrawCube(box.center, box.size);
        else
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

        if (mode != ShotMode.Absolute) return;

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(absolutePosition, 0.5f);
        Gizmos.DrawRay(absolutePosition, Quaternion.Euler(absoluteRotation) * Vector3.forward * 4f);

        if (followPlayerY)
            Gizmos.DrawLine(absolutePosition + Vector3.down * 4f, absolutePosition + Vector3.up * 4f);
    }
}