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

    [Header("أحداث")]
    [Tooltip("لحظة الفتح — صوت، تلميح، ضوء...")]
    public UnityEvent onRevealed;

    private bool running;
    private bool done;

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

        SpawnPortal();

        foreach (GameObject target in enable)
            if (target != null) target.SetActive(true);

        foreach (GameObject target in disable)
            if (target != null) target.SetActive(false);

        onRevealed?.Invoke();
    }

    /// <summary>
    /// يُظهر البوابة في مكان الطريق. تُنشأ قبل تفعيل التريغر بسطر واحد فتكون
    /// موجودة لحظة انفتاحه — فيرى اللاعب بوابةً يمشي إليها، لا تريغرًا خفيًّا
    /// يخطفه من مكانه.
    /// </summary>
    private void SpawnPortal()
    {
        if (portalPrefab == null) return;

        Transform at = portalAt;
        if (at == null && enable != null)
            foreach (GameObject target in enable)
                if (target != null) { at = target.transform; break; }

        if (at == null)
        {
            Debug.LogWarning("[DelayedReveal] ما فيه مكان أضع فيه البوابة.", this);
            return;
        }

        GameObject portal = Instantiate(portalPrefab, at.position + portalOffset, at.rotation);
        portal.name = portalPrefab.name + " (بوابة)";
        portal.transform.localScale = portalPrefab.transform.localScale * portalScale;
        portal.SetActive(true);

        // زينة لا جسم: لا نريد لمؤثّر أن يدفع اللاعب أو يسدّ عليه الطريق
        foreach (var collider in portal.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
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
