using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// لوحة تحذير تملأ الشاشة قبل خطر لأول مرة — صورة شفافة يوفّرها المصمّم، تُتجاوز
/// بأي زر.
///
/// لماذا لوحة وليست علامة في العالم: الخطر الذي لم يُرَ من قبل لا يكفي فيه تلميح
/// جانبي. اللوحة توقف اللعب وتجبر اللاعب على النظر، ثم لا تعود — فالتعليم مرّة
/// واحدة والباقي علامات <see cref="DangerZone"/> في العالم.
///
/// ضع الكائن مع كولايدر (Is Trigger) في الممر قبل البركان الأول، وأعطه الصورة.
/// أو نادِ <see cref="Show"/> من أي حدث.
/// </summary>
[DisallowMultipleComponent]
public class WarningCard : MonoBehaviour
{
    [Header("الصورة")]
    [Tooltip("صورة التحذير — PNG بخلفية شفافة")]
    [SerializeField] private Sprite card;
    [Tooltip("أو: اسم الصورة داخل Osama/Resources إن تُركت الخانة فارغة")]
    [SerializeField] private string cardFromResources = "";
    [Tooltip("أكبر عرض تأخذه من الشاشة")]
    [Range(0.2f, 1f)] [SerializeField] private float widthOfScreen = 0.45f;
    [Tooltip("تعتيم خلف اللوحة ليبرز محتواها — صفر يلغيه")]
    [Range(0f, 1f)] [SerializeField] private float backdrop = 0.6f;
    [Tooltip("يغبّش المشهد خلف اللوحة. لقطةٌ واحدة تُصغَّر ثم تُعرض مكبّرة — واللعبة " +
             "متوقّفة فالمشهد ساكن ولقطة واحدة تكفي، بلا أي كلفة كل إطار")]
    [SerializeField] private bool blurBackground = true;
    [Tooltip("قوة التغبيش: كم مرّة تُصغَّر اللقطة. أكبر = أغبش")]
    [Range(2, 24)] [SerializeField] private int blurAmount = 10;

    [Header("متى تظهر")]
    [Tooltip("تظهر عند اقتراب اللاعب من أيٍّ من هذي الكائنات — بديل عن الكولايدر. " +
             "تعمل من أي اتجاه، فلا تحتاج معرفة الطريق الذي يدخل منه اللاعب")]
    [SerializeField] private Transform[] showNear;
    [Tooltip("مسافة الاقتراب (متر)")]
    [SerializeField] private float nearDistance = 14f;
    [Tooltip("تظهر عند دخول اللاعب كولايدر هذا الكائن")]
    [SerializeField] private bool showOnTrigger = true;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("مرّة واحدة في الجلسة — التحذير للتعليم لا للتكرار")]
    [SerializeField] private bool onlyOnce = true;
    [Tooltip("تأخير بسيط قبل الظهور، ليستقرّ اللاعب مكانه")]
    [SerializeField] private float delay = 0.15f;

    [Header("التجاوز")]
    [Tooltip("توقف اللعب حتى يتجاوزها اللاعب")]
    [SerializeField] private bool pauseGame = true;
    [Tooltip("أي زر أو نقرة تتجاوزها")]
    [SerializeField] private bool anyKeySkips = true;
    [Tooltip("تختفي وحدها بعد هذي الثواني — صفر = تنتظر اللاعب")]
    [SerializeField] private float autoHideAfter = 0f;
    [Tooltip("أقل مدة تبقى فيها قبل قبول التجاوز، فلا تُتجاوز بضغطة كانت جارية")]
    [SerializeField] private float minShowTime = 0.4f;
    [Tooltip("سطر صغير تحتها — فرّغه لإخفائه. لاتيني لأن الخط المدمج بلا عربية")]
    [SerializeField] private string skipHint = "PRESS ANY KEY";

    [Header("الظهور")]
    [SerializeField] private float fadeIn = 0.25f;
    [SerializeField] private float fadeOut = 0.2f;

    [Header("أحداث")]
    public UnityEvent onShown;
    public UnityEvent onDismissed;

    private Canvas canvas;
    private CanvasGroup group;
    private Image backdropImage;
    private Image cardImage;
    private Text hint;
    private Transform player;
    private Texture2D blurred;
    private Sprite blurSprite;
    private bool shown;
    private bool busy;

    private void Awake()
    {
        if (card == null && !string.IsNullOrWhiteSpace(cardFromResources))
            card = Resources.Load<Sprite>(cardFromResources.Trim());

        if (card == null)
        {
            Debug.LogWarning("[WarningCard] ما فيه صورة — اللوحة معطّلة.", this);
            enabled = false;
            return;
        }

        Build();
    }

    private void Update()
    {
        if (showNear == null || showNear.Length == 0) return;
        if (busy || (onlyOnce && shown)) return;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return;
            player = go.transform;
        }

        foreach (Transform target in showNear)
        {
            if (target == null) continue;
            if (Vector3.Distance(player.position, target.position) > nearDistance) continue;

            Show();
            return;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!showOnTrigger || !other.CompareTag(playerTag)) return;
        Show();
    }

    /// <summary>يعرض اللوحة — اربطه بأي حدث إن لم ترد كولايدر.</summary>
    public void Show()
    {
        if (busy || (onlyOnce && shown)) return;
        shown = true;
        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        busy = true;

        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        // قبل إظهار الكانفس: وإلا صوّرنا اللوحة نفسها داخل خلفيتها
        if (blurBackground) yield return CaptureBlur();

        canvas.enabled = true;
        group.blocksRaycasts = true;
        Layout();
        onShown?.Invoke();

        // الوقت الحقيقي في كل شيء: اللعبة متوقفة، و Time.deltaTime صفر حينها
        float previousScale = Time.timeScale;
        if (pauseGame) Time.timeScale = 0f;

        yield return Fade(0f, 1f, fadeIn);

        float open = 0f;
        while (true)
        {
            open += Time.unscaledDeltaTime;
            Layout();

            if (open >= minShowTime)
            {
                if (anyKeySkips && Pressed()) break;
                if (autoHideAfter > 0f && open >= autoHideAfter) break;
            }

            yield return null;
        }

        yield return Fade(1f, 0f, fadeOut);

        if (pauseGame) Time.timeScale = previousScale;

        group.blocksRaycasts = false;
        canvas.enabled = false;
        ReleaseBlur();
        busy = false;
        onDismissed?.Invoke();
    }

    /// <summary>
    /// يلتقط الشاشة مرّة ويصغّرها، ثم تُعرض مكبّرة فتبدو مغبّشة — الترشيح الخطّي
    /// للبطاقة يقوم بالتنعيم. واللعبة متوقّفة فالمشهد لا يتغيّر، فلقطة واحدة تكفي
    /// ولا نحتاج تغبيشًا حيًّا كل إطار.
    /// </summary>
    private IEnumerator CaptureBlur()
    {
        yield return new WaitForEndOfFrame();   // القراءة قبل نهاية الإطار تعطي شاشة نصف مرسومة

        Texture2D shot = null;
        try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
        catch (System.Exception e) { Debug.LogWarning($"[WarningCard] ما قدرت ألتقط الخلفية: {e.Message}", this); }

        if (shot == null) yield break;

        blurred = Shrink(shot, Mathf.Max(2, blurAmount));
        Destroy(shot);

        if (blurred == null) yield break;

        blurSprite = Sprite.Create(blurred, new Rect(0f, 0f, blurred.width, blurred.height),
                                   new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

        backdropImage.sprite = blurSprite;
        backdropImage.type = Image.Type.Simple;
        float dim = 1f - backdrop * 0.85f;
        backdropImage.color = new Color(dim, dim, dim, 1f);
    }

    private static Texture2D Shrink(Texture2D source, int divisor)
    {
        int w = Mathf.Max(1, source.width / divisor);
        int h = Mathf.Max(1, source.height / divisor);

        source.filterMode = FilterMode.Bilinear;
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0);
        rt.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        var small = new Texture2D(w, h, TextureFormat.RGB24, false);
        small.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
        small.Apply();
        small.filterMode = FilterMode.Bilinear;   // التكبير الخطّي هو التغبيش نفسه
        small.wrapMode = TextureWrapMode.Clamp;

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return small;
    }

    private void ReleaseBlur()
    {
        if (backdropImage != null)
        {
            backdropImage.sprite = null;
            backdropImage.color = new Color(0f, 0f, 0f, backdrop);
        }

        if (blurSprite != null) { Destroy(blurSprite); blurSprite = null; }
        if (blurred != null) { Destroy(blurred); blurred = null; }
    }

    private static bool Pressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        group.alpha = to;
    }

    // ───────────────────────────── الواجهة ─────────────────────────────

    /// <summary>كانفس يُبنى بالكود، فلا بريفاب ولا ربط في السين.</summary>
    private void Build()
    {
        var root = new GameObject("WarningCard_UI", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4500;   // تحت شاشة التحميل (5000) وفوق واجهات السين

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();
        group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        backdropImage = NewImage(root.transform, "Backdrop", new Color(0f, 0f, 0f, backdrop));
        Stretch(backdropImage.rectTransform);

        cardImage = NewImage(root.transform, "Card", Color.white);
        cardImage.sprite = card;
        cardImage.preserveAspect = true;
        cardImage.rectTransform.anchorMin = cardImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        cardImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        BuildHint(root.transform);
        canvas.enabled = false;
    }

    private void BuildHint(Transform parent)
    {
        if (string.IsNullOrWhiteSpace(skipHint)) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        if (font == null) return;

        var go = new GameObject("Hint", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        hint = go.AddComponent<Text>();
        hint.font = font;
        hint.fontSize = 24;
        hint.alignment = TextAnchor.LowerCenter;
        hint.color = new Color(1f, 1f, 1f, 0.7f);
        hint.raycastTarget = false;
        hint.horizontalOverflow = HorizontalWrapMode.Overflow;
        hint.verticalOverflow = VerticalWrapMode.Overflow;
        hint.text = string.Join(" ", skipHint.ToCharArray());

        RectTransform rect = hint.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 70f);
        rect.sizeDelta = new Vector2(1400f, 44f);
    }

    /// <summary>
    /// مقاس اللوحة من مقاس الكانفس لا من الشاشة: الكانفس يتدرّج مع الدقة، فالقياس
    /// منه يعطي نفس النسبة على كل شاشة.
    /// </summary>
    private void Layout()
    {
        if (cardImage == null || card == null) return;

        var canvasRect = (RectTransform)canvas.transform;
        float width = canvasRect.rect.width * widthOfScreen;
        float ratio = card.rect.height / Mathf.Max(1f, card.rect.width);

        float height = width * ratio;
        float maxHeight = canvasRect.rect.height * 0.8f;
        if (height > maxHeight)
        {
            height = maxHeight;
            width = height / Mathf.Max(0.0001f, ratio);
        }

        cardImage.rectTransform.sizeDelta = new Vector2(width, height);

        if (backdropImage != null && backdropImage.sprite == null)
            backdropImage.color = new Color(0f, 0f, 0f, backdrop);
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    [ContextMenu("تجربة: اعرض اللوحة الآن")]
    private void DebugShow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[WarningCard] جرّبها أثناء التشغيل.", this);
            return;
        }

        shown = false;
        Show();
    }

    private void OnValidate()
    {
        delay = Mathf.Max(0f, delay);
        minShowTime = Mathf.Max(0f, minShowTime);
        nearDistance = Mathf.Max(0.5f, nearDistance);
        autoHideAfter = Mathf.Max(0f, autoHideAfter);
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.4f);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }

        if (showNear == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        foreach (Transform target in showNear)
            if (target != null) Gizmos.DrawWireSphere(target.position, nearDistance);
    }
}
