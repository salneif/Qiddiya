using UnityEngine;

public class ZipLineSound : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private string playerTag = "Player";

    private bool _played;

    void Start()
    {
        if (source == null) source = GetComponent<AudioSource>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_played) return;
        if (!other.CompareTag(playerTag)) return;

        _played = true;
        source.Play();
    }
}