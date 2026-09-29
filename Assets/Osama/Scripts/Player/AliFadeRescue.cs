using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// شبكة أمانٍ لستارة الموت في التوايلايت: لا تبقى الشاشة سوداء.
///
/// <c>A_FadeScreen</c> (علي) يعتّم ثم يفتح بعد ثانيتين بـ<c>Invoke</c> وبالزمن العادي. فلو وقف
/// الزمن لحظتها أو انقطعت السلسلة (موتان متلاحقان) بقيت الشاشة سوداء واللاعب حيّ تحتها. هنا:
/// إن بقيت معتمة أكثر من ٤ ثوانٍ والزمن يمشي، تُفتح كما كان سيفتحها هو.
///
/// بالاسم عبر الانعكاس، بلا سطرٍ في سكربت علي. يسكت في كل مشهدٍ بلا تلك الستارة.
/// </summary>
[DisallowMultipleComponent]
public class AliFadeRescue : MonoBehaviour
{
    private const float StuckAfter = 4f;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly object No = false, Yes = true;

    private sealed class Fade
    {
        public Component script;
        public RawImage image;
        public FieldInfo fading, unfading;
        public float dark;
    }

    private static AliFadeRescue instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("AliFadeRescue") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<AliFadeRescue>();
        DontDestroyOnLoad(host);
        instance.Scan();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private readonly List<Fade> fades = new List<Fade>();

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Scan();
    }

    private void Scan()
    {
        fades.Clear();
        foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (script == null || script.GetType().Name != "A_FadeScreen") continue;
            System.Type type = script.GetType();
            var image = type.GetField("FadeScreen", Any)?.GetValue(script) as RawImage;
            FieldInfo fading = type.GetField("_isFading", Any), unfading = type.GetField("_isUnFading", Any);
            if (image == null || fading == null || unfading == null) continue;
            fades.Add(new Fade { script = script, image = image, fading = fading, unfading = unfading });
        }
    }

    private void LateUpdate()
    {
        if (fades.Count == 0 || Time.timeScale <= 0f) return;   // موقوف: العتمة مشروعة

        foreach (Fade f in fades)
        {
            if (f.script == null || f.image == null) continue;
            f.dark = f.image.color.a >= 0.95f ? f.dark + Time.unscaledDeltaTime : 0f;
            if (f.dark < StuckAfter) continue;

            f.fading.SetValue(f.script, No);
            f.unfading.SetValue(f.script, Yes);
            f.dark = 0f;
        }
    }
}
