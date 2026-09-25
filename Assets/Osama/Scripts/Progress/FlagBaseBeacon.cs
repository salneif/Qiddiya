using TMPro;
using UnityEngine;

/// <summary>
/// دليل قاعدة العلم: <b>دائرة</b> تنبض على الأرض تُرى من بعيد فيعرف اللاعب أين يزرع
/// علمه، و<b>حرف الزر</b> (E) يظهر فوقها لحظة اقترابه. يختفي كل شيء بعد الزرع.
///
/// لا يظهر إلا وهو يحمل علم هذي القاعدة تحديدًا (<see cref="onlyWhenCarrying"/>)،
/// فلا تتزيّن الجزر بدوائر لا معنى لها قبل أن يملك اللاعب شيئًا.
///
/// يُضاف تلقائيًا من <see cref="FlagBase"/>، فلا تحتاج تركيبه. أضِفه يدويًا على
/// القاعدة فقط إن أردت ضبط الأرقام.
/// </summary>
[DisallowMultipleComponent]
public class FlagBaseBeacon : MonoBehaviour
{
    private const string MaterialPath = "OsamaGroundMarker";
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int RingId = Shader.PropertyToID("_Ring");

    [Header("المصادر (تُلتقط تلقائيًا)")]
    [SerializeField] private FlagBase flagBase;
    [SerializeField] private FlagSocket socket;
    [Tooltip("مكان ظهور الدائرة والحرف — يُستخدم مقبس القاعدة إن تُرك فارغًا")]
    [SerializeField] private Transform anchor;

    [Header("الدائرة البعيدة")]
    [Tooltip("لا تظهر إلا واللاعب حامل علم هذي القاعدة. أطفئه لتظهر دائمًا حتى قبل أن يملكه.")]
    [SerializeField] private bool onlyWhenCarrying = true;
    [Tooltip("نصف قطر الدائرة (متر)")]
    [SerializeField] private float ringRadius = 1.6f;
    [Tooltip("سماكة الحلقة — 1 يجعلها قرصًا ممتلئًا")]
    [Range(0.05f, 1f)]
    [SerializeField] private float ringWidth = 0.25f;
    [Tooltip("لون الدليل. مع Use Flag Color يأخذ لون ضوء العلم نفسه.")]
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.4f, 0.85f);
    [Tooltip("خذ اللون من ضوء العلم (FlagGlow) فيتطابق الدليل مع علمه")]
    [SerializeField] private bool useFlagColor = true;
    [Tooltip("نبضات الدائرة في الثانية")]
    [SerializeField] private float pulseSpeed = 1.2f;
    [Tooltip("ارتفاع الدائرة عن الأرض — يمنع وميضها مع الأرضية")]
    [SerializeField] private float groundOffset = 0.03f;

    [Header("حرف الزر عند الاقتراب")]
    [Tooltip("المسافة التي يظهر عندها الحرف (متر)")]
    [SerializeField] private float promptDistance = 3f;
    [Tooltip("ارتفاع الحرف فوق نقطة الزرع (متر)")]
    [SerializeField] private float promptHeight = 1.8f;
    [Tooltip("حجم الحرف")]
    [SerializeField] private float promptSize = 1.4f;
    [Tooltip("تختفي الدائرة وهو قريب فلا يزدحم المشهد")]
    [SerializeField] private bool hideRingWhenClose = true;

    [Header("عام")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("سرعة الظهور والاختفاء")]
    [SerializeField] private float fadeSpeed = 4f;

    private Transform player;
    private Camera cam;

    private Transform ring;
    private MeshRenderer ringRenderer;
    private Mesh ringMesh;
    private MaterialPropertyBlock block;

    private TextMeshPro prompt;
    private TextMesh legacyPrompt;      // بديل لو ما في خط TMP معيّن
    private MeshRenderer promptRenderer;

    private float ringAlpha;
    private float promptAlpha;
    private bool promptReported;

    private void Awake()
    {
        if (flagBase == null) flagBase = GetComponent<FlagBase>();
        if (socket == null) socket = GetComponent<FlagSocket>();
        if (socket == null) socket = GetComponentInParent<FlagSocket>();
        if (socket == null) socket = GetComponentInChildren<FlagSocket>(true);
        if (anchor == null && socket != null) anchor = socket.PlacePoint;
        if (anchor == null) anchor = transform;

        if (useFlagColor) TakeFlagColor();

        BuildRing();
        BuildPrompt();
    }

    /// <summary>لون ضوء العلم (FlagGlow) ليتطابق دليل القاعدة مع علمها.</summary>
    private void TakeFlagColor()
    {
        if (socket == null || socket.Flag == null) return;

        var light = socket.Flag.GetComponentInChildren<Light>(true);
        if (light == null) return;

        color = new Color(light.color.r, light.color.g, light.color.b, color.a);
    }

    private void BuildRing()
    {
        var material = Resources.Load<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"[FlagBaseBeacon] ما لقيت الماتيريال Resources/{MaterialPath}.", this);
            return;
        }

        ringMesh = BuildQuad();

        var go = new GameObject("FlagBaseBeacon_Ring");
        go.layer = 2; // Ignore Raycast
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = ringMesh;

        ringRenderer = go.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = material;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        ringRenderer.enabled = false;

        ring = go.transform;
        block = new MaterialPropertyBlock();
    }

    private static Mesh BuildQuad()
    {
        var mesh = new Mesh { name = "FlagBaseBeacon_Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f,  0.5f), new Vector3(0.5f, 0f,  0.5f)
        };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// حرف الزر وحده بلا كلمة "اضغط" — أوضح وأقل ضجيجًا.
    ///
    /// TextMeshPro بلا خط معيّن لا يرسم شيئًا ويملأ الكونسول تحذيرات، وإعدادات TMP
    /// في هذا المشروع بلا خط افتراضي. فنحمّل خط المشروع من Resources، وإن لم يوجد
    /// نسقط على TextMesh القديم بخط يونيتي المدمج — يشتغل دائمًا وفي البلد أيضًا.
    /// </summary>
    private void BuildPrompt()
    {
        var go = new GameObject("FlagBaseBeacon_Key");
        go.layer = 2;
        go.transform.SetParent(transform, false);

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF");

        if (font != null)
        {
            prompt = go.AddComponent<TextMeshPro>();
            prompt.font = font;
            prompt.text = KeyLabel();
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.fontSize = 6f;
            prompt.enableWordWrapping = false;
            prompt.color = color;
            prompt.rectTransform.sizeDelta = new Vector2(2f, 2f);
        }
        else
        {
            legacyPrompt = go.AddComponent<TextMesh>();
            legacyPrompt.text = KeyLabel();
            legacyPrompt.anchor = TextAnchor.MiddleCenter;
            legacyPrompt.alignment = TextAlignment.Center;
            legacyPrompt.fontSize = 64;
            legacyPrompt.characterSize = 0.12f;
            legacyPrompt.color = color;

            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin != null)
            {
                legacyPrompt.font = builtin;
                go.GetComponent<MeshRenderer>().sharedMaterial = builtin.material;
            }
        }

        promptRenderer = go.GetComponent<MeshRenderer>();
        if (promptRenderer != null) promptRenderer.enabled = false;
    }

    /// <summary>حرف زر الوضع من المقبس، أو E إن لم يوجد.</summary>
    private string KeyLabel()
    {
        if (socket == null) return "E";
        string k = socket.PlaceKey.ToString();
        return k.Length == 1 ? k : k.Replace("Key", "").ToUpperInvariant();
    }

    private void LateUpdate()
    {
        bool wantRing = false;
        bool wantPrompt = false;

        if (ShouldGuide())
        {
            float distance = Vector3.Distance(player.position, anchor.position);
            bool close = distance <= promptDistance;

            // المقبس قد يكون على كائن آخر — غيابه لا يمنع الحرف، فالزر الافتراضي E
            wantPrompt = close && (socket == null || !socket.AutoPlace);
            wantRing = !(close && hideRingWhenClose);
        }

        if (wantPrompt && !promptReported)
        {
            promptReported = true;
            string label = prompt != null ? prompt.text : (legacyPrompt != null ? legacyPrompt.text : "?");
            string fontName = prompt != null && prompt.font != null ? prompt.font.name
                            : legacyPrompt != null ? "TextMesh المدمج" : "مفقود";
            Debug.Log($"[FlagBaseBeacon] الحرف \"{label}\" يظهر فوق {anchor.name} — " +
                      $"الخط: {fontName}، الكاميرا: {(cam != null ? cam.name : "غير موجودة")}.", this);
        }

        ringAlpha = Mathf.MoveTowards(ringAlpha, wantRing ? 1f : 0f, fadeSpeed * Time.deltaTime);
        promptAlpha = Mathf.MoveTowards(promptAlpha, wantPrompt ? 1f : 0f, fadeSpeed * Time.deltaTime);

        DrawRing();
        DrawPrompt();
    }

    /// <summary>هل نرشد اللاعب الآن؟ (القاعدة فارغة، والعلم معه، وهو في السين)</summary>
    private bool ShouldGuide()
    {
        if (flagBase != null && flagBase.IsPlanted) return false;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return false;
            player = go.transform;
        }

        if (!onlyWhenCarrying) return true;
        if (flagBase == null) return true;

        return GameProgress.Instance.IsCarrying(flagBase.Id);
    }

    private void DrawRing()
    {
        if (ring == null) return;

        if (ringAlpha <= 0.001f)
        {
            ringRenderer.enabled = false;
            return;
        }

        // نبضة حجم خفيفة تلفت النظر من بعيد بلا إزعاج
        float pulse = 1f + 0.06f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);
        float size = ringRadius * 2f * pulse;

        ring.position = anchor.position + Vector3.up * groundOffset;
        ring.rotation = Quaternion.identity;
        ring.localScale = Vector3.one;
        ring.localScale = new Vector3(SafeDiv(size, transform.lossyScale.x),
                                      SafeDiv(size, transform.lossyScale.y),
                                      SafeDiv(size, transform.lossyScale.z));

        block.SetColor(ColorId, new Color(color.r, color.g, color.b, color.a * ringAlpha));
        block.SetFloat(SoftnessId, 0.25f);
        block.SetFloat(RingId, ringWidth);
        ringRenderer.SetPropertyBlock(block);
        ringRenderer.enabled = true;
    }

    private void DrawPrompt()
    {
        if (promptRenderer == null) return;

        Transform text = promptRenderer.transform;
        if (promptAlpha <= 0.001f)
        {
            promptRenderer.enabled = false;
            return;
        }

        if (cam == null || !cam.isActiveAndEnabled) cam = ResolveCamera();

        Vector3 pos = anchor.position + Vector3.up * promptHeight;
        text.position = pos;
        text.localScale = Vector3.one * promptSize;

        // يواجه الكاميرا دائمًا فيُقرأ من أي زاوية
        if (cam != null)
            text.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);

        Color c = new Color(color.r, color.g, color.b, promptAlpha);
        if (prompt != null) prompt.color = c;
        if (legacyPrompt != null) legacyPrompt.color = c;

        promptRenderer.enabled = true;
    }

    /// <summary>
    /// كاميرا العرض: <c>Camera.main</c> تعود null إن لم يكن أحد موسومًا MainCamera،
    /// وعندها يبقى الحرف بدورانه الأصلي فيُرى من حرفه — أي لا يُرى.
    /// </summary>
    private Camera ResolveCamera()
    {
        if (Camera.main != null) return Camera.main;

        foreach (var c in Camera.allCameras)
            if (c != null && c.isActiveAndEnabled) return c;

        return null;
    }

    private static float SafeDiv(float a, float b) => Mathf.Abs(b) > 0.0001f ? a / b : a;

    private void OnDestroy()
    {
        if (ringMesh != null) Destroy(ringMesh);
    }

    private void OnValidate()
    {
        ringRadius = Mathf.Max(0.1f, ringRadius);
        promptDistance = Mathf.Max(0.5f, promptDistance);
        fadeSpeed = Mathf.Max(0.1f, fadeSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Transform a = anchor != null ? anchor : transform;

        Gizmos.color = new Color(color.r, color.g, color.b, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(a.position, Quaternion.identity, new Vector3(1f, 0.02f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, ringRadius);
        Gizmos.matrix = Matrix4x4.identity;

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(a.position, promptDistance);
    }
}
