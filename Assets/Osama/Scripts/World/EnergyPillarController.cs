using UnityEngine;

/// <summary>
/// متحكم عمود الطاقة — يحرّك مستوى التعبئة (اللون يرقى وينزل) في شيدر
/// Osama/EnergyPillar، ويطفّئه فيصير أسود بنعومة.
///
/// حُطّه على المجسم (اللي عليه ماتيريال EnergyPillar) واربط TurnOn/TurnOff/Toggle
/// بأي حدث (رافعة، لمبة، علم...). يستخدم MaterialPropertyBlock فلا يعدّل
/// ملف الماتيريال وتقدر تستخدم نفس الماتيريال لعدة أعمدة.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class EnergyPillarController : MonoBehaviour
{
    [Header("الحالة")]
    [Tooltip("يبدأ شغّالًا؟")]
    [SerializeField] private bool startOn = true;
    [Tooltip("مدة الاشتعال/الانطفاء التدريجي (ثواني)")]
    [SerializeField] private float onOffFadeTime = 0.6f;

    [Header("حركة التعبئة (يرقى وينزل)")]
    [Tooltip("أدنى مستوى تنزل له التعبئة (0..1)")]
    [Range(0f, 1f)] [SerializeField] private float minFill = 0.15f;
    [Tooltip("أعلى مستوى ترقى له التعبئة (0..1)")]
    [Range(0f, 1f)] [SerializeField] private float maxFill = 0.9f;
    [Tooltip("سرعة الصعود والنزول")]
    [SerializeField] private float waveSpeed = 0.5f;

    private static readonly int FillId = Shader.PropertyToID("_Fill");
    private static readonly int OnId = Shader.PropertyToID("_On");

    private Renderer rend;
    private MaterialPropertyBlock block;
    private float onAmount;   // 0 مطفي → 1 شغّال
    private float onTarget;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        onAmount = onTarget = startOn ? 1f : 0f;
    }

    private void Update()
    {
        // اشتعال/انطفاء ناعم
        if (!Mathf.Approximately(onAmount, onTarget))
            onAmount = Mathf.MoveTowards(onAmount, onTarget,
                Time.deltaTime / Mathf.Max(onOffFadeTime, 0.0001f));

        // التعبئة ترقى وتنزل (ping-pong) وهو شغّال
        float span = Mathf.Max(maxFill - minFill, 0.0001f);
        float fill = minFill + Mathf.PingPong(Time.time * waveSpeed * span, span);

        rend.GetPropertyBlock(block);
        block.SetFloat(FillId, fill);
        block.SetFloat(OnId, onAmount);
        rend.SetPropertyBlock(block);
    }

    /// <summary>يشغّل العمود (اللون يرجع ويتحرك).</summary>
    public void TurnOn() => onTarget = 1f;

    /// <summary>يطفّئ العمود (يذوب إلى الأسود).</summary>
    public void TurnOff() => onTarget = 0f;

    /// <summary>يبدّل الحالة.</summary>
    public void Toggle() => onTarget = onTarget > 0.5f ? 0f : 1f;

    /// <summary>ضبط الحالة من حدث بمعامل bool.</summary>
    public void SetOn(bool on) => onTarget = on ? 1f : 0f;
}
