using UnityEngine;

public class CameraZoneTrigger : MonoBehaviour
{
    [SerializeField] private Vector3 overrideOffset = Vector3.zero;
    [SerializeField] private Vector3 overrideRotation = Vector3.zero;
    [SerializeField] private float transitionSpeed = 3f;
    [SerializeField] private bool lockXPosition;
    [SerializeField] private float lockedX;
    [SerializeField] private bool centerOnPlayer;
    [SerializeField] private string playerTag = "Player";
    private CameraFollow _cameraFollow;

    private void Start()
    {
        _cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (_cameraFollow == null) return;

        _cameraFollow.SetOverride(this, overrideOffset, overrideRotation, transitionSpeed, lockXPosition, lockedX, centerOnPlayer);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (_cameraFollow == null) return;

        _cameraFollow.ClearOverride(this, transitionSpeed);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            Gizmos.DrawCube(box.center, box.size);
        else
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
    }
}