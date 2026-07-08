using UnityEngine;

public class LavaTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private PlayerController _player;
    private bool _triggered;

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag(playerTag)) return;

        _player = other.GetComponent<PlayerController>();
        if (_player == null) return;

        _triggered = true;
        //BELOW LINE TO BE COMMENTED OUT
        _player.SetInputEnabled(false);
    }

    // FUNCTION TO BE COMMENTED OUT
    void OnDisable()
    {
        if (_triggered && _player != null)
            _player.SetInputEnabled(true);
        _triggered = false;
        _player = null;
    }

    public bool IsTriggered => _triggered;
}