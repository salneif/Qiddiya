using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// عمود دوّار بعدة وجوه (لغز سكايرم): كل استدعاء لـ <see cref="Rotate"/>
/// يدوّره وجهًا واحدًا (360 ÷ عدد الوجوه) بدوران ناعم.
/// يتذكر وجهه الحالي في <see cref="CurrentFace"/> ليفحصه مدير اللغز.
/// </summary>
public class RotaryPillar : MonoBehaviour
{
    [Header("الوجوه")]
    [Tooltip("عدد وجوه العمود (3 = يدور 120 درجة كل ضغطة)")]
    [SerializeField] private int faceCount = 3;
    [Tooltip("الوجه الذي يبدأ عليه (0 حتى عدد الوجوه-1)")]
    [SerializeField] private int startFace = 0;
    [Tooltip("أسماء الوجوه بالترتيب (ترس / فطر / سرك) — للتوضيح فقط، تظهر فوق العمود " +
             "في نافذة Scene أثناء اللعب فتعرف أي رقم يقابل أي صورة بلا تخمين")]
    [SerializeField] private string[] faceNames;

    [Header("عرض الوجوه (بديل للدوران)")]
    [Tooltip("كائن لكل وجه بالترتيب — يظهر واحد فقط حسب الوجه الحالي. " +
             "استخدمه للوحات المسطّحة التي لا يصلح تدويرها فعليًا. " +
             "اتركه فارغًا ليدور المجسّم كالمعتاد.")]
    [SerializeField] private GameObject[] faceObjects;

    [Header("الدوران")]
    [Tooltip("محور الدوران المحلي (عادة Y للعمود الواقف)")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [Tooltip("مدة دورة الوجه الواحد (ثواني)")]
    [SerializeField] private float rotateTime = 0.5f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت احتكاك حجري أثناء الدوران")]
    [SerializeField] private AudioClip rotateSound;

    [Header("أحداث")]
    [Tooltip("بعد اكتمال كل دورة — اربطه بـ RotaryPuzzle.CheckSolution")]
    public UnityEvent onRotated;

    /// <summary>الوجه المعروض حاليًا (0..faceCount-1).</summary>
    public int CurrentFace { get; private set; }

    /// <summary>هل هو يدور الآن؟ (يمنع ضغطات متراكبة)</summary>
    public bool IsRotating { get; private set; }

    /// <summary>قفل العمود (يستخدمه اللغز بعد الحل).</summary>
    public bool Locked { get; set; }

    private Quaternion baseRotation;

    private void Awake()
    {
        faceCount = Mathf.Max(2, faceCount);
        baseRotation = transform.localRotation;
        CurrentFace = ((startFace % faceCount) + faceCount) % faceCount;

        if (HasFaceObjects) ShowFace(CurrentFace);
        else transform.localRotation = RotationForFace(CurrentFace);
    }

    /// <summary>هل يعرض وجوهه بتبديل الكائنات بدل الدوران؟</summary>
    private bool HasFaceObjects => faceObjects != null && faceObjects.Length > 0;

    /// <summary>يُظهر كائن الوجه المطلوب ويخفي البقية.</summary>
    private void ShowFace(int face)
    {
        for (int i = 0; i < faceObjects.Length; i++)
            if (faceObjects[i] != null) faceObjects[i].SetActive(i == face);
    }

    /// <summary>يدوّر العمود وجهًا واحدًا — اربطه بحدث الزر.</summary>
    public void Rotate()
    {
        if (IsRotating || Locked) return;
        StartCoroutine(RotateStep());
    }

    private IEnumerator RotateStep()
    {
        IsRotating = true;

        if (rotateSound != null && audioSource != null)
            audioSource.PlayOneShot(rotateSound);

        int fromFace = CurrentFace;
        CurrentFace = (CurrentFace + 1) % faceCount;

        if (HasFaceObjects)
        {
            // لوحة مسطّحة: نبدّل الصورة ونحترم نفس المدة ليبقى إيقاع اللغز واحدًا
            ShowFace(CurrentFace);
            if (rotateTime > 0f) yield return new WaitForSeconds(rotateTime);
        }
        else
        {
            Quaternion from = RotationForFace(fromFace);
            Quaternion to = RotationForFace(fromFace + 1); // بدون % حتى يدور للأمام دائمًا

            float t = 0f;
            while (t < rotateTime)
            {
                t += Time.deltaTime;
                float k = curve.Evaluate(rotateTime > 0f ? Mathf.Clamp01(t / rotateTime) : 1f);
                transform.localRotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }
            transform.localRotation = RotationForFace(CurrentFace);
        }

        IsRotating = false;
        onRotated?.Invoke();
    }

#if UNITY_EDITOR
    /// <summary>
    /// يكتب الوجه الحالي فوق العمود في نافذة Scene أثناء اللعب.
    /// اضغط الدوّاسة وشوف الاسم يتغيّر — فتعرف أي رقم يقابل أي صورة مباشرة
    /// بدل ما تحسبها من زوايا الدوران.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        string label = (faceNames != null && CurrentFace < faceNames.Length &&
                        !string.IsNullOrEmpty(faceNames[CurrentFace]))
            ? $"{CurrentFace} — {faceNames[CurrentFace]}"
            : $"وجه {CurrentFace}";

        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, label);
    }
#endif

    /// <summary>زاوية الدوران المقابلة لوجه معيّن.</summary>
    private Quaternion RotationForFace(int face)
    {
        float step = 360f / faceCount;
        return baseRotation * Quaternion.AngleAxis(step * face, rotationAxis.normalized);
    }
}
