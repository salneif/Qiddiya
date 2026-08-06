using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ذاكرة تقدّم اللعبة — الشيء الوحيد الذي يعيش بين السينات (DontDestroyOnLoad).
///
/// تحفظ ثلاثة أشياء فقط:
///  - أي علم يحمله اللاعب الآن (<see cref="CarriedFlag"/>)
///  - أي أعلام زُرعت في الهب (<see cref="IsPlanted"/>)
///  - آخر سين خرج منه (لتحديد نقطة ظهوره في الهب)
///
/// ⚠️ العلم نفسه <b>لا</b> ينتقل ككائن بين السينات. كل سين فيه نسخته من العلم،
/// و<see cref="FlagCarry"/> يقرأ هذي الذاكرة عند التحميل ويضع النسخة في مكانها
/// الصحيح (على رأس اللاعب / في قاعدتها مزروعة / مخفيّة). النتيجة على الشاشة
/// نفسها، لكن بلا مراجع تنكسر عند تغيير السين — راجع الخطأ رقم ٦ في الريدمي.
///
/// ما تحتاج تحطّه في أي سين: أول من يطلبه ينشئه تلقائيًا. حطّه يدويًا في مشهد
/// القائمة فقط إذا أردت ربط زر "لعبة جديدة" بـ<see cref="ResetProgress"/>.
/// </summary>
[DisallowMultipleComponent]
public class GameProgress : MonoBehaviour
{
    private const string KeyPlanted = "Qiddiya.Progress.Planted";
    private const string KeyCarried = "Qiddiya.Progress.Carried";
    private const string KeyLastScene = "Qiddiya.Progress.LastScene";

    private static GameProgress instance;

    /// <summary>الذاكرة الحيّة — تُنشأ تلقائيًا عند أول طلب.</summary>
    public static GameProgress Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindFirstObjectByType<GameProgress>();
            if (instance == null)
                new GameObject("GameProgress (تلقائي)").AddComponent<GameProgress>();

            return instance; // ضُبط داخل Awake لحظة AddComponent
        }
    }

    [Header("الحفظ")]
    [Tooltip("يحفظ التقدّم بين جلسات اللعب (PlayerPrefs). " +
             "اتركه مطفيًا أثناء التطوير وإلا بدأت كل تجربة والأعلام مزروعة أصلًا.")]
    [SerializeField] private bool saveBetweenSessions = false;

    [Header("عدد الأعلام")]
    [Tooltip("كم علمًا يجب زرعه لتنتهي اللعبة")]
    [SerializeField] private int totalFlags = 3;

    [Header("الحالة (للعرض — تتحدّث وقت اللعب)")]
    [Tooltip("العلم المحمول الآن")]
    [SerializeField] private FlagId carriedFlag = FlagId.None;
    [Tooltip("الأعلام المزروعة في الهب")]
    [SerializeField] private List<FlagId> plantedFlags = new();

    /// <summary>يُطلق عند أي تغيّر في التقدّم (حمل/زرع/إعادة ضبط).</summary>
    public static event Action Changed;

    /// <summary>العلم المحمول الآن (None = لا شيء).</summary>
    public FlagId CarriedFlag => carriedFlag;

    /// <summary>عدد الأعلام المزروعة.</summary>
    public int PlantedCount => plantedFlags.Count;

    /// <summary>هل زُرعت كل الأعلام؟ (شرط نهاية اللعبة)</summary>
    public bool AllPlanted => plantedFlags.Count >= totalFlags;

    /// <summary>اسم السين الذي خرج منه اللاعب آخر مرة (فارغ في أول تشغيل).</summary>
    public string LastScene { get; private set; } = string.Empty;

    public bool IsCarrying(FlagId id) => id != FlagId.None && carriedFlag == id;

    public bool IsPlanted(FlagId id) => id != FlagId.None && plantedFlags.Contains(id);

    /// <summary>
    /// يصفّر الحالات الساكنة عند بداية كل تشغيل — ضروري مع
    /// "Enter Play Mode without Domain Reload" وإلا بقي تقدّم الجلسة السابقة.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Changed = null;
    }

    private void Awake()
    {
        // نسخة ثانية (رجعنا للقائمة مثلًا) — الأصلية هي المرجع
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        transform.SetParent(null); // DontDestroyOnLoad لا تقبل إلا كائنًا جذرًا
        DontDestroyOnLoad(gameObject);

        if (saveBetweenSessions) Load();
    }

    /// <summary>يسجّل أن اللاعب رفع هذا العلم — ينادى من <see cref="FlagCarry"/>.</summary>
    public void CarryFlag(FlagId id)
    {
        if (id == FlagId.None || carriedFlag == id) return;
        carriedFlag = id;
        Commit();
    }

    /// <summary>يسجّل أن اللاعب لم يعد حاملًا شيئًا (وضعه أو مات وهو حامله).</summary>
    public void ClearCarried()
    {
        if (carriedFlag == FlagId.None) return;
        carriedFlag = FlagId.None;
        Commit();
    }

    /// <summary>يسجّل زرع العلم في قاعدته نهائيًا — ينادى من <see cref="FlagBase"/>.</summary>
    public void PlantFlag(FlagId id)
    {
        if (id == FlagId.None) return;

        if (!plantedFlags.Contains(id)) plantedFlags.Add(id);
        if (carriedFlag == id) carriedFlag = FlagId.None;

        Commit();
    }

    /// <summary>يسجّل السين الذي نغادره الآن — يحدد نقطة الظهور في الوجهة.</summary>
    public void SetLastScene(string sceneName)
    {
        LastScene = sceneName ?? string.Empty;
        if (saveBetweenSessions) PlayerPrefs.SetString(KeyLastScene, LastScene);
    }

    /// <summary>يمسح كل التقدّم — اربطه بزر "لعبة جديدة" في القائمة الرئيسية.</summary>
    [ContextMenu("امسح كل التقدّم")]
    public void ResetProgress()
    {
        carriedFlag = FlagId.None;
        plantedFlags.Clear();
        LastScene = string.Empty;

        PlayerPrefs.DeleteKey(KeyPlanted);
        PlayerPrefs.DeleteKey(KeyCarried);
        PlayerPrefs.DeleteKey(KeyLastScene);
        PlayerPrefs.Save();

        Changed?.Invoke();
    }

    private void Commit()
    {
        if (saveBetweenSessions) Save();
        Changed?.Invoke();
    }

    private void Save()
    {
        int mask = 0;
        foreach (var f in plantedFlags) mask |= 1 << (int)f;

        PlayerPrefs.SetInt(KeyPlanted, mask);
        PlayerPrefs.SetInt(KeyCarried, (int)carriedFlag);
        PlayerPrefs.SetString(KeyLastScene, LastScene);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        int mask = PlayerPrefs.GetInt(KeyPlanted, 0);
        plantedFlags.Clear();
        foreach (FlagId id in Enum.GetValues(typeof(FlagId)))
        {
            if (id == FlagId.None) continue;
            if ((mask & (1 << (int)id)) != 0) plantedFlags.Add(id);
        }

        carriedFlag = (FlagId)PlayerPrefs.GetInt(KeyCarried, (int)FlagId.None);
        LastScene = PlayerPrefs.GetString(KeyLastScene, string.Empty);
    }
}
