using UnityEngine;
using UnityEngine.EventSystems;

public class HoverScript : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Audio Source")]
    public AudioSource uiAudioSource;

    [Header("Audio Clips")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Header("Pitch")]
    [Range(0.1f, 3f)]
    public float hoverPitch = 1f;

    [Range(0.1f, 3f)]
    public float clickPitch = 1f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (uiAudioSource != null && hoverSound != null)
        {
            uiAudioSource.pitch = hoverPitch;
            uiAudioSource.PlayOneShot(hoverSound);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (uiAudioSource != null && clickSound != null)
        {
            uiAudioSource.pitch = clickPitch;
            uiAudioSource.PlayOneShot(clickSound);
        }
    }
}