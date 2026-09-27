using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// يفتح شيئًا بعد لحظة لا في نفس اللحظة — ليأخذ اللاعب نفسه قبل أن يُنقل.
///
/// المشكلة التي يحلّها: لغز يُحلّ، فينفتح ستار وتظهر أشياء **ويُفتح طريق العودة
/// كله في إطار واحد**. فيُنقل اللاعب قبل أن يرى ما فعله، فيبدو الانتقال بلا سبب.
/// اربط هذا بين الحدث وبين فتح الطريق: يُفتح الستار فورًا، ويتأخّر الطريق ثوانيَ.
///
/// <see cref="requirePlayerAway"/> يزيد شرطًا ثانيًا: لا يُفتح والّلاعب واقف عليه.
/// تفعيل كائن فيه تريغر حول اللاعب يطلق <c>OnTriggerEnter</c> فورًا، فينتقل من حيث
/// يقف بلا أن يمشي إليه — وهذا أسوأ من السرعة، لأنه يبدو عطلًا لا تصميمًا.
///
/// اربط <see cref="Reveal"/> بأي <c>UnityEvent</c> (مثلًا <c>EmblemPuzzle.OnSolved</c>).
/// </summary>
[DisallowMultipleComponent]
public class DelayedReveal : MonoBehaviour
{
    [Header("متى")]
    [Tooltip("تأخير قبل الفتح (ثواني) — هذي هي اللحظة التي يتنفّس فيها اللاعب")]
    [SerializeField] private float delay = 4f;
    [Tooltip("لا يفتح والّلاعب واقف على الكائن — ينتظر حتى يبتعد")]
    [SerializeField] private bool requirePlayerAway = true;
    [Tooltip("كم مترًا يُعدّ ابتعادًا كافيًا")]
    [SerializeField] private float clearance = 5f;
    [Tooltip("أقصى انتظار للابتعاد قبل أن يفتح على أي حال — صفر = ينتظر بلا حد")]
    [SerializeField] private float giveUpAfter = 20f;
    [SerializeField] private string playerTag = "Player";

    [Header("ماذا")]
    [Tooltip("كائنات تُفعّل")]
    [SerializeField] private GameObject[] enable;
    [Tooltip("كائنات تُطفأ في نفس اللحظة")]
    [SerializeField] private GameObject[] disable;

    [Header("بوابة تظهر")]
    [Tooltip("بريفاب يُنشأ عند الفتح — مؤثّر بوابة مثلًا. بلا هذا يصير الطريق " +
             "تريغرًا خفيًّا ينقل اللاعب بلا أن يرى سببًا")]
    [SerializeField] private GameObject portalPrefab;
    [Tooltip("أين يُنشأ — فارغ = أول كائن في قائمة Enable")]
    [SerializeField] private Transform portalAt;
    [Tooltip("إزاحة عن تلك النقطة")]
    [SerializeField] private Vector3 portalOffset;
    [Tooltip("حجمه")]
    [SerializeField] private float portalScale = 1f;
    [Tooltip("يكبر من الصفر إلى حجمه خلال هذي الثواني — الظهور دفعةً واحدة يبدو خطأً")]
    [SerializeField] private float portalGrow = 1.4f;

    [Header("السحب إلى البوابة")]
    [Tooltip("تسحب اللاعب إليها بعد أن تكتمل. بدونها عليه أن يجدها ويمشي إليها، " +
             "وقد لا يراها أصلًا")]
    [SerializeField] private bool pullPlayer = true;
    [Tooltip("وقفة بعد اكتمال البوابة قبل السحب — لحظة يفهم فيها ما يحدث")]
    [SerializeField] private float pullDelay = 0.5f;
    [Tooltip("مدة السحب")]
    [SerializeField] private float pullTime = 1.6f;
    [Tooltip("متغيّرات المشي في الأنيميتر — تُصفَّر أثناء السحب، وإلا بان ماشيًا في " +
             "مكانه وهو مسحوب: تعطيل سكربت الحركة لا يلمس الأنيميتر")]
    [SerializeField] private string[] walkParameters = { "speed", "MovementBlend", "isPushing" };
    [Tooltip("سكربتات حركة اللاعب — تُطفأ أثناء السحب بالاسم لا بمرجع")]
    [SerializeField] private string[] freezeScriptsNamed =
    {
        "PlayerController",
        "A_CrouchAndJump",
        "A_ZipLineSystem",
        "LadderController",
        "BoxPusher",
    };

    [Header("يبدأ من لغز الأعمدة")]
    [Tooltip("يلقى RotaryPuzzle في السين ويشترك في حدث حلّه بنفسه. اللغز داخل بريفاب، " +
             "وربط حدثه بكائن في السين يدويًا لا يعبر حدود البريفاب بثبات")]
    [SerializeField] private bool watchRotaryPuzzle;

    [Header("أحداث")]
    [Tooltip("لحظة الفتح — صوت، تلميح، ضوء...")]
    public UnityEvent onRevealed;
    [Tooltip("بعد أن يصل اللاعب البوابة — هنا يُربط الانتقال إلى السين التالي")]
    public UnityEvent onArrived;

    private bool running;
    private bool done;
    private RotaryPuzzle puzzle;

    private void Start()
    {
        if (!watchRotaryPuzzle) return;

        puzzle = FindFirstObjectByType<RotaryPuzzle>();
        if (puzzle == null)
        {
            Debug.LogWarning("[DelayedReveal] ما لقيت RotaryPuzzle في السين.", this);
            return;
        }

        puzzle.onSolved.AddListener(Reveal);
    }

    private void OnDestroy()
    {
        if (puzzle != null) puzzle.onSolved.RemoveListener(Reveal);
    }

    /// <summary>يبدأ العدّ ثم يفتح — اربطه بحدث حلّ اللغز.</summary>
    public void Reveal()
    {
        if (running || done) return;
        running = true;
        StartCoroutine(Routine());
    }

    /// <summary>يفتح فورًا بلا تأخير ولا شروط — للحالات الاستثنائية.</summary>
    public void RevealNow()
    {
        if (done) return;
        StopAllCoroutines();
        Apply();
    }

    private IEnumerator Routine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (requirePlayerAway)
        {
            float waited = 0f;
            while (!PlayerIsAway())
            {
                waited += Time.deltaTime;
                if (giveUpAfter > 0f && waited >= giveUpAfter)
                {
                    Debug.LogWarning($"[DelayedReveal] اللاعب ما ابتعد خلال {giveUpAfter} ثانية " +
                                     "— فتحت على أي حال حتى لا تعلق المرحلة.", this);
                    break;
                }

                yield return null;
            }
        }

        Apply();
    }

    /// <summary>هل ابتعد اللاعب عن كل ما سنفتحه؟</summary>
    private bool PlayerIsAway()
    {
        var go = PlayerLocator.Find(playerTag);
        if (go == null) return true;   // ما فيه لاعب = ما فيه ما يمنع

        Vector3 player = go.transform.position;
        foreach (GameObject target in enable)
        {
            if (target == null) continue;
            if (Vector3.Distance(player, target.transform.position) < clearance) return false;
        }

        return true;
    }

    private void Apply()
    {
        done = true;
        running = false;

        Transform portal = null;
        try { portal = SpawnPortal(); }
        catch (System.Exception e)
        {
            // البوابة زينة: فشلها لا يمنع فتح الطريق، وإلا حُبس اللاعب في المرحلة
            Debug.LogWarning($"[DelayedReveal] ما قدرت أنشئ البوابة: {e.Message}", this);
        }

        foreach (GameObject target in disable)
            if (target != null) target.SetActive(false);

        StartCoroutine(Arrive(portal));
    }

    /// <summary>
    /// مشهد الوصول: تكبر البوابة، ثم يُفتح الطريق، ثم تسحب اللاعب إليها.
    ///
    /// الترتيب مقصود: الطريق (التريغر) يُفتح <b>قبل</b> السحب لا بعده، فيلتقط
    /// اللاعب لحظة وصوله. ولو فُتح بعد السحب لوقف عليه ولم يحدث شيء.
    /// </summary>
    private IEnumerator Arrive(Transform portal)
    {
        if (portal != null && portalGrow > 0f)
        {
            Vector3 full = portal.localScale;
            float t = 0f;
            while (t < portalGrow)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / portalGrow);
                portal.localScale = full * k;
                yield return null;
            }

            portal.localScale = full;
        }

        foreach (GameObject target in enable)
            if (target != null) target.SetActive(true);

        onRevealed?.Invoke();

        if (!pullPlayer || portal == null) yield break;

        if (pullDelay > 0f) yield return new WaitForSeconds(pullDelay);
        yield return Pull(portal);

        onArrived?.Invoke();   // هنا يُنادى الانتقال، بعد أن يصل اللاعب فعلًا
    }

    /// <summary>
    /// يسحب اللاعب إلى البوابة. الـ<c>CharacterController</c> يُطفأ أثناءها وإلا
    /// قاوم النقل، وسكربتات حركته تُطفأ بالاسم وإلا نازعتنا على موضعه.
    /// </summary>
    private IEnumerator Pull(Transform portal)
    {
        var go = PlayerLocator.Find(playerTag);
        if (go == null) yield break;

        var controller = go.GetComponentInParent<CharacterController>();
        Transform body = controller != null ? controller.transform : go.transform;

        Vector3 from = body.position;
        Quaternion fromRot = body.rotation;
        Vector3 to = portal.position;
        Vector3 flat = Vector3.ProjectOnPlane(to - from, Vector3.up);
        Quaternion toRot = Quaternion.LookRotation(
            flat.sqrMagnitude > 0.001f ? flat : body.forward, Vector3.up);

        // try/finally حول الحركة كلها: لو انرمى استثناء في المنتصف — أو أُوقف
        // الكوروتين لأي سبب — رجع التحكّم للاعب. بدونه يعلق مجمّدًا بلا مخرج،
        // وهذا أسوأ من ألّا يعمل المشهد أصلًا
        Freeze(true);
        StopWalkAnimation(go);
        if (controller != null) controller.enabled = false;

        try
        {
            float t = 0f;
            while (t < pullTime && body != null)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / pullTime);
                body.position = Vector3.Lerp(from, to, k);
                body.rotation = Quaternion.Slerp(fromRot, toRot, k);
                yield return null;
            }
        }
        finally
        {
            if (controller != null) controller.enabled = true;
            Freeze(false);
        }
    }

    /// <summary>
    /// يصفّر متغيّرات المشي. تعطيل سكربت الحركة يوقف اللاعب لكنه لا يلمس الأنيميتر،
    /// فتبقى قيمة السرعة على آخر ما كانت ويظلّ يمشي في مكانه وهو مسحوب.
    /// </summary>
    private void StopWalkAnimation(GameObject player)
    {
        if (walkParameters == null || player == null) return;

        var animator = player.GetComponentInParent<Animator>();
        if (animator == null) animator = player.GetComponentInChildren<Animator>();
        if (animator == null) return;

        foreach (string wanted in walkParameters)
        {
            if (string.IsNullOrWhiteSpace(wanted)) continue;

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name != wanted.Trim()) continue;

                if (parameter.type == AnimatorControllerParameterType.Float)
                    animator.SetFloat(parameter.nameHash, 0f);
                else if (parameter.type == AnimatorControllerParameterType.Bool)
                    animator.SetBool(parameter.nameHash, false);

                break;
            }
        }
    }

    /// <summary>بالاسم لا بمرجع: سكربتات الحركة ملك غيرنا فلا نُترجَم معها.</summary>
    private void Freeze(bool on)
    {
        if (freezeScriptsNamed == null) return;

        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None))
        {
            if (behaviour == null) continue;

            string name = behaviour.GetType().Name;
            foreach (string wanted in freezeScriptsNamed)
            {
                if (string.IsNullOrWhiteSpace(wanted)) continue;
                if (name != wanted.Trim()) continue;

                behaviour.enabled = !on;
                break;
            }
        }
    }

    /// <summary>
    /// يُظهر البوابة في مكان الطريق. تُنشأ قبل تفعيل التريغر بسطر واحد فتكون
    /// موجودة لحظة انفتاحه — فيرى اللاعب بوابةً يمشي إليها، لا تريغرًا خفيًّا
    /// يخطفه من مكانه.
    /// </summary>
    private Transform SpawnPortal()
    {
        if (portalPrefab == null) return null;

        Transform at = portalAt;
        if (at == null && puzzle != null) at = puzzle.transform;
        if (at == null && enable != null)
            foreach (GameObject target in enable)
                if (target != null) { at = target.transform; break; }

        if (at == null)
        {
            Debug.LogWarning("[DelayedReveal] ما فيه مكان أضع فيه البوابة.", this);
            return null;
        }

        GameObject portal = Instantiate(portalPrefab, at.position + portalOffset, at.rotation);
        portal.name = portalPrefab.name + " (بوابة)";
        portal.transform.localScale = portalPrefab.transform.localScale * portalScale;
        portal.SetActive(true);

        // زينة لا جسم: لا نريد لمؤثّر أن يدفع اللاعب أو يسدّ عليه الطريق
        foreach (var collider in portal.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        return portal.transform;
    }

    [ContextMenu("تجربة: افتح الآن")]
    private void DebugReveal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DelayedReveal] جرّبها أثناء التشغيل.", this);
            return;
        }

        RevealNow();
    }

    private void OnValidate()
    {
        delay = Mathf.Max(0f, delay);
        clearance = Mathf.Max(0f, clearance);
        giveUpAfter = Mathf.Max(0f, giveUpAfter);
        portalScale = Mathf.Max(0.01f, portalScale);
        portalGrow = Mathf.Max(0f, portalGrow);
        pullDelay = Mathf.Max(0f, pullDelay);
        pullTime = Mathf.Max(0.05f, pullTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!requirePlayerAway || enable == null) return;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        foreach (GameObject target in enable)
        {
            if (target == null) continue;
            Gizmos.DrawWireSphere(target.transform.position, clearance);
            Gizmos.DrawLine(transform.position, target.transform.position);
        }
    }
}
