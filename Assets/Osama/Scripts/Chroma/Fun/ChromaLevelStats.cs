using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// <b>بطاقة نهاية المرحلة</b>: بعد الخروج من بوابة المرحلة، في الهب بعد اسمه — ورقةٌ تنزلق من
/// اليمين: الميدالية، والزمن وأفضل زمن، والوجوه المجموعة، والذهبية، والهارب، وكم مرّة مات.
/// تبقى سبع ثوانٍ وتذوب، وتختفي تحت الإيقاف والخزانة والتصوير وتعود بعدها.
///
/// العدّادات لكل زيارة مرحلة: تبدأ من الصفر مع كل تحميلٍ لها (ومع «Restart» كذلك).
/// </summary>
[DisallowMultipleComponent]
public class ChromaLevelStats : MonoBehaviour
{
    private const float ShowAfter = 5.2f;                 // بعد اسم المرحلة (٤٫٦ ث)
    private const float SlideIn = 0.45f, Hold = 7f, SlideOut = 0.4f;
    private const float CardW = 500f, CardH = 530f, Margin = 48f, RowStep = 44f;

    private struct Record
    {
        public string scene;
        public int medal, deaths, faces, total, golden, goldenTotal;
        public float seconds, best;
        public bool newBest, hadRunaway, runaway;
    }

    private static ChromaLevelStats instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaLevelStats") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaLevelStats>();
        DontDestroyOnLoad(host);
        instance.Begin(SceneManager.GetActiveScene());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // زيارة المرحلة الجارية
    private string visit;
    private int deaths, planned;
    private string[] goldenIds = new string[0];
    private bool runaway;

    // البطاقة المنتظرة
    private bool pending, showing;
    private Record record;
    private float wait, shownFor;

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform card;
    private TextMeshProUGUI header, title, medalText, bestNote, medalLetter;
    private Image medalFill;
    private TextMeshProUGUI[] values;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ChromaEvents.PlayerDied += OnDied;
        ChromaFunEvents.DropsPlanned += OnPlanned;
        ChromaFunEvents.RunawayCaught += OnRunaway;
        ChromaFunEvents.MedalEarned += OnMedal;
        ChromaBank.RunReset += OnRunReset;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ChromaEvents.PlayerDied -= OnDied;
        ChromaFunEvents.DropsPlanned -= OnPlanned;
        ChromaFunEvents.RunawayCaught -= OnRunaway;
        ChromaFunEvents.MedalEarned -= OnMedal;
        ChromaBank.RunReset -= OnRunReset;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Begin(scene);
    }

    private void Begin(Scene scene)
    {
        visit = LevelRestart.IsLevel(scene.name) ? scene.name : null;
        deaths = planned = 0;
        goldenIds = new string[0];
        runaway = false;
        wait = 0f;
        if (showing) { showing = false; pending = true; }   // لم تكتمل قبل الانتقال: تُعاد في الهب
        if (canvas != null) canvas.enabled = false;
    }

    private void OnRunReset()
    {
        pending = showing = false;
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDied(Vector3 at) { if (visit != null) deaths++; }
    private void OnRunaway(Vector3 at) { if (visit != null) runaway = true; }

    private void OnPlanned(string scene, int total, string[] golden)
    {
        if (scene != visit) return;
        planned = total;
        goldenIds = golden ?? new string[0];
    }

    private void OnMedal(string scene, int medal, float seconds, bool newBest)
    {
        if (scene != visit) return;
        int golden = 0;
        foreach (string id in goldenIds) if (ChromaBank.IsCollected(id)) golden++;

        record = new Record
        {
            scene = scene,
            medal = medal,
            seconds = seconds,
            best = ChromaMedals.Best(scene),
            newBest = newBest,
            deaths = deaths,
            faces = ChromaBank.CollectedWithPrefix(scene + ":"),
            total = planned,
            golden = golden,
            goldenTotal = goldenIds.Length,
            hadRunaway = planned >= 6,
            runaway = runaway,
        };
        pending = true;
        showing = false;
        wait = 0f;
    }

    private void Update()
    {
        if (!pending && !showing) return;
        if (visit != null) return;   // تُعرض خارج المراحل (الهب)

        bool hidden = ChromaEvents.Quiet || Time.timeScale <= 0f || ChromaWardrobe.IsOpen || ChromaPhotoMode.IsOpen;
        if (hidden)
        {
            if (canvas != null && canvas.enabled) canvas.enabled = false;
            return;
        }

        if (pending)
        {
            wait += Time.unscaledDeltaTime;
            if (wait < ShowAfter) return;
            Build();
            Fill();
            pending = false;
            showing = true;
            shownFor = 0f;
            ChromaSfx.Play(record.medal == 3 ? "Drop_Gold" : "Skin_Open", 0.6f);
        }

        if (!canvas.enabled) canvas.enabled = true;
        shownFor += Time.unscaledDeltaTime;
        Animate();
    }

    private void Animate()
    {
        float x;
        float a = 1f;
        if (shownFor < SlideIn)
        {
            float k = ChromaWardrobeArt.BackOut(Mathf.Clamp01(shownFor / SlideIn));
            x = Mathf.LerpUnclamped(CardW + Margin, -Margin, k);
            a = Mathf.Clamp01(shownFor / (SlideIn * 0.6f));
        }
        else if (shownFor < SlideIn + Hold) x = -Margin;
        else
        {
            float k = Mathf.Clamp01((shownFor - SlideIn - Hold) / SlideOut);
            x = Mathf.Lerp(-Margin, CardW * 0.35f, k * k);
            a = 1f - k;
            if (k >= 1f)
            {
                showing = false;
                canvas.enabled = false;
                return;
            }
        }
        card.anchoredPosition = new Vector2(x, 0f);
        group.alpha = a;
    }

    private void Fill()
    {
        Record r = record;
        Color ink = ChromaWardrobeArt.Ink;
        header.text = "LEVEL CLEAR";
        title.text = ChromaMedals.LevelName(r.scene);

        medalFill.color = ChromaMedals.MedalColors[Mathf.Clamp(r.medal, 0, 3)];
        medalLetter.text = r.medal > 0 ? ChromaMedals.MedalNames[r.medal].Substring(0, 1) : "-";
        medalText.text = r.medal > 0 ? ChromaMedals.MedalNames[r.medal] + " MEDAL" : "NO MEDAL";
        bestNote.text = r.newBest ? "NEW BEST!" : "";

        values[0].text = ChromaMedals.Clock(r.seconds);
        values[1].text = r.best > 0f ? ChromaMedals.Clock(r.best) : "-";
        values[2].text = r.total > 0 ? r.faces + " / " + r.total : r.faces.ToString();
        values[3].text = r.goldenTotal > 0 ? r.golden + " / " + r.goldenTotal : "-";
        values[4].text = !r.hadRunaway ? "-" : r.runaway ? "CAUGHT" : "ESCAPED";
        values[5].text = r.deaths.ToString();

        values[2].color = r.total > 0 && r.faces >= r.total ? ChromaStyle.Get().gold : ink;
        values[5].color = r.deaths == 0 ? ChromaStyle.Get().gold : ink;
    }

    private void Build()
    {
        if (canvas != null) return;
        canvas = ChromaWardrobeArt.NewCanvas(transform, "LevelStats", 0, out group);
        Color ink = ChromaWardrobeArt.Ink;
        Color soft = new Color(ink.r, ink.g, ink.b, 0.6f);

        card = ChromaWardrobeArt.NewRect(canvas.transform, "Card");
        ChromaWardrobeArt.Place(card, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Margin, 0f), new Vector2(CardW, CardH));
        ChromaWardrobeArt.Card(card, 3f);

        header = ChromaWardrobeArt.NewText(card, "Header", false, 26f, soft, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(CardW - 40f, 34f));
        title = ChromaWardrobeArt.NewText(card, "Title", true, 56f, ink, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(CardW - 40f, 70f));

        // الميدالية: قرصٌ بإطار حبر وحرفه، واسمها بجانبه
        Vector2 disc = new Vector2(76f, -170f);
        ChromaWardrobeArt.Place(ChromaWardrobeArt.NewImage(card, "Rim", ChromaWardrobeArt.Disc, ink),
                                new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), disc, new Vector2(94f, 94f));
        medalFill = ChromaWardrobeArt.NewImage(card, "Fill", ChromaWardrobeArt.Disc, Color.white);
        ChromaWardrobeArt.Place(medalFill, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), disc, new Vector2(80f, 80f));
        medalLetter = ChromaWardrobeArt.NewText(card, "Letter", true, 50f, Color.white, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(medalLetter, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), disc, new Vector2(80f, 80f));

        medalText = ChromaWardrobeArt.NewText(card, "Medal", true, 40f, ink, TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(medalText, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(140f, -158f), new Vector2(CardW - 170f, 50f));
        bestNote = ChromaWardrobeArt.NewText(card, "Best", false, 24f, ChromaStyle.Get().gold, TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(bestNote, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(142f, -194f), new Vector2(CardW - 170f, 30f));

        string[] labels = { "TIME", "BEST", "FACES", "GOLDEN", "RUNAWAY", "DEATHS" };
        values = new TextMeshProUGUI[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            float y = -250f - i * RowStep;
            var label = ChromaWardrobeArt.NewText(card, "Label" + i, false, 28f, soft, TextAlignmentOptions.Left);
            ChromaWardrobeArt.Place(label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(220f, 40f));
            label.text = labels[i];
            values[i] = ChromaWardrobeArt.NewText(card, "Value" + i, false, 30f, ink, TextAlignmentOptions.Right);
            ChromaWardrobeArt.Place(values[i], new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, y), new Vector2(260f, 40f));
        }
        canvas.enabled = false;
    }
}
