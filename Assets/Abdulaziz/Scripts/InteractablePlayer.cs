using UnityEngine;


public class InteractablePlayer : MonoBehaviour
{
    [Tooltip("Camera used to aim. Defaults to Camera.main.")]
    [SerializeField] private Camera viewCamera;

    [Tooltip("How far the player can reach.")]
    [SerializeField] private float interactRange = 3f;

    [Tooltip("Key to interact.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("Layers that hold interactable objects.")]
    [SerializeField] private LayerMask interactMask = ~0;

    private void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(interactKey)) return;

        Ray ray = viewCamera.ScreenPointToRay(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask)
            && hit.collider.TryGetComponent(out IInteractable interactable))
        {
            interactable.Interact();
        }
    }


    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;


    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : transform.position;
        Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
        Vector3 end = origin + dir * interactRange;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, 0.5f);
    }
}