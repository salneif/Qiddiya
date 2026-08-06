using UnityEngine;

public class PlatformRiseStop : MonoBehaviour
{
    [SerializeField] private Collider stopTrigger;

    private bool _blocked;

    void OnTriggerEnter(Collider other)
    {
        if (_blocked || other != stopTrigger) return;
        _blocked = true;
    }

    public bool Blocked => _blocked;
    public void ClearBlock() => _blocked = false;
}