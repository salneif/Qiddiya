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
        [InspectorName("وميض لمبات")] Blink
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

    [Header("متى يلمع")]
    [Tooltip("المقبض الذي تدلّ عليه اللوحة — أول ما يمسكه اللاعب ينطفي التلميح")]
    [SerializeField] private RemoteSlideControl stopWhenUsed;
    [Tooltip("يبقى مطفيًا بعد أول استخدام. أطفئه ليرجع يلمع كل ما ترك اللاعب المقبض.")]
    [SerializeField] private bool stayOffAfterUse = true;
    [Tooltip("يلمع فقط لما يكون اللاعب أقرب من هذه المسافة (متر). صفر = دائمًا")]
    [SerializeField] private float showDistance = 0f;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("سرعة ظهور واختفاء اللمعة — تمنع الانطفاء المفاجئ")]
    [SerializeField] private float fadeSpeed = 3f;
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

        if (weight <= 0f)
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
        float t = Time.time * speed;
        if (style == Style.Blink)
            return Mathf.Repeat(t, 1f) < 0.5f ? 1f : 0f;

        return 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI);
    }

    private void Apply(float brightness)
    {
        foreach (var t in targets)
        {
            if (t.renderer == null) continue;

            Color glow = t.baseEmission * tint * brightness;
            Color final = Color.Lerp(t.baseEmission, glow, weight);

            // نقرأ البلوك الحالي أولًا حتى لا نمسح قيمًا وضعها سكربت آخر
            t.renderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, final);
            t.renderer.SetPropertyBlock(block);
        }

        if (hintLight != null) hintLight.intensity = lightBaseIntensity * brightness * weight;
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

        if (hintLight != null) hintLight.intensity = 0f;
        applied = false;
    }

    /// <summary>يطفي التلميح نهائيًا — اربطه بأي حدث (WorldLever.onActivated، PuzzleButton.onPressed...).</summary>
    public void StopHint() => stopped = true;

    /// <summary>يرجّع التلميح يلمع من جديد، وينسى أن المقبض استُخدم.</summary>
    public void StartHint()
    {
        stopped = false;
        used = false;
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
