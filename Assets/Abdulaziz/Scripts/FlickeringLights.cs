using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlickeringLights : MonoBehaviour
{
    public enum FlickerMode { Random, Perlin }

    [Header("References")]
    [SerializeField] private Light targetLight;

    [Header("Flicker Settings")]
    [SerializeField] private FlickerMode mode = FlickerMode.Perlin;
    [SerializeField] private float minIntensity = 0.5f;
    [SerializeField] private float maxIntensity = 2f;
    [SerializeField] private float flickerSpeed = 10f;

    [Header("Random Mode Only")]
    [Tooltip("How often a new random target is picked (seconds)")]
    [SerializeField] private float randomInterval = 0.1f;
    [SerializeField] private float smoothing = 5f;

    [Header("Optional Range Flicker")]
    [SerializeField] private bool flickerRange = false;
    [SerializeField] private float minRange = 4f;
    [SerializeField] private float maxRange = 8f;

    private float _targetIntensity;
    private float _timer;
    private float _noiseOffset;

    private void Awake()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        // lights dont sync up with this code
        _noiseOffset = Random.Range(0f, 1000f); 
        _targetIntensity = targetLight.intensity;
    }

    private void Update()
    {
        switch (mode)
        {
            case FlickerMode.Perlin:
                FlickerWithPerlin();
                break;
            case FlickerMode.Random:
                FlickerWithRandom();
                break;
        }
    }

    private void FlickerWithPerlin()
    {
        float noise = Mathf.PerlinNoise(_noiseOffset, Time.time * flickerSpeed);
        targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);

        if (flickerRange)
        {
            float rangeNoise = Mathf.PerlinNoise(_noiseOffset + 50f, Time.time * flickerSpeed);
            targetLight.range = Mathf.Lerp(minRange, maxRange, rangeNoise);
        }
    }

    private void FlickerWithRandom()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _targetIntensity = Random.Range(minIntensity, maxIntensity);
            _timer = randomInterval;
        }

        targetLight.intensity = Mathf.Lerp(targetLight.intensity, _targetIntensity, Time.deltaTime * smoothing);
    }
}