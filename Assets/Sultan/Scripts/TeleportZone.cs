using UnityEngine;

public class TeleportZone : MonoBehaviour
{
    [SerializeField] private Vector3 offset;

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        pc.characterController.enabled = false;
        pc.transform.position += offset;
        pc.characterController.enabled = true;
    }
}