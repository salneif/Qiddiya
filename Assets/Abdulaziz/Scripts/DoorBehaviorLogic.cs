using UnityEngine;


[RequireComponent(typeof(Collider))]
public class DoorBehaviorLogic : MonoBehaviour
{
    [Tooltip("The spawn point this door sends the player to " +
             "(e.g. an empty GameObject placed in the original room).")]
    [SerializeField] private Transform destination;

    [Tooltip("Tag on the player object.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Seconds before the same object can be teleported again.")]
    [SerializeField] private float teleportCooldown = 0.5f;

    private float _lastTeleportTime = -999f;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (Time.time - _lastTeleportTime < teleportCooldown) return;

        if (destination == null)
        {
            return;
        }

        Teleport(other.transform, destination);
        _lastTeleportTime = Time.time;
    }

    private void Teleport(Transform player, Transform dest)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.SetPositionAndRotation(dest.position, dest.rotation);

        if (cc != null) cc.enabled = true;
    }


    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        DrawTriggerVolume();

        if (destination != null)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawLine(transform.position, destination.position);
            Gizmos.DrawWireSphere(destination.position, 0.25f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(destination.position, destination.forward);
        }
    }

    private void DrawTriggerVolume()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Color fill = new Color(1f, 0.5f, 0f, 0.15f);
        Color wire = new Color(1f, 0.5f, 0f, 1f);

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