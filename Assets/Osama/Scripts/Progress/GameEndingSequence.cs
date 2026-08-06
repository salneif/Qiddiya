using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// نهاية اللعبة — تُنادى من <c>FlagBase.On All Flags Planted</c> في القاعدة الثالثة.
///
/// الترتيب:
///  1. تجميد اللاعب (تطفئ سكربتات حركته وتصفّر سرعته).
///  2. سحب الكاميرا للخلف بنعومة — المشهد يتّسع وتظهر الجزر الثلاث منوّرة.
///  3. وقفة قصيرة، ثم تعتيم الشاشة.
///  4. الكريديت: تفعيل كائن (فيه Video Player مثلًا) أو تحميل سين الكريديت.
///
/// ⚠️ سكربت تتبّع الكاميرا لازم يكون في <see cref="disableOnEnding"/>، وإلا شدّ
/// الكاميرا لللاعب كل إطار وما تحرّكت خطوة.
///
/// النوع <c>MonoBehaviour[]</c> مقصود لا <c>Behaviour[]</c> — الثاني يقبل
/// <c>Camera</c> فيطفئها أحدهم بالغلط وتنطفئ الشاشة (خطأ رقم ١٠ في الريدمي).
/// </summary>
public class GameEndingSequence : MonoBehaviour
{
    [Header("تجميد اللاعب")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("سكربتات تُطفأ عند بدء النهاية: حركة اللاعب + تتبّع الكاميرا + أي تحكّم")]
    [SerializeField] private MonoBehaviour[] disableOnEnding;

    [Header("سحب الكاميرا")]
    [Tooltip("الكاميرا المسحوبة — فارغة = الكاميرا الرئيسية")]
    [SerializeField] private Transform cameraToPull;
    [Tooltip("مقدار السحب بفضاء الكاميرا: Z سالب = للخلف، Y موجب = للأعلى")]
    [SerializeField] private Vector3 pullOffset = new Vector3(0f, 3f, -12f);
    [Tooltip("مدة السحب (ثواني)")]
    [SerializeField] private float pullDuration = 5f;
    [SerializeField] private AnimationCurve pullCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("تظل الكاميرا موجّهة لللاعب أثناء السحب")]
    [SerializeField] private bool keepLookingAtPlayer = true;

    [Header("التعتيم")]
    [Tooltip("مموّه الشاشة — يُلتقط تلقائيًا من السين إذا تُرك فارغًا")]
    [SerializeField] private ScreenFader fader;
    [Tooltip("وقفة بعد اكتمال السحب قبل بدء التعتيم (ثواني)")]
    [SerializeField] private float holdBeforeFade = 1f;

    [Header("الكريديت")]
    [Tooltip("كائن يُفعّل بعد التعتيم — حط فيه Video Player + Raw Image لمقطع الكريديت. " +
             "تُكشف الشاشة بعده تلقائيًا.")]
    [SerializeField] private GameObject creditsObject;
    [Tooltip("أو: اسم سين الكريديت المستقل (له أولوية على الكائن أعلاه)")]
    [SerializeField] private string creditsScene;

    [Header("أحداث")]
    [Tooltip("لحظة بدء النهاية — أوقف الموسيقى، اكتم الأصوات...")]
    public UnityEvent onEndingStarted;
    [Tooltip("بعد اكتمال التعتيم — قبل الكريديت مباشرة")]
    public UnityEvent onFadedOut;

    private bool playing;

    /// <summary>يشغّل مشهد النهاية — اربطه بـ FlagBase.On All Flags Planted.</summary>
    public void Play()
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        onEndingStarted?.Invoke();

        Transform player = FreezePlayer();

        yield return PullCamera(player);

        if (holdBeforeFade > 0f) yield return new WaitForSeconds(holdBeforeFade);

        if (fader == null) fader = FindFirstObjectByType<ScreenFader>();

        bool faded = false;
        if (fader != null) fader.FadeOut(() => faded = true);
        else faded = true;

        while (!faded) yield return null;

        onFadedOut?.Invoke();
        ShowCredits();
    }

    private Transform FreezePlayer()
    {
        if (disableOnEnding != null)
            foreach (var b in disableOnEnding)
                if (b != null) b.enabled = false;

        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null) return null;

        // تطفئة السكربت وحدها تترك الجسم ينزلق بسرعته الأخيرة
        var rb = go.GetComponentInParent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        return go.transform;
    }

    private IEnumerator PullCamera(Transform player)
    {
        Transform cam = cameraToPull != null ? cameraToPull
                      : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null || pullDuration <= 0f) yield break;

        Vector3 startPos = cam.position;
        Quaternion startRot = cam.rotation;
        Vector3 endPos = startPos + startRot * pullOffset; // بفضاء الكاميرا لا العالم

        float t = 0f;
        while (t < pullDuration)
        {
            t += Time.deltaTime;
            float k = pullCurve.Evaluate(Mathf.Clamp01(t / pullDuration));

            cam.position = Vector3.Lerp(startPos, endPos, k);

            if (keepLookingAtPlayer && player != null)
            {
                Vector3 dir = player.position - cam.position;
                if (dir.sqrMagnitude > 0.0001f)
                    cam.rotation = Quaternion.Slerp(startRot,
                                                    Quaternion.LookRotation(dir, Vector3.up), k);
            }

            yield return null;
        }

        cam.position = endPos;
    }

    private void ShowCredits()
    {
        if (!string.IsNullOrEmpty(creditsScene))
        {
            if (!Application.CanStreamedLevelBeLoaded(creditsScene))
            {
                Debug.LogError($"[GameEndingSequence] سين الكريديت \"{creditsScene}\" غير موجود " +
                               $"في قائمة مشاهد البناء.", this);
                return;
            }

            SceneManager.LoadSceneAsync(creditsScene);
            return;
        }

        if (creditsObject == null) return;

        creditsObject.SetActive(true);
        if (fader != null) fader.FadeIn(); // نكشف الشاشة على الكريديت
    }
}
