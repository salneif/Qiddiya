using System.Reflection;
using UnityEngine;

/// <summary>
/// يمنع القفزة الثانية غير المقصودة، ويُبقي تسامح القفز على الحواف كما هو.
///
/// العلّة في سكربت القفز: نافذة التسامح (coyote time) تُبقي القفز متاحًا نصف ثانية
/// بعد مغادرة الأرض — وهذا مطلوب لمن يضغط بعد الحافة بشعرة — لكن <b>لا شيء يغلقها
/// حين تُستهلك قفزة</b>. فمن قفز صار أمامه نصف ثانية ليقفز مرّة أخرى في الهواء.
///
/// الإصلاح الصحيح سطران في ذلك السكربت، لكنه ليس لنا. فهذا يغلق النافذة من خارجها:
/// يميّز <b>القفز</b> من <b>المشي عن الحافة</b> — الأول يرفع اللاعب والثاني لا —
/// فيغلقها عند القفز وحده ويترك التسامح للحالة التي وُضع لها.
///
/// كل الوصول <b>بالاسم عبر الانعكاس</b>: لا يذكر صنف أحد ولا يُترجَم معه. إن غُيّر
/// اسم السكربت أو حقوله طبع سطرًا وعطّل نفسه — ولا ينكسر بناء أحد.
///
/// حُطّه على أي كائن في السين الذي تريد ضبطه.
/// </summary>
[DisallowMultipleComponent]
public class DoubleJumpGuard : MonoBehaviour
{
    [Header("الهدف")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("اسم سكربت القفز — بالاسم لا بمرجع، فلا نُترجَم مع سكربت غيرنا")]
    [SerializeField] private string jumpScriptName = "A_CrouchAndJump";
    [Tooltip("حقل «القفز متاح» داخله")]
    [SerializeField] private string canJumpField = "canJump";
    [Tooltip("حقل عدّاد نافذة التسامح داخله")]
    [SerializeField] private string windowField = "_currentWidow";

    [Header("الكشف")]
    [Tooltip("كم يُراقَب الصعود بعد مغادرة الأرض قبل الحكم بأنه مشى عن حافة")]
    [SerializeField] private float watchTime = 0.2f;
    [Tooltip("ارتفاع يُعدّ قفزًا — المشي عن الحافة لا يرفع اللاعب أبدًا")]
    [SerializeField] private float riseToCount = 0.05f;
    [SerializeField] private bool log;

    private CharacterController controller;
    private Component jumpScript;
    private FieldInfo canJump;
    private FieldInfo window;

    private bool wasGrounded = true;
    private bool watching;
    private float watchedFor;
    private float leftGroundAt;
    private bool ready;
    private bool failed;

    private void LateUpdate()
    {
        if (failed) return;
        if (!ready && !Bind()) return;

        bool grounded = controller.isGrounded;
        float y = controller.transform.position.y;

        if (grounded)
        {
            watching = false;
        }
        else if (wasGrounded)
        {
            // غادر الأرض للتوّ — لا نعرف بعدُ أقفز أم مشى عن حافة
            watching = true;
            watchedFor = 0f;
            leftGroundAt = y;
        }
        else if (watching)
        {
            watchedFor += Time.deltaTime;

            if (y - leftGroundAt >= riseToCount)
            {
                Close();
                watching = false;
            }
            else if (watchedFor >= watchTime)
            {
                watching = false;   // ما ارتفع: مشى عن الحافة، فالتسامح حقّه
            }
        }

        wasGrounded = grounded;
    }

    /// <summary>يغلق نافذة التسامح: القفزة استُهلكت فلا ثانية في الهواء.</summary>
    private void Close()
    {
        canJump.SetValue(jumpScript, false);
        window.SetValue(jumpScript, -1f);

        if (log) Debug.Log("[DoubleJumpGuard] أغلقت النافذة بعد قفزة.", this);
    }

    /// <summary>
    /// يمسك السكربت وحقليه بالاسم. الفشل هنا ليس خطأً: يعني أن السكربت تغيّر،
    /// فنطبع سطرًا ونصمت بدل أن نملأ الكونسول كل إطار.
    /// </summary>
    private bool Bind()
    {
        var go = PlayerLocator.Find(playerTag);
        if (go == null) return false;

        controller = go.GetComponentInParent<CharacterController>();
        if (controller == null) return Fail("ما فيه CharacterController على اللاعب.");

        foreach (var component in controller.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || component.GetType().Name != jumpScriptName.Trim()) continue;
            jumpScript = component;
            break;
        }

        if (jumpScript == null)
            return Fail($"ما لقيت سكربتًا اسمه \"{jumpScriptName}\" على اللاعب.");

        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        canJump = jumpScript.GetType().GetField(canJumpField.Trim(), Any);
        window = jumpScript.GetType().GetField(windowField.Trim(), Any);

        if (canJump == null || canJump.FieldType != typeof(bool))
            return Fail($"ما لقيت حقلًا منطقيًا اسمه \"{canJumpField}\".");

        if (window == null || window.FieldType != typeof(float))
            return Fail($"ما لقيت حقلًا عشريًا اسمه \"{windowField}\".");

        ready = true;
        return true;
    }

    private bool Fail(string why)
    {
        failed = true;
        Debug.LogWarning($"[DoubleJumpGuard] {why} عُطّل الحارس، والقفز يعمل كما كان.", this);
        return false;
    }

    private void OnValidate()
    {
        watchTime = Mathf.Max(0.02f, watchTime);
        riseToCount = Mathf.Max(0.001f, riseToCount);
    }
}
