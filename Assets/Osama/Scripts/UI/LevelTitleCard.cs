using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// اسم المرحلة حين يدخلها اللاعب: <b>STEAM TOWN</b>، <b>THE HUB</b>، <b>TWILIGHT</b>،
/// <b>THE CIRCUS</b> — وتحته جملةٌ قصيرة.
///
/// <b>كلامٌ وحده بلا خلفية</b> (طلب أسامة): حروفٌ بلون الورق وخلفها ظلّ حبرٍ مزاح، فتُقرأ
/// فوق أيّ مشهد. الاسم يُكتب حرفًا حرفًا بخطّ السيرك، ثم الجملة بين خطّين، ثم يذوب كل شيء
/// صاعدًا قليلًا ومتّسعًا. <b>أقلّ من خمس ثوانٍ</b>
/// (٠٫٤٥ ظهور + كتابة + ٢٫٢ بقاء + ٠٫٨ ذوبان) — عنوانٌ لا ستارة.
///
/// ينتظر حتى تختفي شاشة التحميل وتنتهي ستارة السين، ويختفي تحت قائمة الإيقاف ويكمل
/// بعدها، ويسكت في الكريديت. يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DisallowMultipleComponent]
public class LevelTitleCard : MonoBehaviour
{
    private struct Title
    {
        public string scene, name, line;
    }

    private static readonly Title[] Titles =
    {
        new Title { scene = "Steam_Final",           name = "STEAM TOWN", line = "Every gear remembers its colour" },
        new Title { scene = "Hub",                   name = "THE HUB",    line = "Three islands wait for their light" },
        new Title { scene = "AliLvl2_SultanVersion", name = "TWILIGHT",   line = "Where the light hides between the trees" },
        new Title { scene = "Main_Circus",           name = "THE CIRCUS", line = "The show never stopped. The colour did." },
    };

    /// <summary>فوق واجهات السين، تحت رأس نقطة الحفظ (3500) والكريديت والتحميل.</summary>
    private const int SortingOrder = 3400;

    private const float AfterLoad = 1.1f;      // ستارة السين تنفتح أوّلًا
    private const float FadeIn = 0.45f;
    private const float PerLetter = 0.07f;
    private const float LineIn = 0.4f;
    private const float Hold = 2.2f;
    private const float FadeOut = 0.8f;

    private const float CardWidth = 940f, CardHeight = 200f, CardTop = 250f;

    private static LevelTitleCard instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("LevelTitleCard") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<LevelTitleCard>();
        DontDestroyOnLoad(host);
        instance.Begin(SceneManager.GetActiveScene());   // السين الأوّل حُمّل قبل أن نشترك
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform card;
    private TextMeshProUGUI title, line, titleShade, lineShade;
    private RectTransform ruleLeft, ruleRight;
    private Coroutine running;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Begin(scene);
    }

    private void Begin(Scene scene)
    {
        if (running != null) StopCoroutine(running);
        running = null;
        if (canvas != null) canvas.enabled = false;

        foreach (Title t in Titles)
            if (t.scene == scene.name) { running = StartCoroutine(Show(t)); return; }
    }

    private IEnumerator Show(Title t)
    {
        while (LoadingOverlay.IsBusy) yield return null;
        yield return new WaitForSecondsRealtime(AfterLoad);
        if (ChromaEvents.Quiet || !Build()) yield break;

        title.text = titleShade.text = t.name;
        title.maxVisibleCharacters = titleShade.maxVisibleCharacters = 0;
        title.characterSpacing = titleShade.characterSpacing = 0f;
        line.text = lineShade.text = t.line;
        line.alpha = lineShade.alpha = 0f;
        group.alpha = 0f;
        canvas.enabled = true;
        ChromaSfx.Play("Pulse_Whoosh", 0.25f, 1.15f);

        float typing = t.name.Length * PerLetter;
        float total = FadeIn + typing + LineIn + Hold + FadeOut;
        float time = 0f;

        while (time < total)
        {
            // تحت قائمة الإيقاف: لا يُرى ولا يمضي وقته
            if (Time.timeScale <= 0f) { group.alpha = 0f; yield return null; continue; }
            time += Time.unscaledDeltaTime;

            float appear = Mathf.Clamp01(time / FadeIn);
            float leave = Mathf.Clamp01((time - (total - FadeOut)) / FadeOut);
            group.alpha = Smooth(appear) * (1f - leave * leave);

            // يدخل من أسفل قليلًا بقفزةٍ مرنة، ويخرج صاعدًا متّسعًا
            float pop = ChromaWardrobeArt.BackOut(appear);
            card.anchoredPosition = new Vector2(0f, -CardTop - 18f * (1f - appear) + 16f * leave);
            float scale = Mathf.Lerp(0.92f, 1f, pop) + 0.03f * leave;
            card.localScale = new Vector3(scale, scale, 1f);

            float typed = Mathf.Clamp01((time - FadeIn * 0.5f) / Mathf.Max(0.01f, typing));
            title.maxVisibleCharacters = titleShade.maxVisibleCharacters = Mathf.CeilToInt(typed * t.name.Length);
            title.characterSpacing = titleShade.characterSpacing = 10f * leave;

            float lineK = Mathf.Clamp01((time - FadeIn * 0.5f - typing) / LineIn);
            line.alpha = Smooth(lineK);
            lineShade.alpha = Smooth(lineK) * ShadeAlpha;
            float rule = Smooth(lineK) * 120f;
            ruleLeft.sizeDelta = new Vector2(rule, 4f);
            ruleRight.sizeDelta = new Vector2(rule, 4f);

            yield return null;
        }

        canvas.enabled = false;
        running = null;
    }

    private static float Smooth(float k) => k * k * (3f - 2f * k);

    private const float Shadow = 5f, ShadeAlpha = 0.8f;

    private TextMeshProUGUI Text(string name, bool isTitle, float size, Color color, Vector2 at, float height)
    {
        TextMeshProUGUI text = ChromaWardrobeArt.NewText(card, name, isTitle, size, color, TextAlignmentOptions.Center);
        if (text.font == null && TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        ChromaWardrobeArt.Place(text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at,
                                new Vector2(CardWidth - (isTitle ? 60f : 320f), height));
        return text;
    }

    /// <summary>البطاقة تُبنى مرّةً وتعيش بين المشاهد. false إن تعذّر (بلا خطّ مثلًا).</summary>
    private bool Build()
    {
        if (canvas != null) return true;

        canvas = ChromaWardrobeArt.NewCanvas(transform, "Title", SortingOrder, out group);

        card = ChromaWardrobeArt.NewRect(canvas.transform, "Card");
        ChromaWardrobeArt.Place(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                new Vector2(0f, -CardTop), new Vector2(CardWidth, CardHeight));
        Color ink = ChromaWardrobeArt.Ink;
        Color paper = ChromaWardrobeArt.Paper;
        Color shade = new Color(ink.r, ink.g, ink.b, ShadeAlpha);

        // الظلّ أوّلًا فيُرسم خلف الحروف
        titleShade = Text("NameShade", true, 96f, shade, new Vector2(Shadow, 26f - Shadow), 120f);
        title = Text("Name", true, 96f, paper, new Vector2(0f, 26f), 120f);
        lineShade = Text("LineShade", false, 36f, shade, new Vector2(Shadow * 0.6f, -52f - Shadow * 0.6f), 48f);
        line = Text("Line", false, 36f, paper, new Vector2(0f, -52f), 48f);

        // خطّان على جانبي الجملة بلون الحروف
        ruleLeft = ChromaWardrobeArt.Place(ChromaWardrobeArt.NewImage(card, "RuleL", ChromaWardrobeArt.Round, paper),
                                           new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
                                           new Vector2(-(CardWidth - 320f) * 0.5f - 12f, -52f), new Vector2(0f, 4f));
        ruleRight = ChromaWardrobeArt.Place(ChromaWardrobeArt.NewImage(card, "RuleR", ChromaWardrobeArt.Round, paper),
                                            new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                                            new Vector2((CardWidth - 320f) * 0.5f + 12f, -52f), new Vector2(0f, 4f));

        if (title.font == null)
        {
            Debug.LogWarning("[LevelTitleCard] لا خطّ — بلا عنوان.", this);
            Destroy(canvas.gameObject);
            canvas = null;
            return false;
        }
        return true;
    }
}
