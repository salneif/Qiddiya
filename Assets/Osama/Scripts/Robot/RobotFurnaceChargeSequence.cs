using System.Collections;
using UnityEngine;

/// <summary>
/// تسلسل شحن "الدفاية" لبوس الروبوت بأسلوب Little Nightmares:
/// تشتعل الأشرطة تدريجيًا من الأسفل للأعلى، ثم تشتعل العيون، ثم يطلق فتولّع
/// الغرفة بالأحمر ويطفى كل شيء، ينتظر فترة، ثم يعيد الكرّة.
/// حُط هذا السكربت على كائن الروبوت (MODELO FINAL) واربط الرندررات بالترتيب.
/// التحكم عبر MaterialPropertyBlock فلا يتم نسخ المتيريالات ولا كسر ربطها.
/// </summary>
public class RobotFurnaceChargeSequence : MonoBehaviour
{
    [Header("قطع الوهج (مرتبة من الأسفل 1 إلى الأعلى 5)")]
    [Tooltip("رندررات الأشرطة بالترتيب: العنصر 0 هو الأسفل")]
    [SerializeField] private Renderer[] bars;

    [Tooltip("رندررات العيون (eyeR / eyeL) — آخر ما يشتعل قبل الإطلاق")]
    [SerializeField] private Renderer[] eyes;

    [Header("التوقيت")]
    [Tooltip("زمن اشتعال كل شريط تدريجيًا (ثواني)")]
    [SerializeField] private float barRampTime = 0.45f;

    [Tooltip("فاصل بسيط بين اشتعال شريط والذي يليه")]
    [SerializeField] private float barDelay = 0.05f;

    [Tooltip("زمن اشتعال العيون تدريجيًا")]
    [SerializeField] private float eyeRampTime = 0.6f;

    [Tooltip("مدة توهّج الغرفة بالأحمر لحظة الإطلاق")]
    [SerializeField] private float fireHoldTime = 0.35f;

    [Tooltip("مدة إطفاء كل شيء تدريجيًا (fade out) بعد الإطلاق")]
    [SerializeField] private float fireFadeOutTime = 0.5f;

    [Tooltip("مدة الانتظار بعد الإطلاق قبل إعادة الشحن")]
    [SerializeField] private float cooldownTime = 3f;

    [Header("شكل التدرّج")]
    [Tooltip("منحنى اشتعال كل قطعة (من 0 مطفي إلى 1 مشتعل بالكامل)")]
    [SerializeField] private AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("مضاعِف لشدة الإضاءة عند الاشتعال الكامل (1 = نفس لون المتيريال الأصلي)")]
    [SerializeField] private float emissionBoost = 1f;

    [Header("إضاءة الغرفة عند الإطلاق")]
    [Tooltip("ضوء أحمر يضيء الغرفة لحظة الإطلاق (اختياري)")]
    [SerializeField] private Light roomLight;

    [Tooltip("شدة الضوء الأحمر لحظة الإطلاق")]
    [SerializeField] private float roomLightIntensity = 8f;

    [Header("الأصوات")]
    [Tooltip("مصدر الصوت المستخدم لتشغيل مقاطع الشحن (يُضاف تلقائيًا إذا تُرك فارغًا)")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("صوت اشتعال الشريط: طقّة معدنية + أزيز كهربائي خفيف. يتكرر لكل شريط بطبقة صوت (pitch) أعلى تدريجيًا")]
    [SerializeField] private AudioClip barIgniteSound;

    [Tooltip("طبقة الصوت (pitch) عند أول شريط")]
    [SerializeField] private float barPitchStart = 0.9f;

    [Tooltip("مقدار ارتفاع طبقة الصوت مع كل شريط جديد")]
    [SerializeField] private float barPitchStep = 0.05f;

    [Tooltip("صوت اشتعال العيون: قصير وحاد (زقّة/طنّة معدنية) يميّز آخر مرحلة قبل الإطلاق")]
    [SerializeField] private AudioClip eyeIgniteSound;

    [Header("عام")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = true;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Emitter[] barEmitters;
    private Emitter[] eyeEmitters;
    private Coroutine routine;
    private float roomLightBaseIntensity;

    /// <summary>حالة إضاءة قطعة واحدة (شريط أو عين).</summary>
    private class Emitter
    {
        public Renderer Renderer;
        public MaterialPropertyBlock Block;
        public Color TargetEmission;

        public void SetLevel(float t)
        {
            Renderer.GetPropertyBlock(Block);
            Block.SetColor(EmissionColorId, TargetEmission * Mathf.Max(0f, t));
            Renderer.SetPropertyBlock(Block);
        }
    }

    private void Awake()
    {
        barEmitters = BuildEmitters(bars);
        eyeEmitters = BuildEmitters(eyes);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        if (roomLight != null)
        {
            roomLightBaseIntensity = roomLight.intensity;
            roomLight.enabled = false;
        }

        SetAllOff();
    }

    private void OnEnable()
    {
        if (playOnStart)
            StartSequence();
    }

    private void OnDisable()
    {
        StopSequence();
    }

    /// <summary>يبدأ تسلسل الشحن من جديد.</summary>
    public void StartSequence()
    {
        StopSequence();
        routine = StartCoroutine(RunSequence());
    }

    /// <summary>يوقف التسلسل ويطفّئ كل شيء.</summary>
    public void StopSequence()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private Emitter[] BuildEmitters(Renderer[] renderers)
    {
        if (renderers == null) return new Emitter[0];

        var list = new System.Collections.Generic.List<Emitter>(renderers.Length);
        foreach (var r in renderers)
        {
            if (r == null) continue;

            Color target = Color.red;
            var mat = r.sharedMaterial;
            if (mat != null && mat.HasProperty(EmissionColorId))
                target = mat.GetColor(EmissionColorId);

            list.Add(new Emitter
            {
                Renderer = r,
                Block = new MaterialPropertyBlock(),
                TargetEmission = target * emissionBoost
            });
        }
        return list.ToArray();
    }

    private void SetAllOff()
    {
        SetGroupLevel(barEmitters, 0f);
        SetGroupLevel(eyeEmitters, 0f);
    }

    private static void SetGroupLevel(Emitter[] group, float level)
    {
        foreach (var e in group)
            e.SetLevel(level);
    }

    private IEnumerator RunSequence()
    {
        do
        {
            // 1) البداية: كل شيء مطفي
            SetAllOff();
            if (roomLight != null) roomLight.enabled = false;

            // 2) الشحن: الأشرطة من الأسفل للأعلى (تبقى مشتعلة تراكميًا)
            for (int i = 0; i < barEmitters.Length; i++)
            {
                PlayBarSound(i);
                yield return RampEmitter(barEmitters[i], barRampTime);
                if (barDelay > 0f)
                    yield return new WaitForSeconds(barDelay);
            }

            // 3) العيون آخر ما يشتعل (صوت حاد مميز)
            PlayEyeSound();
            yield return RampGroup(eyeEmitters, eyeRampTime);

            // 4) الإطلاق: الغرفة تولّع أحمر
            if (roomLight != null)
            {
                roomLight.color = Color.red;
                roomLight.intensity = roomLightIntensity;
                roomLight.enabled = true;
            }

            OnFire();

            yield return new WaitForSeconds(fireHoldTime);

            // 5) يخبو كل شيء تدريجيًا (fade out) بدل الإطفاء المفاجئ
            yield return FadeAllOut(fireFadeOutTime);
            if (roomLight != null)
            {
                roomLight.enabled = false;
                roomLight.intensity = roomLightBaseIntensity;
            }

            // 6) انتظار قبل إعادة الشحن
            yield return new WaitForSeconds(cooldownTime);
        }
        while (loop);
    }

    /// <summary>
    /// نقطة الإطلاق. لاحقًا نضيف هنا إطلاق الليزر وإلحاق الضرر باللاعب.
    /// </summary>
    private void OnFire()
    {
        // TODO: إطلاق شعاع الليزر من العيون + قتل الشخصية (لاحقًا)
    }

    private void PlayBarSound(int barIndex)
    {
        if (barIgniteSound == null) return;
        audioSource.pitch = barPitchStart + barPitchStep * barIndex;
        audioSource.PlayOneShot(barIgniteSound);
    }

    private void PlayEyeSound()
    {
        if (eyeIgniteSound == null) return;
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(eyeIgniteSound);
    }

    private IEnumerator RampEmitter(Emitter emitter, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            emitter.SetLevel(chargeCurve.Evaluate(k));
            yield return null;
        }
        emitter.SetLevel(1f);
    }

    private IEnumerator RampGroup(Emitter[] group, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            SetGroupLevel(group, chargeCurve.Evaluate(k));
            yield return null;
        }
        SetGroupLevel(group, 1f);
    }

    private IEnumerator FadeAllOut(float duration)
    {
        float startRoomIntensity = roomLight != null ? roomLight.intensity : 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            float level = 1f - k;
            SetGroupLevel(barEmitters, level);
            SetGroupLevel(eyeEmitters, level);
            if (roomLight != null)
                roomLight.intensity = Mathf.Lerp(startRoomIntensity, 0f, k);
            yield return null;
        }
        SetAllOff();
    }
}
