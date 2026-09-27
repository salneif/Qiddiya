using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// رأس الشخصية في ركن الشاشة، يلتفت يمينًا ويسارًا.
///
/// يظهر في مكانين: ركن شاشة التحميل، وعند كل نقطة حفظ. وهو صورة واحدة ساكنة لا
/// عدّة إطارات، فالالتفاتة <b>مصنوعة</b> لا مرسومة: ميلٌ في الزاوية، وضغطٌ خفيف
/// في العرض عند الأطراف كما يضيق الوجه حين يستدير، وزحفٌ يتبع الميل. الثلاثة معًا
/// تُقرأ التفاتةً، وأيّها وحده يُقرأ اهتزازًا.
///
/// صنفٌ عاديّ لا <c>MonoBehaviour</c>: مالكه يحرّكه في حلقته هو، فلا يحتاج كائنًا
/// مستقلًا ولا ترتيب تنفيذ.
/// </summary>
public class TurningHead
{
    private const string SpritePath = "Loading/Head_Ali";

    private const float TurnSeconds = 2.4f;     // دورة كاملة: يمين ثم يسار
    private const float TiltDegrees = 9f;
    private const float Squash = 0.11f;         // كم يضيق الوجه عند طرف الالتفاتة
    private const float Sway = 12f;             // وزحفه مع الميل
    private const float BobPixels = 4f;

    private static Sprite cached;
    private static bool looked;

    /// <summary>الصورة من <c>Resources</c>، مرّة واحدة لكل تشغيلة.</summary>
    public static Sprite Art
    {
        get
        {
            if (looked) return cached;

            looked = true;
            cached = Resources.Load<Sprite>(SpritePath);

            if (cached == null)
                Debug.LogWarning($"[TurningHead] ما لقيت \"{SpritePath}\" في Resources — " +
                                 "الرأس ما بيظهر.");

            return cached;
        }
    }

    /// <summary>يونيتي يُبقي الساكنات بين تشغيلتين في المحرر.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cached = null;
        looked = false;
    }

    public RectTransform Rect { get; }
    public Image Image { get; }

    private readonly Vector2 home;
    private float clock;

    private TurningHead(RectTransform rect, Image image, Vector2 home)
    {
        Rect = rect;
        Image = image;
        this.home = home;
    }

    /// <summary>
    /// يبنيه في ركن، والمقاس بالارتفاع وحده فتبقى نسبة الصورة كما هي مهما تغيّرت.
    /// </summary>
    /// <param name="corner">ركن الشاشة: (1,0) أسفل اليمين.</param>
    /// <param name="margin">بُعده عن حافتي الركن بالبكسل.</param>
    public static TurningHead Build(Transform parent, float height, Vector2 corner, Vector2 margin)
    {
        Sprite art = Art;
        if (art == null || parent == null) return null;

        var go = new GameObject("TurningHead", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = art;
        image.raycastTarget = false;
        image.preserveAspect = true;

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = corner;
        rect.pivot = corner;

        float ratio = art.rect.height > 0f ? art.rect.width / art.rect.height : 1f;
        rect.sizeDelta = new Vector2(height * ratio, height);

        // الإزاحة تدخل نحو مركز الشاشة أيًّا كان الركن
        var home = new Vector2(corner.x > 0.5f ? -margin.x : margin.x,
                               corner.y > 0.5f ? -margin.y : margin.y);
        rect.anchoredPosition = home;

        return new TurningHead(rect, image, home);
    }

    /// <summary>خطوة الالتفاتة. نادِها كل إطار بـ<c>deltaTime</c>.</summary>
    public void Turn(float deltaTime)
    {
        if (Rect == null) return;

        clock += deltaTime;

        float turn = Mathf.Sin(clock / TurnSeconds * Mathf.PI * 2f);
        float away = Mathf.Abs(turn);

        Rect.localRotation = Quaternion.Euler(0f, 0f, -turn * TiltDegrees);
        Rect.localScale = new Vector3(1f - Squash * away, 1f, 1f);

        // النطّة على ضِعف تردّد الالتفاتة: ترتفع في الطرفين معًا فتبدو حيّة
        float bob = Mathf.Sin(clock / TurnSeconds * Mathf.PI * 4f) * BobPixels;
        Rect.anchoredPosition = home + new Vector2(turn * Sway, bob);
    }

    /// <summary>يعيده إلى سكونه — لِمن يُظهره ويُخفيه.</summary>
    public void Rest()
    {
        clock = 0f;
        if (Rect == null) return;

        Rect.localRotation = Quaternion.identity;
        Rect.localScale = Vector3.one;
        Rect.anchoredPosition = home;
    }
}
