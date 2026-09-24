using UnityEngine;

/// <summary>
/// يخلّي مجسّمًا مضيئًا (لوحة سهم بلمبات، زر، رافعة) يلمع وينبض كتلميح للاعب:
/// "هنا شي تقدر تستخدمه". وينطفي تلقائيًا أول ما يستخدمه اللاعب، فما يزعجه بعد ما فهم.
///
/// يحرّك قوة الـ Emission الموجودة أصلًا في الماتيريال عبر MaterialPropertyBlock:
///  - ما يغيّر الماتيريال المشتركة (ماتيريال سينتي نفسها على عشرات القطع).
///  - ما يفعّل أي كلمة مفتاحية جديدة، فيشتغل في البلد مثل المحرر بالضبط.
///
/// الشرط الوحيد: الـ Emission مفعّل في ماتيريال المجسّم. لوحات سينتي المضيئة جاهزة.
/// لو ما كان مفعّلًا، يطبع تحذيرًا في الكونسول بدل ما يفشل بصمت.
/// </summary>
[DisallowMultipleComponent]
public class HintGlow : MonoBehaviour
{
    /// <summary>شكل اللمعة.</summary>
    public enum Style
    {
        [InspectorName("نبض ناعم")] Pulse,
        [InspectorName("وميض لمبات")] Blink,
        [InspectorName("ثابت بلا نبض")] Steady
    }

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private const string EmissionKeyword = "_EMISSION";

    [Header("المظهر")]
    [Tooltip("المجسّمات التي تلمع. اتركها فارغة لتُلتقط كل مجسّمات هذا الكائن وأبنائه.")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Style style = Style.Pulse;
    [Tooltip("كم نبضة في الثانية")]
    [SerializeField] private float speed = 1.2f;
    [Tooltip("أخفت نقطة في النبض — نسبة من إضاءة الماتيريال الأصلية (1 = كما هي)")]
    [SerializeField] private float minBrightness = 0.15f;
    [Tooltip("أقوى نقطة في النبض — نسبة من إضاءة الماتيريال الأصلية")]
    [SerializeField] private float maxBrightness = 2.5f;
    [Tooltip("لون اللمعة، يُضرب في لون الماتيريال. الأبيض يحافظ على لونها الأصلي. " +
             "خارج دائرة الألوان يظهر رماديًا، فالسطوع هو اللي يلفت النظر.")]
    [SerializeField] private Color tint = Color.white;

    [Header("ضوء مرافق (اختياري)")]
    [Tooltip("Light ينبض مع اللمعة فيضيء ما حول اللوحة فعليًا. ينطفي مع انطفاء التلميح.")]
    [SerializeField] private Light hintLight;
    [Tooltip("أضواء إضافية تتدرّج معه — لبوابة حولها عدة أضواء تولع سوية. " +
             "⚠️ لا تطفئ كائناتها ولا تضعها في Show When Open، وإلا ولعت دفعة واحدة؛ " +
             "اتركها شغّالة وهذا السكربت يبدأ بإطفاء شدّتها ثم يرفعها بالتدريج.")]
    [SerializeField] private Light[] extraLights;

    [Header("متى يلمع")]
    [Tooltip("المقبض الذي تدلّ عليه اللوحة — أول ما يمسكه اللاعب ينطفي التلميح")]
    [SerializeField] private RemoteSlideControl stopWhenUsed;
    [Tooltip("يبقى مطفيًا بعد أول استخدام. أطفئه ليرجع يلمع كل ما ترك اللاعب المقبض.")]
    [SerializeField] private bool stayOffAfterUse = true;
    [Tooltip("يلمع فقط لما يكون اللاعب أقرب من هذه المسافة (متر). صفر = دائمًا")]
    [SerializeField] private float showDistance = 0f;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("سرعة ظهور واختفاء اللمعة: 1 ÷ المدة بالثواني. " +
             "1 = ثانية، 0.25 = أربع ثوانٍ، 3 = ثلث ثانية. " +
             "والظهور يبدأ وينتهي بنعومة فلا يقفز اللون دفعة واحدة.")]
    [SerializeField] private float fadeSpeed = 3f;
    [Tooltip("قوة الإضاءة وهو مطفي، نسبةً لإضاءة الماتيريال الأصلية. " +
             "صفر = مظلم تمامًا حتى يولع (لبوابة تشع دائمًا وتبيها تظل مطفية حتى يستحقها اللاعب). " +
             "1 = يبقى بإضاءته الأصلية كما كان.")]
    [Range(0f, 1f)]
    [SerializeField] private float offBrightness = 1f;
    [Tooltip("يبدأ مطفيًا حتى يُنادى StartHint — اربطه بحدث مثل FlagBase.On Planted " +
             "ليولع الشيء لحظة زرع العلم لا قبلها.")]
    [SerializeField] private bool startStopped = false;

    private struct Target
    {
        public Renderer renderer;
        public Color baseEmission;
        public bool hadBlock;
    }

    private Target[] targets;
    private MaterialPropertyBlock block;
    private float lightBaseIntensity;
    private float[] extraBaseIntensities;
    private float weight;   // 0 = شكل الماتيريال الأصلي بالضبط، 1 = لمعة كاملة
    private bool applied;   // هل غيّرنا شيئًا يحتاج إرجاعًا؟
    private bool used;      // أمسك اللاعب المقبض مرة على الأقل
    private bool stopped;   // أُطفئ يدويًا عبر StopHint
    private Transform player;

    private void Awake()
    {
        stopped = startStopped;
        block = new MaterialPropertyBlock();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        var list = new System.Collections.Generic.List<Target>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (TryGetBaseEmission(r, out Color baseEmission))
                list.Add(new Target { renderer = r, baseEmission = baseEmission, hadBlock = r.HasPropertyBlock() });
        }
        targets = list.ToArray();

        if (targets.Length == 0)
            Debug.LogWarning("[HintGlow] ما في مجسّم ماتيريالته فيها Emission مفعّل تحت " + name +
                             " — فعّل Emission في الماتيريال أو استخدم خانة الضوء المرافق.", this);

        if (hintLight != null)
        {
            lightBaseIntensity = hintLight.intensity;
            hintLight.intensity = 0f;
        }

        WarnAboutFlicker(hintLight);
        if (extraLights != null)
        {
            extraBaseIntensities = new float[extraLights.Length];
            for (int i = 0; i < extraLights.Length; i++)
            {
                if (extraLights[i] == null) continue;
                extraBaseIntensities[i] = extraLights[i].intensity;
                extraLights[i].intensity = 0f;
                WarnAboutFlicker(extraLights[i]);
            }
        }
    }

    /// <summary>
    /// لون الـ Emission الأصلي لأول ماتيريال مضيئة في المجسّم. يرجع false إذا ما في
    /// ماتيريال Emission فيها مفعّل — تغيير اللون بلا الكلمة المفتاحية لا يظهر شيئًا.
    /// </summary>
    private static bool TryGetBaseEmission(Renderer r, out Color color)
    {
        color = Color.black;
        foreach (var m in r.sharedMaterials)
        {
            if (m == null || !m.HasProperty(EmissionId) || !m.IsKeywordEnabled(EmissionKeyword)) continue;

            color = m.GetColor(EmissionId);
            // Emission مفعّل بلون أسود: الضرب فيه يعطي أسود، فنبدأ من الأبيض
            if (color.maxColorComponent < 0.01f) color = Color.white;
            return true;
        }
        return false;
    }

    private void Update()
    {
        weight = Mathf.MoveTowards(weight, WantsGlow() ? 1f : 0f, fadeSpeed * Time.deltaTime);

        // مطفي وبإضاءته الأصلية = لا شيء نفعله، فنرجّع الماتيريال كما هو تمامًا
        if (weight <= 0f && offBrightness >= 0.999f)
        {
            if (applied) Restore();
            return;
        }

        float k = Mathf.Lerp(minBrightness, maxBrightness, Wave());
        Apply(k);
    }

    /// <summary>هل يجب أن يلمع الآن؟</summary>
    private bool WantsGlow()
    {
        if (stopped) return false;

        if (stopWhenUsed != null && stopWhenUsed.IsEngaged)
        {
            used = true;
            return false;
        }
        if (used && stayOffAfterUse) return false;

        if (showDistance <= 0f) return true;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return false;
            player = go.transform;
        }
        return Vector3.Distance(player.position, transform.position) <= showDistance;
    }

    /// <summary>موجة من 0 إلى 1 حسب شكل اللمعة.</summary>
    private float Wave()
    {
        // ثابت: يشع بـ Max Brightness بلا حركة — يولع مرة ويظل
        if (style == Style.Steady) return 1f;

        float t = Time.time * speed;
        if (style == Style.Blink)
            return Mathf.Repeat(t, 1f) < 0.5f ? 1f : 0f;

        return 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI);
    }

    private void Apply(float brightness)
    {
        // تنعيم التقدّم: يبدأ الولوع بهدوء ويستقر بهدوء بدل قفزة خطية
        float w = Mathf.SmoothStep(0f, 1f, weight);

        foreach (var t in targets)
        {
            if (t.renderer == null) continue;

            Color off = t.baseEmission * offBrightness;
            Color glow = t.baseEmission * tint * brightness;
            Color final = Color.Lerp(off, glow, w);

            // نقرأ البلوك الحالي أولًا حتى لا نمسح قيمًا وضعها سكربت آخر
            t.renderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, final);
            t.renderer.SetPropertyBlock(block);
        }

        SetLights(brightness * w);
        applied = true;
    }

    /// <summary>يرجّع المجسّم لشكل ماتيريالته الأصلي بالضبط.</summary>
    private void Restore()
    {
        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t.renderer == null) continue;

                if (!t.hadBlock)
                {
                    t.renderer.SetPropertyBlock(null);
                    continue;
                }
                t.renderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, t.baseEmission);
                t.renderer.SetPropertyBlock(block);
            }
        }

        SetLights(0f);
        applied = false;
    }

    /// <summary>
    /// <see cref="LightFlicker"/> على نفس الضوء يكتب شدّته كل إطار، فيلغي تدرّجنا
    /// ويولّعه قبل أوانه. نحذّر بدل أن يقضي أحدهم ساعة يبحث عن السبب.
    /// </summary>
    private void WarnAboutFlicker(Light light)
    {
        if (light == null) return;

        var flicker = light.GetComponent<LightFlicker>();
        if (flicker == null || !flicker.enabled) return;

        Debug.LogWarning($"[HintGlow] الضوء \"{light.name}\" عليه LightFlicker شغّال — " +
                         "هو يكتب شدّة الضوء كل إطار فيلغي التدرّج. أطفئه.", light);
    }

    /// <summary>يضبط شدّة كل الأضواء المرافقة نسبةً لشدّتها الأصلية.</summary>
    private void SetLights(float factor)
    {
        if (hintLight != null) hintLight.intensity = lightBaseIntensity * factor;

        if (extraLights == null || extraBaseIntensities == null) return;
        for (int i = 0; i < extraLights.Length; i++)
        {
            if (extraLights[i] == null || i >= extraBaseIntensities.Length) continue;
            extraLights[i].intensity = extraBaseIntensities[i] * factor;
        }
    }

    /// <summary>يطفي التلميح نهائيًا — اربطه بأي حدث (WorldLever.onActivated، PuzzleButton.onPressed...).</summary>
    public void StopHint() => stopped = true;

    /// <summary>يرجّع التلميح يلمع من جديد، وينسى أن المقبض استُخدم.</summary>
    public void StartHint()
    {
        stopped = false;
        used = false;
    }

    /// <summary>
    /// تجربة أثناء Play: كليك يمين على اسم المكوّن. لو ما تغيّر شيء على الشاشة،
    /// فالمجسّم الذي تراه يشع ليس هذا — غالبًا مجسّم آخر يشارك نفس الخامة.
    /// </summary>
    [ContextMenu("تجربة: ولّع الآن")]
    private void DebugStart()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[HintGlow] التجربة أثناء Play فقط.", this); return; }
        StartHint();
        Debug.Log($"[HintGlow] ولّعت \"{name}\" — عدد المجسّمات المتأثرة: {(targets != null ? targets.Length : 0)}.", this);
    }

    [ContextMenu("تجربة: اطفئ الآن")]
    private void DebugStop()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[HintGlow] التجربة أثناء Play فقط.", this); return; }
        StopHint();
        Debug.Log($"[HintGlow] أطفأت \"{name}\" — لو ما تغيّر شيء فالشاع مجسّم آخر.", this);
    }

    private void OnDisable()
    {
        weight = 0f;
        if (applied) Restore();
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        minBrightness = Mathf.Max(0f, minBrightness);
        maxBrightness = Mathf.Max(minBrightness, maxBrightness);
        showDistance = Mathf.Max(0f, showDistance);
        fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        if (showDistance <= 0f) return;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, showDistance);
    }
}
