using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// أشكال تطلع من تحت لفوق وهي تدور حول نفسها وتتمايل — طفو في الفضاء.
///
/// كل شيء بفضاء نقطة الأصل (الكاميرا عادةً)، فالأشكال تعبر الكادر مهما تحرّكت
/// الكاميرا أو ابتعدت. بلا هذا كانت تُترك خلفها لحظة ابتعاد الكاميرا في النهاية.
///
/// الأشكال تُعاد لا تُدمَّر: أول ما يعبر شكلٌ أعلى الكادر يرجع تحت بعشوائية
/// جديدة. فلا <c>Instantiate</c> ولا <c>Destroy</c> طوال العرض ولا كنس ذاكرة.
///
/// حُطّه على أي كائن، وأعطه بريفابات أو حتى كائنات من السين (تُستخدم كقوالب
/// وتُطفأ أصولها تلقائيًا). للكريديت: ضعه على نفس كائن <see cref="GameCredits"/>
/// وهو يشغّله بنفسه.
/// </summary>
[DisallowMultipleComponent]
public class FloatingProps : MonoBehaviour
{
    [Header("الأشكال")]
    [Tooltip("بريفابات أو كائنات من السين تُنسخ. كائن السين يُطفأ أصله تلقائيًا")]
    [SerializeField] private GameObject[] shapes;
    [Tooltip("كم شكلًا في الجو مرة واحدة")]
    [SerializeField] private int count = 14;
    [Tooltip("كل كم ثانية يدخل شكل جديد في البداية")]
    [SerializeField] private float spawnEvery = 0.9f;

    [Header("المكان")]
    [Tooltip("نقطة الأصل — فارغة = الكاميرا الرئيسية")]
    [SerializeField] private Transform origin;
    [Tooltip("بُعد الأشكال أمام نقطة الأصل (متر)")]
    [SerializeField] private float depth = 13f;
    [Tooltip("تشتّتها يمينًا ويسارًا")]
    [SerializeField] private float spreadX = 10f;
    [Tooltip("تشتّتها في العمق")]
    [SerializeField] private float spreadZ = 5f;
    [Tooltip("تبدأ على هذا العمق تحت الكادر")]
    [SerializeField] private float startBelow = 7f;
    [Tooltip("ترجع تحت بعد أن تتجاوز هذا الارتفاع")]
    [SerializeField] private float endAbove = 8f;

    [Header("الطلوع")]
    [Tooltip("سرعة الصعود (متر/ثانية)")]
    [SerializeField] private float riseSpeed = 1.5f;
    [Tooltip("تفاوت السرعة بين شكل وآخر — بدونه صعدت كلها كصفّ واحد")]
    [SerializeField] private float riseJitter = 0.7f;

    [Header("الدوران")]
    [Tooltip("دوران الشكل حول نفسه (درجة/ثانية)")]
    [SerializeField] private float spin = 28f;
    [SerializeField] private float spinJitter = 18f;

    [Header("التمايل")]
    [Tooltip("تمايل جانبي أثناء الصعود (متر)")]
    [SerializeField] private float sway = 0.5f;
    [SerializeField] private float swaySpeed = 0.5f;

    [Header("الحجم")]
    [SerializeField] private float scale = 1f;
    [SerializeField] private float scaleJitter = 0.4f;

    [Header("التشغيل")]
    [Tooltip("يبدأ وحده عند تشغيل السين — أطفئه إن كان غيره يشغّله (الكريديت مثلًا)")]
    [SerializeField] private bool playOnStart = false;

    /// <summary>شكل طائر واحد وحالته.</summary>
    private class Prop
    {
        public Transform transform;
        public Vector3 baseScale;
        public Vector3 spinAxis;
        public float spinSpeed;
        public float riseSpeed;
        public float swayPhase;
        public float centerX;
    }

    private readonly List<Prop> props = new List<Prop>();
    private Transform root;
    private float spawnTimer;
    private bool running;

    private void Start()
    {
        if (playOnStart) Begin();
    }

    /// <summary>يبدأ الطفو حول نقطة الأصل المضبوطة (أو الكاميرا الرئيسية).</summary>
    public void Begin() => Begin(null);

    /// <summary>يبدأ الطفو حول نقطة بعينها — يمرّرها الكريديت وهو يعرف كاميرته.</summary>
    public void Begin(Transform around)
    {
        if (running) return;

        Transform anchor = around != null ? around
                         : (origin != null ? origin
                         : (Camera.main != null ? Camera.main.transform : null));

        if (anchor == null)
        {
            Debug.LogWarning("[FloatingProps] ما فيه نقطة أصل ولا كاميرا رئيسية.", this);
            return;
        }

        if (shapes == null || shapes.Length == 0)
        {
            Debug.LogWarning("[FloatingProps] ما فيه أشكال في القائمة.", this);
            return;
        }

        var go = new GameObject("FloatingProps_Root");
        root = go.transform;
        root.SetParent(anchor, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;

        // كائن السين قالبٌ لا مشارك: يُطفأ حتى لا يبقى واقفًا مكانه بجانب نسخه
        foreach (GameObject shape in shapes)
            if (shape != null && shape.scene.IsValid()) shape.SetActive(false);

        spawnTimer = 0f;
        running = true;
    }

    /// <summary>يوقف الطفو ويشيل النسخ.</summary>
    public void Stop()
    {
        running = false;
        props.Clear();
        if (root != null) Destroy(root.gameObject);
        root = null;
    }

    private void Update()
    {
        if (!running || root == null) return;

        if (props.Count < count)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = Mathf.Max(0.05f, spawnEvery);
                Spawn();
            }
        }

        float dt = Time.deltaTime;
        for (int i = 0; i < props.Count; i++)
        {
            Prop prop = props[i];
            if (prop.transform == null) continue;

            Vector3 local = prop.transform.localPosition;
            local.y += prop.riseSpeed * dt;
            prop.swayPhase += swaySpeed * dt;
            local.x = prop.centerX + Mathf.Sin(prop.swayPhase) * sway;
            prop.transform.localPosition = local;

            prop.transform.Rotate(prop.spinAxis, prop.spinSpeed * dt, Space.Self);

            // عبر أعلى الكادر: يرجع تحت بعشوائية جديدة بدل أن يُدمَّر ويُنشأ غيره
            if (local.y > endAbove) Place(prop);
        }
    }

    private void Spawn()
    {
        GameObject shape = shapes[Random.Range(0, shapes.Length)];
        if (shape == null) return;

        GameObject clone = Instantiate(shape, root);
        clone.SetActive(true);
        clone.name = shape.name + " (طائر)";
        Decorate(clone);

        var prop = new Prop
        {
            transform = clone.transform,
            baseScale = shape.transform.localScale,
        };

        props.Add(prop);
        Place(prop, firstTime: true);
    }

    /// <summary>
    /// زينة لا جسم: الكولايدرات تُطفأ والفيزياء تُلغى. الأشكال تطفو أمام الكاميرا
    /// وتمر خلال مباني الهب، فلو بقيت أجسامًا صلبة دفعت اللاعب أو علّقت الفيزياء.
    /// </summary>
    private static void Decorate(GameObject clone)
    {
        foreach (var collider in clone.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    /// <summary>يحطّ الشكل تحت الكادر بمكان وحجم ودوران جديد.</summary>
    private void Place(Prop prop, bool firstTime = false)
    {
        prop.centerX = Random.Range(-spreadX, spreadX);

        // أول دفعة توزَّع على كامل الارتفاع، وإلا طلعت كلها من القاع دفعة واحدة
        float y = firstTime
            ? Random.Range(-startBelow, endAbove)
            : -startBelow;

        prop.transform.localPosition = new Vector3(
            prop.centerX, y, depth + Random.Range(-spreadZ, spreadZ));

        prop.transform.localRotation = Random.rotation;

        float size = Mathf.Max(0.01f, scale + Random.Range(-scaleJitter, scaleJitter));
        prop.transform.localScale = prop.baseScale * size;

        prop.riseSpeed = Mathf.Max(0.05f, riseSpeed + Random.Range(-riseJitter, riseJitter));
        prop.spinAxis = Random.onUnitSphere;
        prop.spinSpeed = spin + Random.Range(-spinJitter, spinJitter);
        prop.swayPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void OnDisable()
    {
        if (running) Stop();
    }

    private void OnValidate()
    {
        count = Mathf.Max(1, count);
        spawnEvery = Mathf.Max(0.05f, spawnEvery);
        scale = Mathf.Max(0.01f, scale);
        scaleJitter = Mathf.Clamp(scaleJitter, 0f, scale);
        riseJitter = Mathf.Clamp(riseJitter, 0f, Mathf.Max(0f, riseSpeed));
    }

    private void OnDrawGizmosSelected()
    {
        Transform anchor = origin != null ? origin
                         : (Camera.main != null ? Camera.main.transform : transform);

        Gizmos.matrix = Matrix4x4.TRS(anchor.position, anchor.rotation, Vector3.one);
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(0f, (endAbove - startBelow) * 0.5f, depth),
                            new Vector3(spreadX * 2f, startBelow + endAbove, spreadZ * 2f));
    }
}
