using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// مدير لغز الأعمدة الدوّارة (بأسلوب سكايرم):
/// يفحص بعد كل دورة هل كل عمود على وجهه الصحيح، وعند تطابق التركيبة كاملة
/// يطلق <see cref="onSolved"/> (اربطه بفتح الباب) ويقفل الأعمدة والأزرار.
/// </summary>
public class RotaryPuzzle : MonoBehaviour
{
    [Header("الأعمدة")]
    [Tooltip("أعمدة اللغز بالترتيب")]
    [SerializeField] private RotaryPillar[] pillars;

    [Tooltip("التركيبة الصحيحة — لكل عمود رقم وجهه الصحيح (0..2). " +
             "يجب أن يساوي طولها عدد الأعمدة")]
    [SerializeField] private int[] solution;

    [Header("بعد الحل")]
    [Tooltip("قفل الأعمدة والأزرار بعد الحل (ما عاد تدور)")]
    [SerializeField] private bool lockWhenSolved = true;
    [Tooltip("أزرار اللغز — تُقفل مع الحل (اختياري)")]
    [SerializeField] private PuzzleButton[] buttons;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت نجاح اللغز (قرقعة حجر/جرس)")]
    [SerializeField] private AudioClip solvedSound;

    [Header("أحداث")]
    [Tooltip("عند الحل — اربطه بـ Gate.Open لفتح الباب")]
    public UnityEvent onSolved;

    [Header("تشخيص")]
    [Tooltip("يطبع أوجه الأعمدة عند كل فحص. " +
             "لو لففت عمودًا وما طُبع شيء = On Rotated في ذاك العمود غير مربوط " +
             "بـ RotaryPuzzle.CheckSolution — وهذا يعني أن اللغز لن يُحلّ أبدًا.")]
    [SerializeField] private bool debugLog = false;

    /// <summary>هل حُلّ اللغز؟</summary>
    public bool IsSolved { get; private set; }

    /// <summary>
    /// يفحص التركيبة الحالية — اربطه بحدث onRotated في كل عمود.
    /// </summary>
    public void CheckSolution()
    {
        if (IsSolved || pillars == null || solution == null) return;

        if (solution.Length != pillars.Length)
        {
            Debug.LogWarning("[RotaryPuzzle] طول التركيبة لا يساوي عدد الأعمدة!");
            return;
        }

        if (debugLog)
        {
            var now = new System.Text.StringBuilder();
            var want = new System.Text.StringBuilder();
            for (int i = 0; i < pillars.Length; i++)
            {
                if (i > 0) { now.Append(','); want.Append(','); }
                now.Append(pillars[i] == null ? "؟" : pillars[i].CurrentFace.ToString());
                want.Append(solution[i]);
            }
            Debug.Log($"[RotaryPuzzle] {name}: الأوجه الآن [{now}] — المطلوب [{want}]", this);
        }

        // لو أي عمود على وجه غلط → اللغز غير محلول بعد
        for (int i = 0; i < pillars.Length; i++)
        {
            if (pillars[i] == null || pillars[i].CurrentFace != solution[i])
                return;
        }

        Solve();
    }

    /// <summary>
    /// اختبار: كليك يمين على اسم المكوّن أثناء اللعب → يحلّ اللغز فورًا.
    /// لفحص ما بعد الحل (الانتقال، الباب) بلا لفّ الأعمدة.
    /// </summary>
    [ContextMenu("تجربة: حُلّ اللغز الآن")]
    private void DebugSolve()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RotaryPuzzle] التجربة تعمل أثناء Play فقط.", this);
            return;
        }
        if (!IsSolved) Solve();
    }

    /// <summary>
    /// يعيد اللغز لحالته الأولى: يفك الحل، ويرجّع كل عمود لوجه بدايته، ويفتح الأزرار.
    /// اربطه بـ PlayerKillable.onRespawn ليُمحى تقدّم اللغز عند الموت.
    /// </summary>
    public void ResetPuzzle()
    {
        IsSolved = false;

        if (pillars != null)
            foreach (var p in pillars)
                if (p != null) p.ResetToStart();

        if (buttons != null)
            foreach (var b in buttons)
                if (b != null) b.Locked = false;
    }

    private void Solve()
    {
        IsSolved = true;
        if (debugLog) Debug.Log($"[RotaryPuzzle] {name}: انحلّ — On Solved ينطلق الآن.", this);

        if (solvedSound != null && audioSource != null)
            audioSource.PlayOneShot(solvedSound);

        if (lockWhenSolved)
        {
            foreach (var p in pillars)
                if (p != null) p.Locked = true;
            if (buttons != null)
                foreach (var b in buttons)
                    if (b != null) b.Locked = true;
        }

        onSolved?.Invoke();
    }
}
