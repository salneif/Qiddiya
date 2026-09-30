using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// عقد إضافات المتعة (التلويح، الوجه الهارب، الميداليات، التصوير، الأوسمة) في مكانٍ واحد.
///
/// كلّ ميزةٍ تُطلق أحداثها هنا ولا تعرف من يسمعها، والأوسمة تسمع هنا ولا تعرف من أطلقها —
/// فتُبنى كلها معًا ويُحذف أيّها دون أن ينكسر غيره. ومستمعٌ معطوب لا يُسكت البقيّة.
///
/// <see cref="Announce"/> لافتةٌ عامّة (وسام، ميدالية، صورة حُفظت): تعرضها لافتة الأزياء
/// بعد تعميمها، وما يُعلَن قبل أن تستمع ينتظرها في <see cref="TakeBacklog"/>.
/// </summary>
public static class ChromaFunEvents
{
    /// <summary>لافتةٌ تُعرض أعلى الشاشة.</summary>
    public struct Banner
    {
        public string header;   // سطرٌ صغير فوق: "ACHIEVEMENT"، "GOLD MEDAL"...
        public string title;    // الاسم الكبير
        public string line;     // سطر التفاصيل تحته (قد يكون فارغًا)
        public string letter;   // حرف الوسام في دائرته
        public Color accent;    // لون الوسام
        public string sfx;      // صوتٌ من Resources/Chroma/Sfx (فارغ = بلا صوت خاص)
    }

    /// <summary>جُمعت قطرة: معرّفها (null لقطرات الدفعات)، أذهبيّة، أهي الوجه الهارب، وموضعها.</summary>
    public static event Action<string, bool, bool, Vector3> DropCollected;

    /// <summary>خُطّطت قطرات السين: اسمه، عدد الموضوعة كلها، ومعرّفات الذهبية منها.</summary>
    public static event Action<string, int, string[]> DropsPlanned;

    /// <summary>أُمسك الوجه الهارب بعد هروبه.</summary>
    public static event Action<Vector3> RunawayCaught;

    /// <summary>أدّى حركة: "wave" أو "clap".</summary>
    public static event Action<string> EmotePerformed;

    /// <summary>حُفظت صورة من وضع التصوير (مسارها).</summary>
    public static event Action<string> PhotoTaken;

    /// <summary>ميدالية مرحلة: السين، الميدالية (0 لا شيء، 1 برونز، 2 فضّة، 3 ذهب)، الزمن بالثواني، أهو أفضل وقت.</summary>
    public static event Action<string, int, float, bool> MedalEarned;

    /// <summary>غادر مرحلةً عبر بوابتها فعلًا (بعد كل الشروط): من، إلى.</summary>
    public static event Action<string, string> LevelLeft;

    /// <summary>خطوة تعليمٍ أُتمّت: رقمها، وأهي الأخيرة.</summary>
    public static event Action<int, bool> TutorialStep;

    /// <summary>لافتةٌ مطلوبة.</summary>
    public static event Action<Banner> Announced;

    private static readonly Queue<Banner> backlog = new Queue<Banner>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DropCollected = null;
        DropsPlanned = null;
        RunawayCaught = null;
        EmotePerformed = null;
        PhotoTaken = null;
        MedalEarned = null;
        LevelLeft = null;
        TutorialStep = null;
        Announced = null;
        backlog.Clear();
    }

    public static void RaiseDropCollected(string id, bool golden, bool runaway, Vector3 at)
    {
        if (DropCollected == null) return;
        foreach (Action<string, bool, bool, Vector3> l in DropCollected.GetInvocationList())
            Safe(() => l(id, golden, runaway, at));
    }

    public static void RaiseDropsPlanned(string scene, int total, string[] goldenIds)
    {
        if (DropsPlanned == null) return;
        foreach (Action<string, int, string[]> l in DropsPlanned.GetInvocationList())
            Safe(() => l(scene, total, goldenIds));
    }

    public static void RaiseRunawayCaught(Vector3 at)
    {
        if (RunawayCaught == null) return;
        foreach (Action<Vector3> l in RunawayCaught.GetInvocationList()) Safe(() => l(at));
    }

    public static void RaiseEmote(string emote)
    {
        if (EmotePerformed == null) return;
        foreach (Action<string> l in EmotePerformed.GetInvocationList()) Safe(() => l(emote));
    }

    public static void RaisePhotoTaken(string path)
    {
        if (PhotoTaken == null) return;
        foreach (Action<string> l in PhotoTaken.GetInvocationList()) Safe(() => l(path));
    }

    public static void RaiseMedal(string scene, int medal, float seconds, bool best)
    {
        if (MedalEarned == null) return;
        foreach (Action<string, int, float, bool> l in MedalEarned.GetInvocationList())
            Safe(() => l(scene, medal, seconds, best));
    }

    public static void RaiseLevelLeft(string from, string to)
    {
        if (LevelLeft == null) return;
        foreach (Action<string, string> l in LevelLeft.GetInvocationList()) Safe(() => l(from, to));
    }

    public static void RaiseTutorialStep(int step, bool last)
    {
        if (TutorialStep == null) return;
        foreach (Action<int, bool> l in TutorialStep.GetInvocationList()) Safe(() => l(step, last));
    }

    /// <summary>يطلب لافتة. إن لم يستمع أحدٌ بعد، تنتظر في الطابور.</summary>
    public static void Announce(Banner banner)
    {
        if (Announced == null) { backlog.Enqueue(banner); return; }
        foreach (Action<Banner> l in Announced.GetInvocationList()) Safe(() => l(banner));
    }

    /// <summary>لافتاتٌ طُلبت قبل أن يستمع أحد — يأخذها العارض لحظة اشتراكه.</summary>
    public static bool TakeBacklog(out Banner banner)
    {
        if (backlog.Count == 0) { banner = default; return false; }
        banner = backlog.Dequeue();
        return true;
    }

    private static void Safe(Action call)
    {
        try { call(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
