using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// يُلحق ردودنا بأحداث مرحلة التوايلايت: نقاط حفظ علي وموته.
///
/// مرحلة التوايلايت لها نظامها الخاص — <c>A_CheckPoint</c> و
/// <c>A_PlayerDeath_WaterSection</c> — ولا تمرّ بـ<c>Checkpoint</c> ولا
/// <c>PlayerKillable</c>. فكان اللاعب يمرّ على نقطة حفظ هناك فلا يرى الوجه ولا
/// تهتزّ يده، ويموت فلا يحسّ شيئًا، بينما يفعل ذلك كلَّه في بقيّة اللعبة.
///
/// والوصل بلا تعديل سطر واحد عند علي: سكربتاته تُطلق <b>أحداثًا عامّة</b> أصلًا،
/// فنشترك فيها <b>بالاسم عبر الانعكاس</b> — فلا نُترجَم معها، وإن غُيّرت طبعنا سطرًا
/// وسكتنا، ومرحلته تعمل كما كانت.
///
/// يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DisallowMultipleComponent]
public class AliEventBridge : MonoBehaviour
{
    private const BindingFlags Any =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static AliEventBridge instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("AliEventBridge") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<AliEventBridge>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>ما اشتركنا فيه من أحداث كائناتٍ بعينها، لنفكّه حين يذهب سينها.</summary>
    private readonly System.Collections.Generic.List<(EventInfo info, object target, Delegate handler)>
        bound = new System.Collections.Generic.List<(EventInfo, object, Delegate)>();

    private bool staticBound;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindStatic();
        BindScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unbind();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Unbind();       // كائنات السين السابق ذهبت، وأحداثها معها
        BindScene();
    }

    /// <summary>
    /// حدثُ نقاط الحفظ <b>ساكن</b>: يعيش بلا كائن، فنشترك فيه مرّة واحدة لا مع كل سين
    /// — والاشتراك المكرّر يجعل النقطة الواحدة تُعلن مرّتين ثم ثلاثًا.
    /// </summary>
    private void BindStatic()
    {
        if (staticBound) return;

        Type type = Find("A_CheckPoint");
        EventInfo info = type?.GetEvent("OnCheckPoint", Any);
        if (info == null) return;

        MethodInfo method = GetType().GetMethod(nameof(OnAliCheckpoint),
                                                BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            info.AddEventHandler(null, Delegate.CreateDelegate(info.EventHandlerType, this, method));
            staticBound = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AliEventBridge] ما نفع الاشتراك في A_CheckPoint.OnCheckPoint: " +
                             $"{e.Message}", this);
        }
    }

    /// <summary>أحداثُ الموت على كائنٍ في السين، فتُربط مع كل سين وتُفكّ معه.</summary>
    private void BindScene()
    {
        Type type = Find("A_PlayerDeath_WaterSection");
        if (type == null) return;

        foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (component == null || component.GetType() != type) continue;

            Listen(component, "OnPlayerDeath", nameof(OnAliDeath));
            Listen(component, "OnPlayerFall", nameof(OnAliFall));
        }
    }

    private void Listen(object target, string eventName, string methodName)
    {
        EventInfo info = target.GetType().GetEvent(eventName, Any);
        if (info == null) return;

        MethodInfo method = GetType().GetMethod(methodName,
                                                BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            Delegate handler = Delegate.CreateDelegate(info.EventHandlerType, this, method);
            info.AddEventHandler(target, handler);
            bound.Add((info, target, handler));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AliEventBridge] ما نفع الاشتراك في {eventName}: {e.Message}", this);
        }
    }

    private void Unbind()
    {
        foreach ((EventInfo info, object target, Delegate handler) in bound)
        {
            // الكائن قد يكون دُمِّر مع سينه، وفكُّ الاشتراك منه يرمي
            try { if (target is UnityEngine.Object o && o != null) info.RemoveEventHandler(target, handler); }
            catch (Exception) { }
        }

        bound.Clear();
    }

    /// <summary>
    /// الصنف بالاسم من نفس التجميعة.
    ///
    /// كلّنا في <c>Assembly-CSharp</c>، فالبحث فيها وحدها يكفي — ولا نذكر الصنف
    /// نصًّا في الكود فلا نُترجَم معه ولا ينكسر بناؤنا إن حُذف.
    /// </summary>
    private static Type Find(string name)
    {
        Type type = typeof(AliEventBridge).Assembly.GetType(name);
        if (type == null)
            Debug.LogWarning($"[AliEventBridge] ما لقيت صنفًا اسمه \"{name}\" — " +
                             "تخطّينا ربطه، ومرحلة التوايلايت تعمل كما كانت.");

        return type;
    }

    // ---------- الردود ----------

    private void OnAliCheckpoint(int number)
    {
        CheckpointFace.Announce();
        PadRumble.Tick();
        ChromaEvents.RaiseCheckpoint(ChromaEvents.PlayerPosition());
    }

    private void OnAliDeath()
    {
        PadRumble.Hit();
        ChromaEvents.RaisePlayerDied(ChromaEvents.PlayerPosition());
    }

    /// <summary>السقوط في الماء ليس موتًا تامًّا عنده، فضربته أخفّ.</summary>
    private void OnAliFall() => PadRumble.Play(0.5f, 0.3f, 0.22f);
}
