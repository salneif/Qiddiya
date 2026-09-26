using UnityEngine;

/// <summary>
/// يجعل فخًّا قاتلًا <b>يبدو</b> قاتلًا: علامة على الأرض بمقاس الكولايدر القاتل
/// نفسه، وتوهّج ينبض، وضوء، وهمهمة تعلو كلما اقتربت.
///
/// المشكلة التي يحلّها: <c>LavaTrigger</c> وأمثاله يقتلون بلمسة، لكن اللاعب لا يرى
/// حدود القتل ولا يشعر أن هناك خطرًا أصلًا — فيموت ويظن أن اللعبة ظلمته. الموت
/// المقبول هو الذي يراه اللاعب قادمًا.
///
/// مستقلّ تمامًا: لا يعرف سكربت القتل ولا يتدخّل فيه، فيصلح فوق أي فخّ من أي أحد.
/// ضعه على نفس كائن التريغر القاتل وكفى — يقرأ الكولايدر بنفسه.
///
/// العلامة تُرسم بشيدر <c>Osama/GroundMarker</c> من <c>Resources</c>، فتدخل البلد
/// دائمًا ولا تحتاج أي صورة.
/// </summary>
[DisallowMultipleComponent]
public class DangerZone : MonoBehaviour
{
    private const string MaterialPath = "OsamaGroundMarker";
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int RingId = Shader.PropertyToID("_Ring");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    [Header("المنطقة")]
    [Tooltip("الكولايدر القاتل — يُلتقط من نفس الكائن إن تُرك فارغًا، وتُقاس منه العلامة")]
    [SerializeField] private Collider area;
    [Tooltip("زيادة على مقاس الكولايدر (متر) — العلامة أوسع قليلًا من القتل نفسه، " +
             "فمن يقف على حافتها ما زال حيًّا")]
    [SerializeField] private float margin = 0.35f;
    [Tooltip("ارتفاع العلامة عن أسفل الكولايدر، فلا تغوص في الأرض ولا تطفو")]
    [SerializeField] private float groundOffset = 0.03f;

    [Header("الشكل")]
    [Tooltip("لون الخطر")]
    [SerializeField] private Color color = new Color(1f, 0.25f, 0.05f, 0.75f);
    [Tooltip("سمك الحلقة: صفر = بقعة مصمتة، 1 = خط رفيع")]
    [Range(0f, 1f)] [SerializeField] private float ring = 0.55f;
    [Tooltip("نعومة الحافة")]
    [Range(0.01f, 1f)] [SerializeField] private float softness = 0.3f;

    [Header("النبض")]
    [Tooltip("نبضة في الثانية — النبض يقول «حيّ وخطير»، والثابت يقول «زينة»")]
    [SerializeField] private float pulsesPerSecond = 1.1f;
    [Tooltip("أخفت ما تصل إليه الشدّة أثناء النبض")]
    [Range(0f, 1f)] [SerializeField] private float dimTo = 0.45f;

    [Header("التوهّج")]
    [Tooltip("رِندَرات الحِمَم — يُضاف إليها توهّج نابض بلون الخطر")]
    [SerializeField] private Renderer[] glowRenderers;
    [Tooltip("شدّة التوهّج")]
    [SerializeField] private float glowStrength = 2.5f;
    [Tooltip("ضوء ينبض مع العلامة — يُنشأ تلقائيًا إن تُرك فارغًا وفُعّل الخيار")]
    [SerializeField] private Light dangerLight;
    [Tooltip("ينشئ ضوءًا إن لم يكن هناك واحد")]
    [SerializeField] private bool createLight = true;
    [Tooltip("أقصى شدّة للضوء")]
    [SerializeField] private float lightIntensity = 3f;

    [Header("الصوت")]
    [Tooltip("همهمة مستمرة تعلو كلما اقتربت — أقوى إنذار، يعمل والّلاعب ناظر لغير هنا")]
    [SerializeField] private AudioClip warningLoop;
    [Range(0f, 1f)] [SerializeField] private float warningVolume = 0.5f;
    [Tooltip("المسافة التي تبدأ عندها الهمهمة تُسمع")]
    [SerializeField] private float hearingRange = 12f;
    [SerializeField] private string playerTag = "Player";

    private Transform marker;
    private MeshRenderer markerRenderer;
    private MaterialPropertyBlock block;
    private AudioSource warningSource;
    private Transform player;
    private MaterialPropertyBlock glowBlock;

    private void Awake()
    {
        if (area == null) area = GetComponent<Collider>();
        if (area == null)
        {
            Debug.LogWarning("[DangerZone] ما فيه كولايدر — ما قدرت أعرف حدود الخطر.", this);
            enabled = false;
            return;
        }

        BuildMarker();
        BuildLight();
        BuildSound();
    }

    /// <summary>
    /// مربّع ملصق بالأرض بمقاس الكولايدر. يُقاس من <c>bounds</c> لا من المقاس المحلّي:
    /// الفخاخ مائلة ومكبّرة بمقاسات غريبة، والـ<c>bounds</c> تعطي الحيّز الحقيقي.
    /// </summary>
    private void BuildMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "DangerZone_Marker";
        go.layer = 2;   // Ignore Raycast
        Destroy(go.GetComponent<Collider>());

        marker = go.transform;
        marker.SetParent(transform, false);

        markerRenderer = go.GetComponent<MeshRenderer>();
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;

        var material = Resources.Load<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"[DangerZone] ما لقيت \"{MaterialPath}\" في Resources.", this);
            enabled = false;
            return;
        }

        markerRenderer.sharedMaterial = material;
        block = new MaterialPropertyBlock();
        glowBlock = new MaterialPropertyBlock();

        LayoutMarker();
    }

    private void LayoutMarker()
    {
        if (marker == null || area == null) return;

        Bounds bounds = area.bounds;
        float side = Mathf.Max(bounds.size.x, bounds.size.z) + margin * 2f;

        // بفضاء العالم: الأب قد يكون مكبّرًا أو مائلًا، والعلامة يجب أن تبقى مستوية
        marker.position = new Vector3(bounds.center.x, bounds.min.y + groundOffset, bounds.center.z);
        marker.rotation = Quaternion.Euler(90f, 0f, 0f);

        Vector3 scale = transform.lossyScale;
        marker.localScale = new Vector3(
            Div(side, scale.x), Div(side, scale.z), Div(1f, scale.y));
    }

    private void BuildLight()
    {
        if (dangerLight == null && createLight)
        {
            var go = new GameObject("DangerZone_Light");
            go.transform.SetParent(transform, false);
            go.transform.position = area.bounds.center + Vector3.up * 0.5f;

            dangerLight = go.AddComponent<Light>();
            dangerLight.type = LightType.Point;
            dangerLight.range = Mathf.Max(area.bounds.size.x, area.bounds.size.z) + 4f;
            dangerLight.shadows = LightShadows.None;   // فخّ صغير لا يستحق ظلالًا
        }

        if (dangerLight != null) dangerLight.color = color;
    }

    private void BuildSound()
    {
        if (warningLoop == null) return;

        warningSource = gameObject.AddComponent<AudioSource>();
        warningSource.clip = warningLoop;
        warningSource.loop = true;
        warningSource.playOnAwake = false;
        warningSource.volume = 0f;
        // ثنائي الأبعاد والمسافة بالكود: كاميرا اللعبة بعيدة عن اللاعب، والصوت
        // المجسّم معها يُقاس من الكاميرا لا من اللاعب فلا يكاد يُسمع
        warningSource.spatialBlend = 0f;
        warningSource.Play();
    }

    private void LateUpdate()
    {
        LayoutMarker();

        float wave = Mathf.Sin(Time.time * pulsesPerSecond * Mathf.PI * 2f) * 0.5f + 0.5f;
        float strength = Mathf.Lerp(dimTo, 1f, wave);

        if (markerRenderer != null)
        {
            block.SetColor(ColorId, new Color(color.r, color.g, color.b, color.a * strength));
            block.SetFloat(SoftnessId, softness);
            block.SetFloat(RingId, ring);
            markerRenderer.SetPropertyBlock(block);
        }

        if (dangerLight != null) dangerLight.intensity = lightIntensity * strength;

        Glow(strength);
        Warn();
    }

    /// <summary>توهّج على رِندَرات الحِمَم بـ MaterialPropertyBlock، فلا تُمسّ مادّتها المشتركة.</summary>
    private void Glow(float strength)
    {
        if (glowRenderers == null || glowRenderers.Length == 0) return;

        Color emission = color * (glowStrength * strength);
        foreach (Renderer renderer in glowRenderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(glowBlock);
            glowBlock.SetColor(EmissionId, emission);
            renderer.SetPropertyBlock(glowBlock);
        }
    }

    private void Warn()
    {
        if (warningSource == null) return;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return;
            player = go.transform;
        }

        float distance = Vector3.Distance(player.position, area.bounds.center);
        float near = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, hearingRange));
        warningSource.volume = warningVolume * near * near;   // تربيع: تهدأ من بعيد وتشتد عند الحافة
    }

    private static float Div(float a, float b) => Mathf.Abs(b) > 0.0001f ? a / b : a;

    private void OnValidate()
    {
        margin = Mathf.Max(0f, margin);
        hearingRange = Mathf.Max(0.5f, hearingRange);
        pulsesPerSecond = Mathf.Max(0f, pulsesPerSecond);
    }

    private void OnDrawGizmosSelected()
    {
        Collider c = area != null ? area : GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.5f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(c.bounds.center, hearingRange);
    }
}
