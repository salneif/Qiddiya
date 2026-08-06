using UnityEngine;

public class PlatformRiseStop : MonoBehaviour
{
    [SerializeField] private Collider stopTrigger;
    [SerializeField] private GameObject collider1;
    [SerializeField] private GameObject collider2;
    [SerializeField] private GameObject collider3;
    [SerializeField] private GameObject collider4;
    [SerializeField] private bool debug;

    private bool _blocked;

    void OnTriggerEnter(Collider other)
    {
        if (debug) Debug.Log($"[PlatformRiseStop] entered {other.name}", other);

        if (_blocked || other != stopTrigger) return;
        _blocked = true;

        collider1.SetActive(false);
        collider2.SetActive(false);
        collider3.SetActive(false);
        collider4.SetActive(false);
    }

    public bool Blocked => _blocked;
    public void ClearBlock() => _blocked = false;
}