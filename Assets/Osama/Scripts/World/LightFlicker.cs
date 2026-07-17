using UnityEngine;

/// <summary>
/// وميض ضوء عضوي (فانوس/لمبة ملاهي قديمة) — يتنفس بنعومة عبر Perlin،
/// مع "طقّات" انقطاع عشوائية اختيارية تعطي طابع رعب خفيف.
/// حُطّه على أي Light في الخلفية ليحسّ المشهد أنه حي.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("التنفس (وميض ناعم مستمر)")]
    [Tooltip("أقل شدة يوصل لها الضوء (نسبة من شدته الأصلية)")]
    [Range(0f, 1f)]
    [SerializeField] private float minIntensity = 0.6f;
    [Tooltip("سرعة الوميض الناعم")]
    [SerializeField] private float flickerSpeed = 1.5f;

    [Header("طقّات الانقطاع (اختياري — رعب)")]
    [Tooltip("تفعيل انقطاعات مفاجئة قصيرة")]
    [SerializeField] private bool blackouts = false;
    [Tooltip("متوسط الزمن بين الانقطاعات (ثواني)")]
    [SerializeField] private float blackoutEvery = 7f;
    [Tooltip("مدة الانقطاع (ثواني)")]
    [SerializeField] private float blackoutLength = 0.12f;

    private Light lightSource;
    private float baseIntensity;
    private float seed;
    private float blackoutTimer;
    private float blackoutLeft;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        seed = Random.value * 100f;
        ResetBlackoutTimer();
    }

    private void Update()
    {
        // انقطاع مفاجئ؟
        if (blackouts)
        {
            if (blackoutLeft > 0f)
            {
                blackoutLeft -= Time.deltaTime;
                lightSource.intensity = 0f;
                return;
            }

            blackoutTimer -= Time.deltaTime;
            if (blackoutTimer <= 0f)
            {
                blackoutLeft = blackoutLength * Random.Range(0.5f, 1.5f);
                ResetBlackoutTimer();
                return;
            }
        }

        // وميض ناعم عضوي
        float n = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
        lightSource.intensity = baseIntensity * Mathf.Lerp(minIntensity, 1f, n);
    }

    private void ResetBlackoutTimer()
    {
        blackoutTimer = blackoutEvery * Random.Range(0.6f, 1.4f);
    }
}
