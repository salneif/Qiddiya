using UnityEngine;

public class LavaTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        PlayerKillable _player = other.GetComponent<PlayerKillable>();

        if (_player == null) return;

        _player.Kill();

    }
}