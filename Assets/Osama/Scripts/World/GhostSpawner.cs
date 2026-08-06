using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// يولّد مجموعة أشباح من نقطة واحدة (فم المهرج مثلًا) عند دخول اللاعب منطقته.
///
/// يخرجون <b>واحدًا بعد الآخر</b> لا دفعة واحدة: ثلاثة أشباح تظهر معًا تُقرأ
/// كخطأ برمجي، أما خروجهم تباعًا فيُقرأ كشيء يزحف من الداخل — والفارق كله
/// في <see cref="delayBetween"/>.
///
/// يربط العلم بكل شبح مولَّد تلقائيًا، وإلا بدأ الشبح في حالة التراجع بدل المطاردة.
///
/// التركيب: كائن فيه Collider (Is Trigger) عند مدخل المنطقة + هذا السكربت،
/// و<see cref="spawnPoint"/> عند الفم. اربط الستارة بـ<see cref="onSpawnStarted"/>
/// لتختفي لحظة الخروج.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GhostSpawner : MonoBehaviour
{
    [Header("التفعيل")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("يعمل مرة واحدة فقط")]
    [SerializeField] private bool triggerOnce = true;
    [Tooltip("لا ينطلق إلا واللاعب <b>حامل العلم</b>. يمر من هنا ذاهبًا فلا يحدث شيء، " +
             "ويمر راجعًا بالعلم فينفتح الفم.")]
    [SerializeField] private bool requireFlagHeld = false;

    [Header("ما يخرج")]
    [Tooltip("بريفاب الشبح")]
    [SerializeField] private GameObject ghostPrefab;
    [Tooltip("كم شبحًا")]
    [SerializeField] private int count = 3;
    [Tooltip("نقطة الخروج — كائن فارغ داخل فم المهرج. إن تُركت فارغة يُستخدم هذا الكائن.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("تشتّت عشوائي حول نقطة الخروج (متر) — يمنعهم من التطابق التام")]
    [SerializeField] private float spawnSpread = 0.4f;
    [Tooltip("الفاصل بين خروج شبح والذي بعده (ثواني). صفر = يخرجون دفعة واحدة ويبدون خطأً برمجيًا.")]
    [SerializeField] private float delayBetween = 0.5f;

    [Header("الربط")]
    [Tooltip("العلم — يُربط بكل شبح مولَّد. بدونه يبدأ الشبح متراجعًا لا مطاردًا.")]
    [SerializeField] private FlagItem flag;
    [Tooltip("أب اختياري تُوضع تحته الأشباح لترتيب الهيرآركي")]
    [SerializeField] private Transform ghostParent;

    [Header("موسيقى المطاردة")]
    [Tooltip("مقطع المطاردة — يُشغَّل مع خروج الأشباح ويتوقف تلقائيًا عند حذفهم " +
             "(موت أو مخرج). عمره مربوط بعمرهم فيستحيل أن يبقى شغّالًا بعد اختفائهم.")]
    [SerializeField] private AudioClip chaseMusic;

    [Header("لمسات")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت يُشغَّل مع كل شبح يخرج")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private CameraShake cameraShake;

    [Header("أحداث")]
    [Tooltip("لحظة بدء الخروج — اربط هنا اختفاء الستارة")]
    public UnityEvent onSpawnStarted;
    [Tooltip("بعد خروج آخر شبح")]
    public UnityEvent onSpawnFinished;

    [Header("تشخيص")]
    [Tooltip("يطبع في الكونسول سبب عدم انطلاق الكمين. " +
             "لو مرّيت بالمنطقة وما طُبع شيء أبدًا = الكولايدر ما حسّ باللاعب أصلًا. " +
             "أطفئه بعد ما تخلص.")]
    [SerializeField] private bool debugLog = false;

    /// <summary>هل خرجت المجموعة؟</summary>
    public bool HasSpawned { get; private set; }

    /// <summary>الأشباح المولَّدة — نتعقّبها لنقدر نحذفها عند الموت أو عند المخرج.</summary>
    private readonly List<GameObject> spawned = new List<GameObject>();

    private Transform Origin => spawnPoint != null ? spawnPoint : transform;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>
    /// Stay لا Enter: لو دخل اللاعب بلا علم ثم أخذه وهو واقف داخل المنطقة،
    /// ينطلق فورًا بدل أن ينتظر خروجه ودخوله من جديد.
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // HasSpawned تمنع التكرار دائمًا — لا كل إطار داخل المنطقة.
        // مع Trigger Once تظل مرفوعة للأبد، وبدونها تنزل عند الخروج (أدناه).
        if (HasSpawned)
        {
            if (triggerOnce)
                Report("اللاعب داخل المنطقة، لكن الكمين مستهلك (Trigger Once). " +
                       "يُعاد تسليحه بـ DespawnAll فقط — تأكد أنه مربوط بـ PlayerKillable.On Respawn، " +
                       "وأن DespawnPermanently ما انطلق من AudioFadeZone. " +
                       "ولو تبيه يشتغل كل مرة تمر فيها بالعلم، أطفئ Trigger Once.");
            return;
        }

        // الشرط غير متحقق → لا ينطلق ولا يُستهلك، فيبقى مسلّحًا لمروره القادم
        if (requireFlagHeld && flag == null)
        {
            Report("خانة Flag فارغة مع Require Flag Held — لن ينطلق أبدًا.");
            return;
        }

        if (requireFlagHeld && !flag.IsHeld)
        {
            Report($"اللاعب داخل المنطقة لكنه لا يحمل \"{flag.name}\".");
            return;
        }

        Report("الشرط تحقّق — الأشباح تخرج الآن.");
        Spawn();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        lastReason = null; // نسمح بطباعة نفس السبب من جديد في المرور القادم

        // بلا Trigger Once: يُعاد التسليح عند الخروج لا داخل المنطقة —
        // وإلا انطلق كل إطار وأنت واقف فيها وولّد أشباحًا بلا نهاية.
        if (!triggerOnce) HasSpawned = false;
    }

    /// <summary>يطبع السبب مرة واحدة لا كل إطار — OnTriggerStay يعمل ٦٠ مرة في الثانية.</summary>
    private void Report(string reason)
    {
        if (!debugLog || reason == lastReason) return;
        lastReason = reason;
        Debug.Log($"[GhostSpawner] {name}: {reason}", this);
    }

    private string lastReason;

    /// <summary>يطلق الخروج يدويًا — اربطه بأي حدث آخر إن أردت.</summary>
    public void Spawn()
    {
        if (HasSpawned) return;
        if (ghostPrefab == null) return;

        HasSpawned = true;
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        onSpawnStarted?.Invoke();   // هنا تختفي الستارة
        if (cameraShake != null) cameraShake.Shake();

        if (chaseMusic != null && MusicDirector.Instance != null)
            MusicDirector.Instance.PlayOverride(chaseMusic);

        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            SpawnOne();
            if (delayBetween > 0f) yield return new WaitForSeconds(delayBetween);
        }

        onSpawnFinished?.Invoke();
    }

    /// <summary>
    /// يحذف كل الأشباح ويعيد تسليح الكمين ليعمل من جديد —
    /// اربطه بـ PlayerKillable.onRespawn.
    /// </summary>
    public void DespawnAll() => Clear(rearm: true);

    /// <summary>
    /// يحذف الأشباح ويقفل الكمين نهائيًا — اربطه بمخرج المرحلة
    /// حتى لا يلاحقوك للمراحل التالية.
    /// </summary>
    public void DespawnPermanently() => Clear(rearm: false);

    private void Clear(bool rearm)
    {
        StopAllCoroutines();   // يوقف أي خروج لم يكتمل بعد

        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();

        // الموسيقى تموت مع الأشباح — لا مطاردة بلا مطارِد
        if (chaseMusic != null && MusicDirector.Instance != null)
            MusicDirector.Instance.ClearOverride();

        HasSpawned = !rearm;   // عند إعادة التسليح نرجّعها false ليعمل التريغر ثانية
    }

    private void SpawnOne()
    {
        Vector2 c = Random.insideUnitCircle * spawnSpread;
        Vector3 pos = Origin.position + new Vector3(c.x, 0f, c.y);

        var go = Instantiate(ghostPrefab, pos, Origin.rotation);
        if (ghostParent != null) go.transform.SetParent(ghostParent, true);

        // البحث يشمل المعطّل: البريفاب غالبًا محفوظ مطفأً (حتى لا يعمل في المشهد)،
        // والنسخة تولد مطفأة مثله فلا يجده البحث العادي
        var enemy = go.GetComponentInChildren<FlagGlitchEnemy>(true);
        if (enemy != null && flag != null) enemy.SetFlag(flag);

        // نفعّله بعد ربط العلم، فيشترك OnEnable في أحداث العلم وهو مضبوط أصلًا
        go.SetActive(true);
        spawned.Add(go);

        if (spawnSound != null && audioSource != null) audioSource.PlayOneShot(spawnSound);
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c != null)
        {
            Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.7f);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }

        Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.9f);
        Gizmos.DrawWireSphere(Origin.position, spawnSpread);
    }
}
