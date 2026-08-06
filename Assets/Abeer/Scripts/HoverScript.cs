using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(AudioSource))]
public class ButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Audio Clips")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Header("Pitch")]
    [Range(0.1f, 3f)]
    public float hoverPitch = 1f;

    [Range(0.1f, 3f)]
    public float clickPitch = 1f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound != null)
        {
            audioSource.pitch = hoverPitch;
            audioSource.PlayOneShot(hoverSound);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null)
        {
            audioSource.pitch = clickPitch;
            audioSource.PlayOneShot(clickSound);
        }
    }
    public void OnPointerClick2(PointerEventData eventData)
{
    Debug.Log("Clicked!");

    if (clickSound != null)
    {
        audioSource.pitch = clickPitch;
        audioSource.PlayOneShot(clickSound);
    }
}
}