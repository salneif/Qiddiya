using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

/// <summary>
/// يُلبس اللاعب زيّه: صبغةٌ على جسمه، وهالة لونٍ حوله، ومؤثّراته — في كل سين وبعد
/// كل موت، بلا ربط.
///
/// <b>الصبغة بـ<see cref="MaterialPropertyBlock"/></b> على رندررات الجسم وحدها: المادّة
/// مشتركة، ولو غيّرناها لصبغنا كل من يلبسها. والرندررات من شجرة <see cref="Animator"/>
/// الشخصية لحظة البحث، بلا علمٍ محمول — العلم يُلصق باللاعب نفسه.
///
/// <b>وحين تتبدّل مادّة الجسم تُرفع الصبغة</b>: احتراق الموت (<see cref="DeathDissolveEffect"/>)
/// ومادّة موت علي في التوايلايت يضعان مادّتهما مكان مادّة الجسم، والكتلة باقيةٌ على
/// الرندرر — فلو بقيت لصبغت الرماد بلون الزيّ. المادّة الأصلية تُعرف بشيدرها ونسيجها،
/// فنسختها (<c>renderer.material</c>) تُعرف أيضًا فتعود الصبغة معها.
///
/// <b>والهالة</b> منطقة لونٍ واحدة تتبع اللاعب (<see cref="ColorZones.Follow"/>)، فيُرى زيّه
/// ملوّنًا في العالم الرمادي. والمؤثّرات جسيماتٌ تُبنى مرّة وتعيش مع هذا الكائن بين
/// السينات، تتبع اللاعب موضعًا لا أبوّة: كائن اللاعب يموت مع سينه.
///
/// يُركّبه <see cref="ChromaSkins"/>.
/// </summary>
[DisallowMultipleComponent]
public class ChromaSkinWearer : MonoBehaviour
{
    private const float AuraRadius = 1f;
    private const float AuraReach = 1.3f;
    private const bool AuraEnabled = false;
    /// <summary>في سين الأمان (السيرك) الهالة تلوّن الجسد وحده، بلا حلقةٍ على الأرض توهم بالحماية.</summary>
    private const float SafeAuraRadius = 0.35f, SafeAuraReach = 1f;
    private const float AuraRetry = 1f;

    /// <summary>البحث عن اللاعب حين يغيب — نصف ثانية تكفي، وفي القائمة والانترو لا لاعب أصلًا.</summary>
    private const float FindEvery = 0.5f;

    /// <summary>أبعد من هذا في إطارٍ واحد نقلٌ لا مشي (عودةٌ بعد موت، بوابة).</summary>
    private const float Teleport = 4f;

    private const float JumpSpeed = 1.2f;
    private const float AirKick = 4f;        // قفزةٌ ثانية في الهواء: السرعة تقفز فجأة
    private const float MinAirTime = 0.18f;  // أقصر من هذا ارتجافُ أرضٍ لا هبوط

    private const float ShowcaseEvery = 2.4f;
    private const float FlashSeconds = 0.35f;
    private const float BreathSeconds = 0.7f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private static ChromaSkinWearer instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>في السين لاعبٌ فعّال نُلبسه.</summary>
    public static bool HasPlayer => instance != null && instance.Present;

    /// <summary>
    /// لاعبٌ حيّ بجسمه وفي يده: لا ميّت، ولا في مادّة موت، ولا كونترولره مطفأ — مشهدٌ يقوده
    /// (شفط البوابة في السيرك يطفئه ويسحبه بالوقت الحقيقي، وموت علي يطفئه ثانيتين). النقل
    /// يطفئه ويشعله في النداء نفسه، فلا يُرى مطفأً.
    /// </summary>
    public static bool PlayerReady => HasPlayer && !instance.dead && !instance.altered &&
                                      (instance.controller == null || instance.controller.enabled);

    /// <summary>لحظة اللبس: ومضةٌ على الجسم، والهالة تتّسع وتعود، ودفعةٌ من المؤثّر.</summary>
    public static void Celebrate()
    {
        if (instance == null) return;
        instance.celebratedAt = Time.unscaledTime;
        instance.showcaseBig = true;
    }

    /// <summary>
    /// أين منتصف جسم اللاعب على عرض الشاشة (٠ يسار، ١ يمين) — لتضع الخزانة لوحتها في الجهة
    /// الأخرى. false = لا لاعب، أو لا كاميرا، أو هو خلفها.
    /// </summary>
    public static bool TryViewportX(out float x)
    {
        x = 0.5f;
        Camera view = Camera.main;
        if (!HasPlayer || view == null) return false;

        Vector3 middle = instance.body.position + Vector3.up * (instance.feet + instance.height * 0.5f);
        Vector3 point = view.WorldToViewportPoint(middle);
        if (point.z <= 0f) return false;

        x = point.x;
        return true;
    }

    /// <summary>رندررٌ نصبغه، وما نعرف به مادّته الأصلية.</summary>
    private sealed class Worn
    {
        public Renderer renderer;
        public int colorId;
        public Color baseColor;
        public Shader shader;
        public Texture baseMap;
        public Material seen;
        public bool fits = true;
        public bool painted;
        public Color paintedColor;
    }

    private readonly List<Worn> worn = new List<Worn>();
    private readonly List<Renderer> found = new List<Renderer>();
    private readonly ChromaSkinRig[] rigs = new ChromaSkinRig[ChromaSkins.Count];
    private readonly bool[] rigTried = new bool[ChromaSkins.Count];
    private MaterialPropertyBlock block;
    private Transform fxRoot;

    private GameObject player;
    private Transform body;
    private CharacterController controller;
    private PlayerKillable killable;
    private bool dressed, dead, altered;
    private float feet, height = 1f;
    private float nextFindAt;

    private ColorZones.Handle aura;
    private float nextAuraAt;

    private int shown = -1;
    private Color paint = Color.white;
    private bool snapPaint;
    private float hueTime;
    private float celebratedAt = -10f;
    private bool showcaseNow, showcaseBig;
    private float nextShowcaseAt;

    private int live;              // الزيّ الذي تعمل مؤثّراته الآن (٠ = لا شيء)
    private bool unscaledFx;
    private Vector3 lastPosition;
    private bool grounded = true;
    private float airborneAt, lowestFall, lastFall;

    private bool Present => player != null && body != null && player.activeInHierarchy;

    private void Awake()
    {
        instance = this;
        block = new MaterialPropertyBlock();
        fxRoot = new GameObject("SkinFx").transform;
        fxRoot.SetParent(transform, false);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Undress();
        foreach (ChromaSkinRig rig in rigs) rig?.Clear();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>السين الجديد بلاعبٍ جديد، ولا يبقى من جسيمات القديم شيء في إحداثيّاته.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        Undress();
        foreach (ChromaSkinRig rig in rigs) rig?.Clear();
        nextFindAt = nextAuraAt = 0f;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevKeys();
#endif
        if (Present || Time.unscaledTime < nextFindAt) return;

        nextFindAt = Time.unscaledTime + FindEvery;
        GameObject candidate = PlayerLocator.Find("Player");
        if (candidate != null) Dress(candidate);
    }

    /// <summary>
    /// بعد <c>Update</c> كله: اللاعب تحرّك، ومؤثّر الموت بدّل المادّة إن مات — فنرى
    /// حال الإطار نفسه قبل أن يُرسم.
    /// </summary>
    private void LateUpdate()
    {
        if (!Present)
        {
            if (dressed) Undress();
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        bool wasDead = dead;
        dead = killable != null && killable.IsDead;

        int skin = ChromaSkins.Shown;
        if (skin != shown)
        {
            shown = skin;
            showcaseNow = true;
        }

        // الموشور يدور بوقت اللعب، وبالوقت الحقيقي والخزانة موقفةٌ له لتعرضه
        hueTime += ChromaWardrobe.IsOpen ? Time.unscaledDeltaTime : Time.deltaTime;
        ChromaSkin look = ChromaSkins.Get(skin);

        Paint(look, dt);

        float since = Time.unscaledTime - celebratedAt;
        // أسامة: اللون للأعلام وحدها — هي النور، وهي ما يُبعد الفئران. اللاعب بلا هالة لون؛
        // لون زيّه يُرى حين يدخل نور علم. (AuraEnabled لإعادتها يومًا)
        Aura(AuraEnabled && skin != 0, since < BreathSeconds ? Mathf.Sin(since / BreathSeconds * Mathf.PI) : 0f);
        Effects(skin, !ChromaEvents.Quiet && !dead && !altered, wasDead && !dead);
    }

    // ---------- اللاعب ----------

    private void Dress(GameObject candidate)
    {
        Undress();

        player = candidate;
        controller = candidate.GetComponentInParent<CharacterController>();
        hopY = float.NaN;
        body = controller != null ? controller.transform : candidate.transform;
        killable = candidate.GetComponentInParent<PlayerKillable>();

        Animator animator = body.GetComponentInChildren<Animator>();
        Transform model = animator != null ? animator.transform : body;

        model.GetComponentsInChildren(false, found);
        foreach (Renderer r in found)
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            if (Carried(r.transform)) continue;

            Material material = r.sharedMaterial;
            if (material == null) continue;

            bool hasBase = material.HasProperty(BaseColorId);
            if (!hasBase && !material.HasProperty(ColorId)) continue;

            int id = hasBase ? BaseColorId : ColorId;
            worn.Add(new Worn
            {
                renderer = r,
                colorId = id,
                baseColor = material.GetColor(id),
                shader = material.shader,
                baseMap = MainTexture(material),
                seen = material,
            });
        }
        found.Clear();

        Measure();
        lastPosition = body.position;
        grounded = true;
        lastFall = 0f;
        shown = -1;
        snapPaint = true;      // لاعبٌ جديد يظهر بزيّه فورًا، لا يتلوّن أمام العين
        dressed = true;
    }

    /// <summary>
    /// يُفلت كل ما وضعناه: الهالة، والمؤثّر، والصبغة عن جسمٍ ما زال موجودًا (مُطفأ مثلًا)
    /// — فلا يبقى عليه لونٌ لا يُحدَّث.
    /// </summary>
    private void Undress()
    {
        ReleaseAura();
        if (live > 0) rigs[live]?.SetActive(false);
        live = 0;

        foreach (Worn w in worn)
            if (w.renderer != null && w.painted) w.renderer.SetPropertyBlock(null);
        worn.Clear();

        player = null;
        body = null;
        controller = null;
        hopY = float.NaN;
        killable = null;
        dressed = dead = altered = false;
    }

    /// <summary>علمٌ محمول أو ما يشبهه: يُلصق باللاعب لكنه ليس ثيابه.</summary>
    private static bool Carried(Transform t) =>
        t.GetComponentInParent<FlagItem>() != null ||
        t.GetComponentInParent<FlagCarry>() != null ||
        t.GetComponentInParent<ExternalFlagPickup>() != null;

    /// <summary>
    /// طول الشخصية وموضع قدميها من حدود جسمها: مركز الكونترولر ليس القدمين (هنا
    /// في منتصف الجسم)، والمؤثّرات تُصمَّم بطولٍ واحد وتُمدّ بطولها الحقيقي.
    /// </summary>
    private void Measure()
    {
        bool any = false;
        Bounds bounds = default;
        foreach (Worn w in worn)
        {
            if (!w.renderer.enabled) continue;   // المطفأ حدوده قديمة أو صفر
            if (!any) bounds = w.renderer.bounds;
            else bounds.Encapsulate(w.renderer.bounds);
            any = true;
        }

        if (any && bounds.size.y > 0.2f)
        {
            feet = bounds.min.y - body.position.y;
            height = Mathf.Clamp(bounds.size.y, 0.5f, 3f);
        }
        else if (controller != null)
        {
            feet = controller.center.y - controller.height * 0.5f;
            height = Mathf.Clamp(controller.height, 0.5f, 3f);
        }
        else
        {
            feet = 0f;
            height = 1.8f;
        }
    }

    // ---------- الصبغة ----------

    private void Paint(ChromaSkin look, float dt)
    {
        Color target = Color.Lerp(Color.white, look.TintAt(hueTime), look.Strength);
        paint = snapPaint ? target : ChromaWardrobeArt.Damp(paint, target, 12f, dt);
        snapPaint = false;

        float since = Time.unscaledTime - celebratedAt;
        float flash = since < FlashSeconds ? Mathf.Sin(since / FlashSeconds * Mathf.PI) * 0.7f : 0f;
        Color shade = paint * (1f + flash);

        // الأصليّ بلا كتلةٍ أصلًا: لا نترك أثرًا حين لا نغيّر شيئًا
        bool plain = flash <= 0f && Mathf.Abs(shade.r - 1f) + Mathf.Abs(shade.g - 1f) + Mathf.Abs(shade.b - 1f) < 0.004f;

        altered = false;
        foreach (Worn w in worn)
        {
            if (w.renderer == null) continue;

            Material now = w.renderer.sharedMaterial;
            if (now != w.seen)
            {
                w.seen = now;
                w.fits = now != null && now.shader == w.shader && MainTexture(now) == w.baseMap;
                if (w.fits) w.baseColor = now.GetColor(w.colorId);
            }

            if (!w.fits || plain)
            {
                if (!w.fits) altered = true;
                if (w.painted)
                {
                    w.renderer.SetPropertyBlock(null);
                    w.painted = false;
                }
                continue;
            }

            Color c = w.baseColor * shade;
            c.a = w.baseColor.a;
            if (w.painted && c == w.paintedColor) continue;

            block.Clear();
            block.SetColor(w.colorId, c);
            w.renderer.SetPropertyBlock(block);
            w.painted = true;
            w.paintedColor = c;
        }
    }

    private static Texture MainTexture(Material material)
    {
        if (material.HasProperty(BaseMapId)) return material.GetTexture(BaseMapId);
        return material.HasProperty(MainTexId) ? material.GetTexture(MainTexId) : null;
    }

    // ---------- الهالة ----------

    /// <summary>
    /// منطقةٌ واحدة تتبع اللاعب ما دام زيّه ملوّنًا. مخزون المناطق مشترك ومحدود، فإن
    /// نفد نعيد المحاولة كل ثانية بصمت — والمنطقة تموت مع سينها فتُطلب في الجديد.
    /// </summary>
    private void Aura(bool want, float breath)
    {
        if (!want)
        {
            ReleaseAura();
            return;
        }

        if ((aura == null || !aura.Alive) && Time.unscaledTime >= nextAuraAt)
        {
            aura = ColorZones.Follow(body, new Vector3(0f, feet + height * 0.5f, 0f),
                                     ColorZones.SafetyScene ? SafeAuraRadius : AuraRadius,
                                     ColorZones.SafetyScene ? SafeAuraReach : AuraReach);
            if (aura == null) nextAuraAt = Time.unscaledTime + AuraRetry;
        }

        if (aura != null && aura.Alive)
            aura.Radius = ColorZones.SafetyScene ? SafeAuraRadius : AuraRadius * (1f + 0.9f * breath);
    }

    private void ReleaseAura()
    {
        ColorZones.Release(aura);
        aura = null;
    }

    // ---------- المؤثّرات ----------

    private void Effects(int skin, bool allowed, bool revived)
    {
        int want = allowed ? skin : 0;
        if (want != live)
        {
            if (live > 0) rigs[live]?.SetActive(false);
            live = want;
            if (live > 0) Rig(live)?.SetActive(true);
        }

        bool unscaled = ChromaWardrobe.IsOpen;
        if (unscaled != unscaledFx)
        {
            unscaledFx = unscaled;
            foreach (ChromaSkinRig r in rigs) r?.UseUnscaledTime(unscaled);
        }

        Vector3 position = body.position;
        Vector3 step = position - lastPosition;
        lastPosition = position;

        if (revived) grounded = true;   // العودة بعد الموت نقلٌ لا هبوط

        bool showcase = showcaseNow || showcaseBig ||
                        (ChromaWardrobe.IsOpen && Time.unscaledTime >= nextShowcaseAt);
        bool big = showcaseBig;
        showcaseNow = showcaseBig = false;
        if (showcase) nextShowcaseAt = Time.unscaledTime + ShowcaseEvery;

        ChromaSkinRig rig = live > 0 ? rigs[live] : null;
        if (rig == null)
        {
            Hops(null);
            return;
        }

        Vector3 soles = position + Vector3.up * feet;
        rig.Place(soles, body.eulerAngles.y, height, ChromaSkin.HueAt(hueTime));

        float distance = step.magnitude;
        if (Time.deltaTime > 0f && distance < Teleport) rig.Moved(soles - step, distance);
        Hops(rig);

        // اللاعب في الخزانة واقفٌ والزمن موقوف: ما يُطلق بالحركة لا يُرى إلا بعرض
        if (showcase && (ChromaWardrobe.IsOpen || big)) rig.Showcase(big);
    }

    private ChromaSkinRig Rig(int skin)
    {
        if (!rigTried[skin])
        {
            rigTried[skin] = true;
            rigs[skin] = ChromaSkinFx.Build(skin, fxRoot);
            rigs[skin]?.UseUnscaledTime(unscaledFx);
        }
        return rigs[skin];
    }

    /// <summary>
    /// القفز والهبوط من الكونترولر نفسه: يعمل مع أي سكربت حركة، ومع منصّات القفز.
    /// لا يُقرأ والزمن موقوف: الكونترولر لا يتحرّك فيظنّ نفسه في الهواء.
    /// </summary>
    private float hopY = float.NaN;

    private void Hops(ChromaSkinRig rig)
    {
        if (controller == null || !controller.enabled || Time.deltaTime <= 0f) return;

        // الأرض كما يراها سكربت الحركة، والسرعة من الموضع: قوارب التوايلايت تحرّك الكونترولر
        // بصفرٍ كل إطار فتمسح isGrounded وvelocity معًا
        bool onGround = JumpPolish.Active ? JumpPolish.Grounded : controller.isGrounded;
        float y = controller.transform.position.y;
        float fall = float.IsNaN(hopY) ? 0f : (y - hopY) / Time.deltaTime;
        hopY = y;

        if (grounded && !onGround)
        {
            airborneAt = Time.time;
            lowestFall = 0f;
            if (fall > JumpSpeed) rig?.Jumped();
        }
        else if (!grounded && !onGround && fall - lastFall > AirKick && fall > JumpSpeed)
        {
            rig?.Jumped();
        }
        else if (!grounded && onGround && Time.time - airborneAt > MinAirTime)
        {
            rig?.Landed(Mathf.InverseLerp(3f, 14f, -lowestFall));
        }

        if (!onGround) lowestFall = Mathf.Min(lowestFall, fall);
        grounded = onGround;
        lastFall = fall;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// للتجربة وحدها، ولا تدخل البلد النهائي: F9 = مئة قطرة (تفتح الأزياء وتُظهر
    /// لافتتها)، F10 = الزيّ التالي ولو كان مقفلًا.
    /// </summary>
    private static void DevKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f9Key.wasPressedThisFrame) ChromaBank.Add(100, ChromaEvents.PlayerPosition());
        if (keyboard.f10Key.wasPressedThisFrame)
            ChromaSkins.EquipEvenIfLocked((ChromaSkins.Equipped + 1) % ChromaSkins.Count);
    }
#endif
}
