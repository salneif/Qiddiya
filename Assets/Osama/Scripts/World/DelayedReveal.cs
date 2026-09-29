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
    [Tooltip("تظهر أمام اللاعب مباشرة بدل مكان ثابت. الأصحّ للغز: نقطة أصل اللغز " +
             "قد تكون تحت الأرضية أو في وسط المجسّم، وأمام اللاعب دائمًا صحيح")]
    [SerializeField] private bool portalInFrontOfPlayer = true;
    [Tooltip("كم مترًا أمامه")]
    [SerializeField] private float portalDistance = 2.6f;
    [Tooltip("أين يُنشأ إن لم يكن أمام اللاعب — فارغ = أول كائن في قائمة Enable")]
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
    [Tooltip("قوّة الشفط: 1 = حركة متّزنة، 3 = يتباطأ في أوّله ثم ينقضّ في آخره")]
    [Range(1f, 4f)] [SerializeField] private float suction = 2.6f;
    [Tooltip("كم يصغر اللاعب وهو يدخل. 1 = لا يصغر")]
    [Range(0.2f, 1f)] [SerializeField] private float suctionShrink = 0.55f;
    [Tooltip("لفّة حول نفسه في آخر الطريق (درجات). صفر = بلا لفّ")]
    [SerializeField] private float suctionSpin = 420f;
    [Tooltip("دمدمةٌ تشتدّ في اليد مع الشفط")]
    [SerializeField] private bool rumbleWhilePulled = true;
    [Tooltip("مدة السحب")]
    [SerializeField] private float pullTime = 1.6f;
    [Tooltip("متغيّرات المشي في الأنيميتر — تُصفَّر أثناء السحب، وإلا بان ماشيًا في " +
             "مكانه وهو مسحوب: تعطيل سكربت الحركة لا يلمس الأنيميتر")]
    [SerializeField] private string[] walkParameters = { "speed", "MovementBlend" };
    [Tooltip("يُبقي أنميشن المشي شغّالًا أثناء السحب فيبدو ماشيًا نحو البوابة. " +
             "إيقافه يجعله ينزلق واقفًا، ويُقرأ كأنه لم يُسحب أصلًا")]
    [SerializeField] private bool walkWhilePulled = true;
    [Tooltip("قيمة سرعة المشي في الأنيميتر أثناء السحب")]
    [SerializeField] private float walkValue = 1f;
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

    [Tooltip("يطبع في الكونسول ما حدث فعلًا في كل خطوة")]
    [SerializeField] private bool log;

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

        if (log) Debug.Log($"[DelayedReveal] فُتح الطريق • السحب مفعّل={pullPlayer}", this);

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
        if (go == null)
        {
            Debug.LogWarning("[DelayedReveal] ما لقيت اللاعب — لا سحب.", this);
            yield break;
        }

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
        if (log)
            Debug.Log($"[DelayedReveal] سحب «{body.name}» من {from} إلى {to} " +
                      $"({Vector3.Distance(from, to):0.0} م خلال {pullTime} ث) " +
                      $"• CharacterController={(controller != null ? "نعم" : "لا")}", this);

        // مسافةٌ لا تُرى ليست شفطًا. نقولها بصوتٍ عالٍ بدل أن يبحث أحد عن العلّة
        float span = Vector3.Distance(from, to);
        if (span < 0.75f)
            Debug.LogWarning($"[DelayedReveal] البوابة على بُعد {span:0.00} م من اللاعب فقط — " +
                             "لن يُرى شفط. ارفع Portal Distance.", this);

        Vector3 fromScale = body.localScale;

        Freeze(true);
        SetWalk(go, walkWhilePulled ? walkValue : 0f);
        if (controller != null) controller.enabled = false;

        try
        {
            float t = 0f;
            while (t < pullTime && body != null)
            {
                // زمنٌ غير متأثّر بالتوقّف: هذا مشهدٌ يُعرض، ولوحةُ تحذيرٍ أو قائمةٌ
                // تفتح في أثنائه كانت تُجمّده في منتصفه
                t += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(t / pullTime);

                // تسارعٌ لا منحنى ناعم: الناعم يبدأ سريعًا وينتهي بطيئًا فيُقرأ مشيًا،
                // والقوّة تجعله يتردّد في أوّله ثم ينقضّ في آخره — وهذا ما يُقرأ شفطًا
                float k = Mathf.Pow(n, suction);

                body.position = Vector3.Lerp(from, to, k);

                // يستدير نحو البوابة أوّلًا، ثم يلفّ حول نفسه وهو يُبتلع
                Quaternion facing = Quaternion.Slerp(fromRot, toRot, Mathf.Clamp01(n * 3f));
                body.rotation = facing * Quaternion.Euler(0f, suctionSpin * k, 0f);

                body.localScale = fromScale * Mathf.Lerp(1f, suctionShrink, k);

                if (rumbleWhilePulled) PadRumble.Hold(0.1f + 0.25f * k, 0.05f + 0.12f * k);   // سحبٌ يُحسّ ولا يهزّ الكفّين
                yield return null;
            }
        }
        finally
        {
            SetWalk(go, 0f);   // يقف عند الوصول لا يظلّ يمشي في مكانه
            if (body != null) body.localScale = fromScale;   // لا يدخل السين التالي قزمًا
            if (controller != null) controller.enabled = true;
            Freeze(false);

            if (log)
                Debug.Log($"[DelayedReveal] انتهى السحب — اللاعب الآن عند {body.position} " +
                          $"(المطلوب {to}، الفرق {Vector3.Distance(body.position, to):0.00} م)", this);
        }
    }

    /// <summary>
    /// يضبط متغيّرات المشي في الأنيميتر. تعطيل سكربت الحركة لا يلمس الأنيميتر، فلا
    /// هو يوقف المشي ولا يشغّله — نحن من يقرّر: يمشي وهو مسحوب، ويقف عند الوصول.
    /// </summary>
    private void SetWalk(GameObject player, float value)
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
                    animator.SetFloat(parameter.nameHash, value);
                else if (parameter.type == AnimatorControllerParameterType.Bool)
                    animator.SetBool(parameter.nameHash, value > 0.01f);

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

        Vector3 place;
        Quaternion facing;
        // نُخبر عن المكان قبل الإنشاء، فإن طلع في غير محلّه عرفنا لماذا

        if (portalInFrontOfPlayer)
        {
            var playerGo = PlayerLocator.Find(playerTag);
            if (playerGo == null)
            {
                Debug.LogWarning("[DelayedReveal] ما لقيت اللاعب لأضع البوابة أمامه.", this);
                return null;
            }

            Transform p = playerGo.transform;
            Vector3 ahead = Vector3.ProjectOnPlane(p.forward, Vector3.up).normalized;
            if (ahead.sqrMagnitude < 0.01f) ahead = Vector3.forward;

            // من قدمي اللاعب لا من مركزه، وإلا طفت البوابة أو دُفنت
            place = p.position + ahead * portalDistance;
            facing = Quaternion.LookRotation(-ahead, Vector3.up);
        }
        else
        {
            Transform at = portalAt;
            if (at == null && enable != null)
                foreach (GameObject target in enable)
                    if (target != null) { at = target.transform; break; }

            if (at == null)
            {
                Debug.LogWarning("[DelayedReveal] ما فيه مكان أضع فيه البوابة.", this);
                return null;
            }

            place = at.position;
            facing = at.rotation;
        }

        if (log) Debug.Log($"[DelayedReveal] البوابة عند {place + portalOffset}", this);

        GameObject portal = Instantiate(portalPrefab, place + portalOffset, facing);
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
        portalDistance = Mathf.Max(0.5f, portalDistance);
        portalGrow = Mathf.Max(0f, portalGrow);
        pullDelay = Mathf.Max(0f, pullDelay);
        pullTime = Mathf.Max(0.1f, pullTime);
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
