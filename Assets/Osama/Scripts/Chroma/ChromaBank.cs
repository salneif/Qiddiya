using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <b>قطرات اللون</b> — نقاط اللاعب. عدّادان:
///
/// <list type="bullet">
/// <item><see cref="Run"/> — ما جمعه في هذه اللعبة. يُصفَّر مع لعبةٍ جديدة، بنفس لحظة
/// تصفير <see cref="GameProgress"/>، فيمشي مع الأعلام: من يكمل لعبته يكمل عدّه.</item>
/// <item><see cref="Lifetime"/> — كل ما جمعه عبر كل اللعبات. <b>لا يُصفَّر أبدًا</b>،
/// وهو ما يفتح الأزياء.</item>
/// </list>
///
/// والقطرات الموضوعة في المراحل لها معرّفات: ما جُمع منها في هذه اللعبة لا يعود إن
/// رجع اللاعب للمرحلة (<see cref="IsCollected"/>)، ويعود كلّه مع لعبةٍ جديدة.
///
/// <b>الإجمالي وحده يُحفظ</b> في <c>PlayerPrefs</c>. عدّ اللعبة والقطرات المجموعة في
/// الذاكرة فقط: من يضغط Play في مرحلةٍ ليجرّبها يجد قطراتها كلها، ولا تختفي قطرةٌ لأنها
/// جُمعت في تجربةٍ سابقة.
/// </summary>
public static class ChromaBank
{
    private const string LifetimeKey = "Chroma.Lifetime";

    /// <summary>قطرات هذه اللعبة.</summary>
    public static int Run { get { Load(); return run; } }

    /// <summary>كل القطرات عبر كل اللعبات — تفتح الأزياء.</summary>
    public static int Lifetime { get { Load(); return lifetime; } }

    /// <summary>
    /// عند كسب قطرات: الكمية، ومن أين جاءت في العالم (لرقمٍ يطير منها أو مؤثّر).
    /// </summary>
    public static event Action<int, Vector3> Gained;

    /// <summary>عند بداية لعبةٍ جديدة — صُفّر <see cref="Run"/> ورجعت القطرات الموضوعة كلها.</summary>
    public static event Action RunReset;

    private static int run, lifetime;
    private static HashSet<string> collected;
    private static bool loaded, dirty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Gained = null;
        RunReset = null;
        collected = null;
        loaded = dirty = false;
        run = lifetime = 0;
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        lifetime = Mathf.Max(0, PlayerPrefs.GetInt(LifetimeKey, 0));
        run = 0;
        collected = new HashSet<string>();
    }

    /// <summary>يضيف قطرات ويُعلن عنها. الكمية السالبة أو الصفر تُتجاهل.</summary>
    public static void Add(int amount, Vector3 at)
    {
        if (amount <= 0) return;
        Load();

        run += amount;
        lifetime += amount;
        Store();

        Gained?.Invoke(amount, at);
    }

    /// <summary>هل جُمعت هذه القطرة الموضوعة في هذه اللعبة؟</summary>
    public static bool IsCollected(string id)
    {
        Load();
        return !string.IsNullOrEmpty(id) && collected.Contains(id);
    }

    /// <summary>يسجّل قطرةً موضوعة كمجموعة، فلا تظهر ثانيةً في هذه اللعبة.</summary>
    public static void MarkCollected(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        Load();
        collected.Add(id);
    }

    /// <summary>كم قطرةً موضوعة جُمعت ومعرّفها يبدأ بهذه البادئة (مثل اسم السين).</summary>
    public static int CollectedWithPrefix(string prefix)
    {
        Load();
        int n = 0;
        foreach (string id in collected)
            if (id.StartsWith(prefix, StringComparison.Ordinal)) n++;
        return n;
    }

    /// <summary>معرّفات القطرات المجموعة في هذه اللعبة — يحفظها <see cref="ChromaSave"/>.</summary>
    internal static string[] CollectedIds()
    {
        Load();
        var ids = new string[collected.Count];
        collected.CopyTo(ids);
        return ids;
    }

    /// <summary>«Continue»: يعيد عدّ اللعبة والقطرات المجموعة كما حُفظت. الإجمالي لا يُمسّ.</summary>
    internal static void Restore(int runCount, IEnumerable<string> ids)
    {
        Load();
        run = Mathf.Max(0, runCount);
        collected.Clear();
        if (ids != null)
            foreach (string id in ids)
                if (!string.IsNullOrEmpty(id)) collected.Add(id);
    }

    /// <summary>
    /// لعبة جديدة: يصفّر عدّ هذه اللعبة ويعيد القطرات الموضوعة. الإجمالي لا يُمسّ.
    /// يناديه <see cref="ChromaEvents"/> تلقائيًا مع تصفير <see cref="GameProgress"/>.
    /// </summary>
    public static void ResetRun()
    {
        Load();
        run = 0;
        collected.Clear();
        Store();
        Flush();
        RunReset?.Invoke();
    }

    /// <summary>يكتب ما تغيّر إلى القرص. يُنادى عند تغيّر السين وعند الخروج — لا عند كل قطرة.</summary>
    public static void Flush()
    {
        if (!dirty) return;
        dirty = false;
        PlayerPrefs.Save();
    }

    private static void Store()
    {
        PlayerPrefs.SetInt(LifetimeKey, lifetime);
        dirty = true;
    }
}
