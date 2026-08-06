using UnityEngine;

public class RespawnTrigger : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool applyPointRotation = true;
    [SerializeField] private float retriggerDelay = 0.5f;

    private float _cooldown;

    void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_cooldown > 0f) return;
        if (!other.CompareTag(playerTag)) return;

        if (respawnPoint == null)
        {
            Debug.LogWarning("[Respawn] No respawn point assigned.", this);
            return;
        }

        respawn(other);
    }

    void respawn(Collider other)
    {
        _cooldown = retriggerDelay;

        CharacterController cc = other.GetComponentInParent<CharacterController>();
        Transform t = cc != null ? cc.transform : other.transform;
        PlayerController player = t.GetComponent<PlayerController>();

        if (player != null) player.SetInputEnabled(false);

        if (cc != null) cc.enabled = false;

        t.position = respawnPoint.position;
        if (applyPointRotation) t.rotation = respawnPoint.rotation;

        if (cc != null) cc.enabled = true;

        if (player != null) player.SetInputEnabled(true);
    }
}