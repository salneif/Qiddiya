using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// مناطق لونٍ مؤقّتة فوق نظام العالم الأبيض/الأسود: <b>نبضة</b> تكبر ثم تذوب
/// (<see cref="Pulse"/>)، أو منطقة <b>تتبع</b> شيئًا حتى تُفلت (<see cref="Follow"/>).
///
/// كلها <see cref="WorldInteractor"/> حقيقية، فتُلوّن الشاشة وتُبدّل أرضيّات
/// <c>WorldChange</c> تمامًا كالأعلام — بلا شيدر جديد.
///
/// <b>بمخزونٍ ثابت لكل سين</b>: الشيدر يقرأ ١٠٠ منطقة على الأكثر، والمدير يأخذها
/// بترتيبٍ غير مضمون. فلو زدنا بلا حساب لأسقطنا منطقةً من مناطق السين نفسه — علمًا أو
/// جزيرة. لذا يُعدّ ما في السين أولًا، ولا يُنشأ إلا ما يتّسع تحت الحدّ بهامش.
/// وحين ينفد المخزون تُرفض النبضة بصمت: مؤثّرٌ فائت خيرٌ من منطقةٍ ضائعة.
///
/// وفي سينٍ بلا <see cref="InteractorManager"/> (القائمة، الانترو) لا شيء يُنشأ،
/// و<see cref="Pulse"/> ترجع false.
/// </summary>
[DefaultExecutionOrder(-50)]   // قبل InteractorManager.LateUpdate فيقرأ مواضع هذا الإطار
public class ColorZones : MonoBehaviour
{
    /// <summary>أقصى ما ننشئه في سين.</summary>
    public const int PoolSize = 16;

    /// <summary>هامش تحت حدّ الشيدر لما قد يُنشئه غيرنا وقت اللعب.</summary>
    private const int Headroom = 4;

    /// <summary>بصمة أرضيّات WorldChange = Scale × Radius، والشكل دائرةٌ بنحو ٠٫٤٢ من المربّع.
    /// ٢٫٥ تجعلها بحجم دائرة الشاشة تقريبًا — نفس قيمة الأعلام.</summary>
    private static readonly Vector3 FootprintScale = new Vector3(2.5f, 1f, 2.5f);

    /// <summary>منطقةٌ تتبع شيئًا. أفلتها بـ<see cref="Release"/> حين تنتهي.</summary>
    public sealed class Handle
    {
        internal Zone zone;
        public bool Alive => zone != null && zone.handle == this;

        /// <summary>غيّر نصف القطر وهي حيّة (للتنفّس أو الكبر).</summary>
        public float Radius
        {
            get => Alive ? zone.baseRadius : 0f;
            set { if (Alive) zone.baseRadius = Mathf.Max(0f, value); }
        }
    }

    internal sealed class Zone
    {
        public WorldInteractor interactor;
        public Handle handle;          // منطقة تتبع
        public Transform target;
        public Vector3 offset;
        public float baseRadius;
        public bool pulsing;           // نبضة
        public float start, grow, hold, fade;
        public bool Busy => handle != null || pulsing;
    }

    private static ColorZones current;
    private readonly List<Zone> zones = new List<Zone>();

    /// <summary>هل في هذا السين نظام لونٍ نرسم عليه؟</summary>
    public static bool Available => current != null && current.zones.Count > 0;

    /// <summary>
    /// نبضة لون: تكبر إلى <paramref name="radius"/> خلال <paramref name="grow"/> بقفزةٍ
    /// مرنة، تبقى <paramref name="hold"/>، ثم تذوب خلال <paramref name="fade"/>.
    /// <paramref name="reach"/> = الحدّ الرأسي (م)؛ السالب = تلقائي من نصف القطر.
    /// ترجع false إن لم يكن في السين نظام لون أو نفد المخزون.
    /// </summary>
    public static bool Pulse(Vector3 at, float radius, float grow = 0.18f, float hold = 0.3f,
                             float fade = 0.9f, float reach = -1f)
    {
        Zone z = Take();
        if (z == null) return false;

        z.pulsing = true;
        z.target = null;
        z.offset = at;
        z.baseRadius = Mathf.Max(0.05f, radius);
        z.start = Time.time;
        z.grow = Mathf.Max(0.01f, grow);
        z.hold = Mathf.Max(0f, hold);
        z.fade = Mathf.Max(0.01f, fade);
        Arm(z, reach);
        return true;
    }

    /// <summary>
    /// منطقة تتبع <paramref name="target"/> (بإزاحة <paramref name="offset"/>) حتى تُفلت
    /// أو يُدمَّر الهدف. null إن لم يكن نظام لون أو نفد المخزون — فتحقّق قبل الاستعمال.
    /// </summary>
    public static Handle Follow(Transform target, Vector3 offset, float radius, float reach = -1f)
    {
        if (target == null) return null;
        Zone z = Take();
        if (z == null) return null;

        z.handle = new Handle { zone = z };
        z.target = target;
        z.offset = offset;
        z.baseRadius = Mathf.Max(0f, radius);
        z.pulsing = false;
        Arm(z, reach);
        return z.handle;
    }

    /// <summary>يُفلت منطقةً تتبع. آمنٌ مع null ومع منطقةٍ أُفلتت أو انتهى سينها.</summary>
    public static void Release(Handle handle)
    {
        if (handle == null || !handle.Alive) return;
        Free(handle.zone);
    }

    // ---------- التركيب ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Build();   // السين الأوّل حُمّل قبل أن نشترك
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Build();
    }

    /// <summary>مخزون هذا السين: يعيش معه ويموت معه، فلا يحمل مناطق سينٍ قديم.</summary>
    private static void Build()
    {
        if (current != null) return;   // سين إضافيّ فوق سينٍ له مخزون

        var manager = FindAnyObjectByType<InteractorManager>();
        if (manager == null) return;

        int inScene = FindObjectsByType<WorldInteractor>(FindObjectsInactive.Include,
                                                         FindObjectsSortMode.None).Length;
        int budget = Mathf.Clamp(InteractorManager.MaxInteractors - Headroom - inScene, 0, PoolSize);
        if (budget == 0) return;

        var host = new GameObject("ColorZones") { hideFlags = HideFlags.HideInHierarchy };
        current = host.AddComponent<ColorZones>();

        for (int i = 0; i < budget; i++)
        {
            // الكائن فعّال دائمًا والمكوّن مطفأ وقت الفراغ: المدير يجمع بـFindObjectsByType
            // التي لا ترى الكائنات المطفأة، ويتخطّى المكوّن المطفأ في كل إطار
            var go = new GameObject("Zone" + i) { hideFlags = HideFlags.HideInHierarchy };
            go.transform.SetParent(host.transform, false);
            var it = go.AddComponent<WorldInteractor>();
            it.Scale = FootprintScale;
            it.Radius = 0f;
            it.enabled = false;
            current.zones.Add(new Zone { interactor = it });
        }

        manager.Refresh();
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
        foreach (Zone z in zones) if (z.handle != null) z.handle.zone = null;
    }

    // ---------- المخزون ----------

    private static Zone Take()
    {
        if (current == null) return null;
        foreach (Zone z in current.zones)
            if (!z.Busy && z.interactor != null) return z;
        return null;
    }

    private static void Arm(Zone z, float reach)
    {
        z.interactor.HeightReach = reach >= 0f ? reach : Mathf.Clamp(z.baseRadius * 0.6f, 1.5f, 4f);
        z.interactor.Radius = z.pulsing ? 0f : z.baseRadius;
        z.interactor.transform.position = z.target != null ? z.target.position + z.offset : z.offset;
        z.interactor.enabled = true;
    }

    private static void Free(Zone z)
    {
        if (z.handle != null) z.handle.zone = null;
        z.handle = null;
        z.target = null;
        z.pulsing = false;
        if (z.interactor != null)
        {
            z.interactor.Radius = 0f;
            z.interactor.enabled = false;
        }
    }

    private void LateUpdate()
    {
        float now = Time.time;
        foreach (Zone z in zones)
        {
            if (z.interactor == null || !z.Busy) continue;

            if (z.handle != null)
            {
                if (z.target == null) { Free(z); continue; }   // الهدف دُمّر
                z.interactor.transform.position = z.target.position + z.offset;
                z.interactor.Radius = z.baseRadius;
                continue;
            }

            float t = now - z.start;
            float r;
            if (t < z.grow) r = BackOut(t / z.grow);
            else if (t < z.grow + z.hold) r = 1f;
            else if (t < z.grow + z.hold + z.fade)
            {
                float k = (t - z.grow - z.hold) / z.fade;
                r = 1f - k * k;   // يذوب ببطء ثم يسرع — أنعم من الخطّي
            }
            else { Free(z); continue; }

            z.interactor.Radius = z.baseRadius * r;
        }
    }

    /// <summary>كبرٌ يتجاوز الهدف قليلًا ثم يستقرّ — نبضة لا انزلاق.</summary>
    private static float BackOut(float x)
    {
        const float s = 1.6f;
        x -= 1f;
        return x * x * ((s + 1f) * x + s) + 1f;
    }
}
