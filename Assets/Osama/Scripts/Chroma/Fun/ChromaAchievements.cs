using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>أوسمة الإنجاز</b> — اثنا عشر، محفوظة للأبد في <c>PlayerPrefs</c>. كلٌّ يُفتح بلافتةٍ أعلى
/// الشاشة، وتُعرض كلها في صفحةٍ داخل الخزانة (<see cref="ChromaAchievementsPage"/>).
///
/// تسمع <see cref="ChromaFunEvents"/> وحدها (ومعها موت اللاعب): لا تعرف من أطلق الحدث.
/// </summary>
[DisallowMultipleComponent]
public class ChromaAchievements : MonoBehaviour
{
    public sealed class Def
    {
        public string id, name, detail, letter;
    }

    public static readonly Def[] All =
    {
        new Def { id = "steam",       name = "STEAM COLLECTOR",    detail = "Every face in Steam Town",      letter = "S" },
        new Def { id = "twilight",    name = "TWILIGHT COLLECTOR", detail = "Every face in Twilight",        letter = "T" },
        new Def { id = "circus",      name = "CIRCUS COLLECTOR",   detail = "Every face in the Circus",      letter = "C" },
        new Def { id = "hub",         name = "HUB COLLECTOR",      detail = "Every face in the Hub",         letter = "H" },
        new Def { id = "golden",      name = "GOLDEN TOUCH",       detail = "Every golden face in the game", letter = "G" },
        new Def { id = "runaway",     name = "CAUGHT YOU!",        detail = "Catch a runaway face",          letter = "R" },
        new Def { id = "untouchable", name = "UNTOUCHABLE",        detail = "Finish a level without dying",  letter = "U" },
        new Def { id = "combo",       name = "COMBO ARTIST",       detail = "Collect 10 faces in a row",     letter = "X" },
        new Def { id = "gold",        name = "GOLD RUN",           detail = "Earn a gold time medal",        letter = "1" },
        new Def { id = "wave",        name = "HELLO THERE",        detail = "Wave at the camera",            letter = "W" },
        new Def { id = "clap",        name = "ENCORE!",            detail = "Clap for yourself",             letter = "E" },
        new Def { id = "photo",       name = "SAY CHEESE",         detail = "Take a photo in photo mode",    letter = "P" },
    };

    private static readonly Dictionary<string, string> SceneIds = new Dictionary<string, string>
    {
        { "Steam_Final", "steam" }, { "AliLvl2_SultanVersion", "twilight" }, { "Main_Circus", "circus" }, { "Hub", "hub" },
    };

    private const string Key = "Chroma.Ach.";
    private const string GoldKnown = "Chroma.GoldKnown", GoldGot = "Chroma.GoldGot", Planned = "Chroma.PlannedScenes";
    private const float UntouchableMinSeconds = 20f;

    public static bool Has(string id) => PlayerPrefs.GetInt(Key + id, 0) == 1;

    public static int Unlocked
    {
        get
        {
            int n = 0;
            foreach (Def d in All) if (Has(d.id)) n++;
            return n;
        }
    }

    private static ChromaAchievements instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaAchievements") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaAchievements>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private int deaths;
    private float enteredAt;

    private void OnEnable()
    {
        ChromaFunEvents.DropCollected += OnDrop;
        ChromaFunEvents.DropsPlanned += OnPlanned;
        ChromaFunEvents.RunawayCaught += OnRunaway;
        ChromaFunEvents.EmotePerformed += OnEmote;
        ChromaFunEvents.PhotoTaken += OnPhoto;
        ChromaFunEvents.MedalEarned += OnMedal;
        ChromaFunEvents.LevelLeft += OnLevelLeft;
        ChromaEvents.PlayerDied += OnDied;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ChromaFunEvents.DropCollected -= OnDrop;
        ChromaFunEvents.DropsPlanned -= OnPlanned;
        ChromaFunEvents.RunawayCaught -= OnRunaway;
        ChromaFunEvents.EmotePerformed -= OnEmote;
        ChromaFunEvents.PhotoTaken -= OnPhoto;
        ChromaFunEvents.MedalEarned -= OnMedal;
        ChromaFunEvents.LevelLeft -= OnLevelLeft;
        ChromaEvents.PlayerDied -= OnDied;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        deaths = 0;
        enteredAt = Time.time;
    }

    private void OnDied(Vector3 at) => deaths++;

    private void OnPlanned(string scene, int total, string[] goldenIds)
    {
        AddAll(Planned, new[] { scene });
        AddAll(GoldKnown, goldenIds);
    }

    private void OnDrop(string id, bool golden, bool runaway, Vector3 at)
    {
        if (ChromaDropField.Combo >= 10) Unlock("combo");

        string scene = SceneManager.GetActiveScene().name;
        if (id != null && ChromaDropField.PlacedTotal > 0 && ChromaDropField.PlacedCollected >= ChromaDropField.PlacedTotal &&
            SceneIds.TryGetValue(scene, out string sceneAch))
            Unlock(sceneAch);

        if (golden && id != null)
        {
            AddAll(GoldGot, new[] { id });
            // كل الذهبية: عُرفت ذهبيّات المراحل الأربع كلها، وكلها جُمعت
            HashSet<string> planned = Read(Planned), known = Read(GoldKnown), got = Read(GoldGot);
            bool allScenes = true;
            foreach (string s in SceneIds.Keys) allScenes &= planned.Contains(s);
            if (allScenes && known.Count > 0 && got.IsSupersetOf(known)) Unlock("golden");
        }
    }

    private void OnRunaway(Vector3 at) => Unlock("runaway");
    private void OnEmote(string emote) => Unlock(emote == "wave" ? "wave" : "clap");
    private void OnPhoto(string path) => Unlock("photo");

    private void OnMedal(string scene, int medal, float seconds, bool best)
    {
        if (medal == 3) Unlock("gold");
    }

    private void OnLevelLeft(string from, string to)
    {
        bool level = from == "Steam_Final" || from == "AliLvl2_SultanVersion" || from == "Main_Circus";
        if (level && deaths == 0 && Time.time - enteredAt >= UntouchableMinSeconds) Unlock("untouchable");
    }

    private static void Unlock(string id)
    {
        if (Has(id)) return;
        PlayerPrefs.SetInt(Key + id, 1);
        PlayerPrefs.Save();

        foreach (Def d in All)
        {
            if (d.id != id) continue;
            ChromaFunEvents.Announce(new ChromaFunEvents.Banner
            {
                header = "ACHIEVEMENT  " + Unlocked + " / " + All.Length,
                title = d.name,
                line = d.detail,
                letter = d.letter,
                accent = ChromaStyle.Get().gold,
                sfx = "Skin_Unlock",
            });
            break;
        }
    }

    private static HashSet<string> Read(string key)
    {
        var set = new HashSet<string>();
        foreach (string s in PlayerPrefs.GetString(key, "").Split('|'))
            if (!string.IsNullOrEmpty(s)) set.Add(s);
        return set;
    }

    private static void AddAll(string key, IEnumerable<string> items)
    {
        HashSet<string> set = Read(key);
        bool changed = false;
        foreach (string s in items) if (!string.IsNullOrEmpty(s)) changed |= set.Add(s);
        if (!changed) return;
        PlayerPrefs.SetString(key, string.Join("|", set));
    }
}
