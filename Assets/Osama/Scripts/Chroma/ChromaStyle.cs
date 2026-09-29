using TMPro;
using UnityEngine;

/// <summary>
/// هويّة مؤثّرات اللون وواجهاتها في مكانٍ واحد: خطوط اللعبة وألوان القطرات والحبر.
///
/// الواجهات التي تُبنى بالكود (العدّاد، خزانة الأزياء) لا تجد خطوط اللعبة إلا بمرجع،
/// وخطّا عبير خارج <c>Resources</c>. فهذا الأصل في <c>Osama/Resources/Chroma</c> يحمل
/// المرجع، ويدخل البلد يقينًا.
///
/// <see cref="Get"/> لا يرجع null أبدًا: إن غاب الأصل رجعت نسخةٌ افتراضية بلا خطوط،
/// ومن يرسم نصًّا يرجع لخطّ TMP الافتراضي.
/// </summary>
[CreateAssetMenu(menuName = "Osama/Chroma Style", fileName = "ChromaStyle")]
public class ChromaStyle : ScriptableObject
{
    private const string ResourcePath = "Chroma/ChromaStyle";

    [Header("الخطوط")]
    [Tooltip("خطّ النصوص والأرقام — الكرايون")]
    public TMP_FontAsset bodyFont;
    [Tooltip("خطّ العناوين — السيرك")]
    public TMP_FontAsset titleFont;

    [Header("الألوان")]
    [Tooltip("الحبر: الإطارات والظلال والنصّ على الورق")]
    public Color ink = new Color(0.07f, 0.06f, 0.08f, 1f);
    [Tooltip("الورق: خلفيّات البطاقات")]
    public Color paper = new Color(0.97f, 0.95f, 0.9f, 1f);
    [Tooltip("ألوان القطرات بالترتيب — كل قطرةٍ لونٌ من هنا، فالمسار قوس قزح")]
    public Color[] palette =
    {
        new Color(1.00f, 0.30f, 0.43f),   // أحمر وردي
        new Color(1.00f, 0.62f, 0.11f),   // برتقالي
        new Color(1.00f, 0.84f, 0.04f),   // أصفر
        new Color(0.24f, 0.86f, 0.59f),   // أخضر
        new Color(0.18f, 0.77f, 0.95f),   // سماوي
        new Color(0.61f, 0.36f, 0.90f),   // بنفسجي
        new Color(0.95f, 0.36f, 0.71f),   // وردي
    };
    [Tooltip("لون القطرة الذهبية النادرة")]
    public Color gold = new Color(1f, 0.8f, 0.25f, 1f);

    private static ChromaStyle cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cached = null;

    /// <summary>الهويّة من Resources، أو افتراضيّة إن غابت. لا ترجع null.</summary>
    public static ChromaStyle Get()
    {
        if (cached != null) return cached;
        cached = Resources.Load<ChromaStyle>(ResourcePath);
        if (cached == null)
        {
            Debug.LogWarning($"[ChromaStyle] ما لقيت Osama/Resources/{ResourcePath} — بلا خطوط اللعبة.");
            cached = CreateInstance<ChromaStyle>();
        }
        return cached;
    }

    /// <summary>لون القطرة رقم <paramref name="index"/> — يدور على اللوحة.</summary>
    public Color Palette(int index)
    {
        if (palette == null || palette.Length == 0) return Color.white;
        int i = index % palette.Length;
        return palette[i < 0 ? i + palette.Length : i];
    }
}
