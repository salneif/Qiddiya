using UnityEngine;

public class SteamPipe : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] steamEmitters;
    [SerializeField] private Transform pivot;
    [SerializeField] private float maxShakeIntensity = 0.06f;
    [SerializeField] private float shakeFrequency = 25f;
    [SerializeField] private float dangerThreshold = 100f;
    private Vector3 _shakeOrigin;
    private float _pressure;
    private float _shakeIntensity;

    void Start()
    {
        if (pivot != null)
            _shakeOrigin = pivot.localPosition;

        setEmissionRates(0f);
    }

    void Update()
    {
        if (pivot == null || _shakeIntensity < 0.001f) return;

        float time = Time.time * shakeFrequency;
        float x = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * _shakeIntensity;
        float y = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * _shakeIntensity;
        pivot.localPosition = _shakeOrigin + new Vector3(x, y, 0f);
    }

    public void SetPressure(float pressure)
    {
        _pressure = Mathf.Max(0f, pressure);
        updatePipeState();
    }

    void updatePipeState()
    {
        if (_pressure <= 40f)
            _shakeIntensity = 0f;
        else
            _shakeIntensity = Mathf.Lerp(0f, maxShakeIntensity, (_pressure - 40f) / (dangerThreshold - 40f));

        if (_shakeIntensity < 0.001f && pivot != null)
            pivot.localPosition = _shakeOrigin;

        float emissionFactor = _pressure / dangerThreshold;
        setEmissionRates(emissionFactor);
    }

    void setEmissionRates(float factor)
    {
        if (steamEmitters == null) return;

        for (int i = 0; i < steamEmitters.Length; i++)
        {
            if (steamEmitters[i] == null) continue;

            var emission = steamEmitters[i].emission;
            float threshold = i * 0.2f + 0.1f;
            if (factor < threshold)
            {
                emission.rateOverTime = 0f;
                continue;
            }

            float localFactor = (factor - threshold) / (1f - threshold);
            emission.rateOverTime = Mathf.Lerp(2f, 30f, localFactor);
        }
    }

    public bool IsDanger => _pressure >= dangerThreshold;
    public float Pressure => _pressure;
}
