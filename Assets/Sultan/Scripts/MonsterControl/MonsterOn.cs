using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MonsterOn : MonoBehaviour
{
    [SerializeField] private MonoBehaviour scriptOne;
    [SerializeField] private MonoBehaviour scriptTwo;
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
            if (scriptOne != null) scriptOne.enabled = true;
            if (scriptTwo != null) scriptTwo.enabled = true;
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
        else
        {
            Debug.LogWarning("Color Adjustments not found on the Post Process Volume Profile!");
        }
    }
}
