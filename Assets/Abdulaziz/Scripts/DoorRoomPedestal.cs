using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RoomDoor : MonoBehaviour
{
    [Tooltip("The pedestal that decides which room this door leads to.")]
    [SerializeField] private CubePedestal linkedPedestal;

    [Tooltip("Tag on the player object.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Seconds before the same object can be teleported again " +
             "(stops instant re-trigger loops at the destination).")]
    [SerializeField] private float teleportCooldown = 0.5f;

    private float lastTeleportTime = -999f;

    private void Reset()
    {

        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (Time.time - lastTeleportTime < teleportCooldown) return;

        if (linkedPedestal == null)
        {
            Debug.LogWarning($"{name}: no pedestal linked.", this);
            return;
        }

        Transform destination = linkedPedestal.CurrentDestination;
        if (destination == null)
        {
            Debug.LogWarning($"{name}: pedestal has no destination set.", this);
            return;
        }

        Teleport(other.transform, destination);
        lastTeleportTime = Time.time;
    }

    private void Teleport(Transform player, Transform destination)
    {

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.SetPositionAndRotation(destination.position, destination.rotation);

        if (cc != null) cc.enabled = true;
    }



    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        DrawTriggerVolume();

        if (linkedPedestal != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, linkedPedestal.transform.position);
        }

        // Green line: where the door currently leads.
        if (linkedPedestal != null && linkedPedestal.CurrentDestination != null)
        {
            Vector3 dest = linkedPedestal.CurrentDestination.position;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, dest);
            Gizmos.DrawWireSphere(dest, 0.25f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, "Door");
#endif
    }

    private void DrawTriggerVolume()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;


        Color fill = new Color(0f, 1f, 1f, 0.15f);
        Color wire = Color.cyan;

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = fill; Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = wire; Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = fill; Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = wire; Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else
        {

            Gizmos.color = wire;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}