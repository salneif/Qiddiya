using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// عنصرُ واجهةٍ يتبدّل مع جهاز اللاعب: صورة للكيبورد وصورة ليد التحكّم، ونصّان
/// كذلك إن كان نصًّا.
///
/// في القوائم أزرارٌ تقول <c>L1</c> و<c>R1</c>، وهي أسماء يد التحكّم وحدها — ومن
/// يلعب بالكيبورد يبدّل التبويبات بـ<c>Q</c> و<c>E</c> ولا يعرف ذلك من الشاشة.
/// وفي العالم يتكفّل <c>PromptIcons</c> بتلميحاته، أمّا الواجهة فعناصرها موضوعةٌ
/// بيد مصمّمها ولا يصحّ أن نخمّن ما فيها — فهذا يُركَّب على العنصر ويُعطى الصورتين.
///
/// يسمع <c>InputScheme.Changed</c> فلا يسأل كل إطار، ويضبط نفسه عند التفعيل كذلك:
/// لوحةٌ تُفتح بعد أن بدّل اللاعب جهازه لن يصلها الحدث وهي مطفأة.
/// </summary>
[DisallowMultipleComponent]
public class UISchemeIcon : MonoBehaviour
{
    [Header("الصورة")]
    [Tooltip("الصورة التي تتبدّل. فارغ = يبحث عن Image على نفس الكائن")]
    [SerializeField] private Image image;
    [Tooltip("صورة الكيبورد")]
    [SerializeField] private Sprite keyboardSprite;
    [Tooltip("صورة يد التحكّم")]
    [SerializeField] private Sprite gamepadSprite;

    [Header("الصورة الخام")]
    [Tooltip("RawImage تتبدّل. فارغ = يبحث عن RawImage على نفس الكائن")]
    [SerializeField] private RawImage rawImage;
    [Tooltip("نسيج الكيبورد")]
    [SerializeField] private Texture keyboardTexture;
    [Tooltip("نسيج يد التحكّم")]
    [SerializeField] private Texture gamepadTexture;

    [Header("النصّ")]
    [Tooltip("نصّ يتبدّل معها. فارغ = يبحث عن Text على نفس الكائن")]
    [SerializeField] private Text label;
    [SerializeField] private string keyboardText = "";
    [SerializeField] private string gamepadText = "";

    [Header("الظهور")]
    [Tooltip("يُطفئ الكائن كلّه على الجهاز الآخر — لتلميحٍ لا معنى له إلا على أحدهما")]
    [SerializeField] private bool keyboardOnly;
    [SerializeField] private bool gamepadOnly;

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (rawImage == null) rawImage = GetComponent<RawImage>();
        if (label == null) label = GetComponent<Text>();
    }

    private void OnEnable()
    {
        InputScheme.Changed += Apply;
        Apply();
    }

    private void OnDisable() => InputScheme.Changed -= Apply;

    private void Apply()
    {
        bool pad = InputScheme.UsingGamepad;

        if (keyboardOnly || gamepadOnly)
        {
            bool show = pad ? gamepadOnly : keyboardOnly;

            // على الصورة لا على الكائن: إطفاء الكائن يوقف OnEnable فلا يصلنا خبرُ
            // العودة، فيبقى مخفيًّا وإن بدّل اللاعب جهازه
            if (image != null) image.enabled = show;
            if (rawImage != null) rawImage.enabled = show;
            if (label != null) label.enabled = show;
            if (image == null && rawImage == null && label == null) return;
        }

        Sprite sprite = pad ? gamepadSprite : keyboardSprite;
        if (image != null && sprite != null) image.sprite = sprite;

        // شاشة الإعدادات مرسومة بـRawImage لا Image، فالنسيج لا السبرايت
        Texture texture = pad ? gamepadTexture : keyboardTexture;
        if (rawImage != null && texture != null) rawImage.texture = texture;

        string text = pad ? gamepadText : keyboardText;
        if (label != null && !string.IsNullOrEmpty(text)) label.text = text;
    }

    /// <summary>للتجربة في المحرر: اضغطها وشاهد الشكل الآخر.</summary>
    [ContextMenu("تجربة: طبّق الآن")]
    private void ApplyNow() => Apply();
}
