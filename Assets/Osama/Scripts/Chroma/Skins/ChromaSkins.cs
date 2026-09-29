using System;
using UnityEngine;

/// <summary>
/// زيٌّ واحد من أزياء الشخصية.
///
/// الصبغة <b>تُضرب</b> في لون نسيج الشخصية بقدر <see cref="Strength"/>، فتبقى خيوط
/// النسيج وظلاله تحتها: زيٌّ جديد لا دلو طلاء. و<see cref="Accent"/> لونه في الخزانة
/// واللافتة، و<see cref="Swatches"/> ألوانه على بطاقته.
/// </summary>
public sealed class ChromaSkin
{
    /// <summary>دورة قوس القزح للزيّ الملوّن (ث) — بطيئة: تُلاحظ ولا تُزعج.</summary>
    private const float RainbowPeriod = 9f;

    public string Name { get; }
    public string Tagline { get; }

    /// <summary>كم قطرةً (عبر كل اللعبات) تفتحه.</summary>
    public int Threshold { get; }

    public Color Tint { get; }
    public float Strength { get; }
    public Color Accent { get; }
    public Color[] Swatches { get; }

    /// <summary>ألوانه تدور على قوس القزح بدل لونٍ ثابت.</summary>
    public bool Rainbow { get; }

    public ChromaSkin(string name, string tagline, int threshold, Color tint, float strength,
                      Color accent, bool rainbow, params Color[] swatches)
    {
        Name = name;
        Tagline = tagline;
        Threshold = threshold;
        Tint = tint;
        Strength = strength;
        Accent = accent;
        Rainbow = rainbow;
        Swatches = swatches;
    }

    /// <summary>موضع اللحظة على دورة قوس القزح (٠..١) — واحدٌ للصبغة والمؤثّر والواجهة.</summary>
    public static float HueAt(float time) => Mathf.Repeat(time / RainbowPeriod, 1f);

    /// <summary>لون الصبغة في هذه اللحظة من دورته.</summary>
    public Color TintAt(float time) => Rainbow ? Color.HSVToRGB(HueAt(time), 0.55f, 1f) : Tint;

    /// <summary>لونه في الواجهة في هذه اللحظة — أشبع من الصبغة، فالورق فاتح.</summary>
    public Color AccentAt(float time) => Rainbow ? Color.HSVToRGB(HueAt(time), 0.72f, 0.95f) : Accent;
}

/// <summary>
/// <b>أزياء الشخصية</b>: ستّة، تُفتح بمجموع القطرات عبر كل اللعبات
/// (<see cref="ChromaBank.Lifetime"/>) — فلا يضيع زيٌّ فُتح إن بدأ اللاعب لعبةً جديدة.
///
/// <list type="bullet">
/// <item><see cref="Equipped"/> — الملبوس، محفوظ في <c>PlayerPrefs</c>. زيٌّ محفوظٌ وهو مقفل
/// (مسحٌ للتقدّم مثلًا) يُلبَس مكانه الأصليّ.</item>
/// <item><see cref="Preview"/> — ما تعرضه الخزانة الآن على اللاعب، ولو كان مقفلًا. لا يُحفظ.</item>
/// <item><see cref="Shown"/> — ما يُرى فعلًا: المعاينة إن وُجدت، وإلا الملبوس.</item>
/// </list>
///
/// ويركّب كائن الأزياء — اللابس واللافتة والخزانة — مرّة، بلا شيء في أي مشهد.
/// </summary>
public static class ChromaSkins
{
    private const string EquippedKey = "Chroma.Skin";

    private static readonly ChromaSkin[] all =
    {
        new ChromaSkin("CLASSIC", "The original look. Timeless.", 0,
            Color.white, 0f, new Color(0.3f, 0.3f, 0.34f), false,
            new Color(0.1f, 0.1f, 0.12f), new Color(0.5f, 0.5f, 0.52f), new Color(0.94f, 0.94f, 0.92f)),

        new ChromaSkin("EMBER", "Warm as a hearth, bright as a spark.", 40,
            new Color(1f, 0.56f, 0.34f), 0.55f, new Color(1f, 0.42f, 0.14f), false,
            new Color(1f, 0.86f, 0.36f), new Color(1f, 0.56f, 0.14f), new Color(0.93f, 0.24f, 0.1f),
            new Color(0.42f, 0.1f, 0.07f)),

        new ChromaSkin("BRASS", "Polished copper, puffing steam.", 120,
            new Color(0.96f, 0.72f, 0.44f), 0.55f, new Color(0.8f, 0.52f, 0.24f), false,
            new Color(0.95f, 0.8f, 0.45f), new Color(0.82f, 0.52f, 0.26f), new Color(0.55f, 0.33f, 0.17f),
            new Color(0.33f, 0.27f, 0.22f), new Color(0.92f, 0.91f, 0.86f)),

        new ChromaSkin("TWILIGHT", "Fireflies follow you home.", 220,
            new Color(0.62f, 0.52f, 1f), 0.5f, new Color(0.56f, 0.4f, 0.96f), false,
            new Color(0.16f, 0.1f, 0.34f), new Color(0.56f, 0.4f, 0.96f), new Color(0.24f, 0.74f, 0.8f),
            new Color(0.62f, 1f, 0.84f)),

        new ChromaSkin("CIRCUS", "Every jump is a celebration!", 330,
            new Color(1f, 0.5f, 0.55f), 0.5f, new Color(0.93f, 0.17f, 0.27f), false,
            new Color(0.93f, 0.17f, 0.27f), Color.white, new Color(1f, 0.8f, 0.2f),
            new Color(0.2f, 0.6f, 0.95f)),

        new ChromaSkin("PRISM", "Every colour, all at once.", 450,
            Color.white, 0.55f, Color.white, true,
            new Color(1f, 0.3f, 0.35f), new Color(1f, 0.65f, 0.15f), new Color(1f, 0.9f, 0.2f),
            new Color(0.3f, 0.88f, 0.5f), new Color(0.25f, 0.65f, 1f), new Color(0.65f, 0.4f, 0.95f)),
    };

    private static int stored = -1;     // المحفوظ، يُقرأ مرّة
    private static int forced = -1;     // مفتاح التجربة: ملبوسٌ ولو كان مقفلًا، لهذه الجلسة
    private static int preview = -1;
    private static GameObject host;

    /// <summary>تغيّر الملبوس أو المعاينة.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        stored = forced = preview = -1;
        Changed = null;
        host = null;
    }

    /// <summary>
    /// كائنٌ واحد للثلاثة: اللابس يُلبس، واللافتة تُعلن، والخزانة تُعرض. وبترتيبٍ ثابت
    /// لا يتوقّف على من يُركَّب قبل من.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (host != null) return;

        host = new GameObject("ChromaSkins") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<ChromaSkinWearer>();
        host.AddComponent<ChromaSkinToast>();
        host.AddComponent<ChromaWardrobe>();
    }

    public static int Count => all.Length;

    public static ChromaSkin Get(int index) => all[Mathf.Clamp(index, 0, all.Length - 1)];

    public static bool IsUnlocked(int index) =>
        index >= 0 && index < all.Length && ChromaBank.Lifetime >= all[index].Threshold;

    /// <summary>الزيّ الملبوس. المحفوظ إن كان مفتوحًا، وإلا الأصليّ.</summary>
    public static int Equipped
    {
        get
        {
            if (stored < 0) stored = Mathf.Clamp(PlayerPrefs.GetInt(EquippedKey, 0), 0, all.Length - 1);
            return stored == forced || IsUnlocked(stored) ? stored : 0;
        }
    }

    /// <summary>ما تعرضه الخزانة على اللاعب الآن؛ -1 = لا معاينة.</summary>
    public static int Preview
    {
        get => preview;
        set
        {
            int next = value < 0 ? -1 : Mathf.Clamp(value, 0, all.Length - 1);
            if (next == preview) return;
            preview = next;
            Changed?.Invoke();
        }
    }

    /// <summary>ما يُرى على اللاعب فعلًا.</summary>
    public static int Shown => preview >= 0 ? preview : Equipped;

    /// <summary>يلبس زيًّا مفتوحًا ويحفظه. المقفل يُرفض ويرجع false.</summary>
    public static bool Equip(int index)
    {
        if (!IsUnlocked(index)) return false;
        Store(index);
        return true;
    }

    /// <summary>
    /// للتجربة (F10): يلبس ولو كان مقفلًا. يُحفظ كالعادة، لكنه يبقى ملبوسًا في هذه
    /// الجلسة وحدها — في التالية يُعامَل كأيّ زيٍّ مقفل ويرجع الأصليّ.
    /// </summary>
    public static void EquipEvenIfLocked(int index)
    {
        forced = Mathf.Clamp(index, 0, all.Length - 1);
        Store(forced);
    }

    private static void Store(int index)
    {
        stored = Mathf.Clamp(index, 0, all.Length - 1);
        PlayerPrefs.SetInt(EquippedKey, stored);
        PlayerPrefs.Save();     // اختيارٌ من اللاعب، نادرٌ — يُكتب فورًا
        Changed?.Invoke();
    }
}
