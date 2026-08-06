using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ReturnColor : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private float targetSaturation = -78f;
    private ColorAdjustments colorAdjustments;

    private void Start()
    {
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out colorAdjustments);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            ChangeSaturation(targetSaturation);
        }
    }

    private void ChangeSaturation(float value)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = value;
        }
    }
}
