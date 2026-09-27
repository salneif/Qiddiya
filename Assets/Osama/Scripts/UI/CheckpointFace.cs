using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// يُعلن نقطة الحفظ: رأس الشخصية يطلع من ركن الشاشة أسفل اليمين، يلتفت لحظة، ثم يعود.
///
/// نقاط الحفظ كانت صامتة تمامًا — يمرّ عليها اللاعب فلا يدري أنها سُجّلت، فيلعب
/// المقطع بحذر من لا يعرف أين سيعود. والإعلان يجب أن يكون <b>في الركن</b> لا في
/// الوسط: خبرٌ طيّب لا يستحق مقاطعة اللعب.
///
/// يسمع <c>Checkpoint.Activated</c> مرّة واحدة، فيعمل مع كل نقطة حفظ في كل سين بلا
/// ربط ولا كائن في المشهد — وأي نقطة تُضاف لاحقًا تعمل معه وحدها.
/// </summary>
[DisallowMultipleComponent]
public class CheckpointFace : MonoBehaviour
{
    private const float HeadHeight = 170f;
    private const float SlideSeconds = 0.45f;
    private const float StaySeconds = 2.2f;
    private const float HiddenDrop = 220f;      // كم ينزل تحت الحافة وهو مختفٍ
    private const float AnyGap = 4f;            // أقلّ فاصل بين إعلانين أيًّا كانا
    private const float SameGap = 30f;           // وفاصلٌ أطول لتكرار النقطة نفسها

    private static CheckpointFace instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("CheckpointFace") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<CheckpointFace>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private Canvas canvas;
    private CanvasGroup group;
    private TurningHead head;
    private Coroutine showing;
    private Checkpoint last;
    private float lastAt = -999f;

    private void OnEnable() => Checkpoint.Activated += OnCheckpoint;
    private void OnDisable() => Checkpoint.Activated -= OnCheckpoint;

    /// <summary>
    /// خبرٌ طيّب يُقال مرّة، لا كلّما مررتَ.
    ///
    /// نقاط الحفظ التي <c>Trigger Once</c> مطفأ فيها تُطلق كلّما دخلها اللاعب — ومن
    /// وقف على حافة نقطة يدخل ويخرج منها مرارًا. فنصمت عن تكرار النقطة نفسها نصف
    /// دقيقة، وعن أي إعلان ثانٍ لثوانٍ. والعودة بعد موت تُعلن عادةً: هي خبرٌ حينها.
    /// </summary>
    private void OnCheckpoint(Checkpoint point)
    {
        float now = Time.unscaledTime;
        if (now - lastAt < AnyGap) return;
        if (point == last && now - lastAt < SameGap) return;

        last = point;
        lastAt = now;

        if (canvas == null && !Build()) return;

        if (showing != null) StopCoroutine(showing);
        showing = StartCoroutine(Show());
    }

    private IEnumerator Show()
    {
        canvas.gameObject.SetActive(true);
        head.Rest();

        yield return Slide(0f, 1f);

        for (float t = 0f; t < StaySeconds; t += Time.unscaledDeltaTime)
        {
            head.Turn(Time.unscaledDeltaTime);
            yield return null;
        }

        yield return Slide(1f, 0f);

        canvas.gameObject.SetActive(false);
        showing = null;
    }

    /// <summary>
    /// يزحف من تحت الحافة ويخفت معًا. والالتفاتة تكمل أثناء الزحف فلا يبدو صورةً
    /// تُدفع، بل رأسًا يطلّ.
    /// </summary>
    private IEnumerator Slide(float from, float to)
    {
        RectTransform rect = head.Rect;

        for (float t = 0f; t < SlideSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / SlideSeconds);
            k = k * k * (3f - 2f * k);
            float at = Mathf.Lerp(from, to, k);

            head.Turn(Time.unscaledDeltaTime);

            // الالتفاتة كتبت الموضع للتوّ، فنُزيحه بعدها لا قبلها
            rect.anchoredPosition += new Vector2(0f, -HiddenDrop * (1f - at));
            group.alpha = at;
            yield return null;
        }

        head.Turn(0f);
        group.alpha = to;
    }

    private bool Build()
    {
        if (TurningHead.Art == null) return false;

        var root = new GameObject("CheckpointFace_Canvas");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3500;        // فوق واجهات اللعب، وتحت شاشة التحميل

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;      // إعلانٌ لا واجهة: لا يبتلع نقرة
        group.interactable = false;
        group.alpha = 0f;

        head = TurningHead.Build(root.transform, HeadHeight, new Vector2(1f, 0f),
                                 new Vector2(56f, 40f));

        if (head == null) { Destroy(root); canvas = null; return false; }

        canvas.gameObject.SetActive(false);
        return true;
    }
}
