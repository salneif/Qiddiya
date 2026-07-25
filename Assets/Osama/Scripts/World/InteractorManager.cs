using UnityEngine;

/// <summary>
/// مدير الـ Interactors — واحد فقط في المشهد.
///
/// يجمع كل <see cref="WorldInteractor"/> الموجودة (في Start و OnEnable)،
/// وفي LateUpdate يعبّئ بياناتها في مصفوفات ويرسلها كمتغيرات Global
/// لشيدر تغيير العالم:
///  _InteractorData     : xyz = الموقع، w = نصف القطر
///  _InteractorScale    : xyz = الحجم
///  _InteractorRotation : xyz = الدوران (درجات)
///  _InteractorTexIndex : رقم الشكل في الـ Texture 2D Array
///  _InteractorCount    : عدد الكائنات الفعّالة
///
/// ملاحظة: حجم المصفوفات ثابت (MAX_INTERACTORS = 100) لأن يونتي يثبّت حجم
/// مصفوفة الشيدر من أول إرسال — لا ترسل مصفوفة أصغر لاحقًا.
/// إذا أنشأت Interactor جديدًا وقت اللعب نادِ <see cref="Refresh"/>.
///
/// [ExecuteAlways] ضرورية: بدونها لا يعمل LateUpdate في وضع التحرير، فتبقى
/// بيانات الدوائر مجمّدة على آخر جلسة Play — وتحريك الكائن أو تغيير نصف قطره
/// لا يظهر له أي أثر في نافذة Scene.
/// </summary>
[ExecuteAlways]
public class InteractorManager : MonoBehaviour
{
    /// <summary>الحد الأقصى — يجب أن يطابق MAX_INTERACTORS في ملف الـ HLSL.</summary>
    public const int MaxInteractors = 100;

    private WorldInteractor[] interactors = new WorldInteractor[0];

    private readonly Vector4[] data = new Vector4[MaxInteractors];
    private readonly Vector4[] scales = new Vector4[MaxInteractors];
    private readonly Vector4[] rotations = new Vector4[MaxInteractors];
    private readonly float[] texIndices = new float[MaxInteractors];

    private static readonly int DataId = Shader.PropertyToID("_InteractorData");
    private static readonly int ScaleId = Shader.PropertyToID("_InteractorScale");
    private static readonly int RotationId = Shader.PropertyToID("_InteractorRotation");
    private static readonly int TexIndexId = Shader.PropertyToID("_InteractorTexIndex");
    private static readonly int CountId = Shader.PropertyToID("_InteractorCount");

    private void Start() => Refresh();

    private void OnEnable() => Refresh();

    private void OnDisable() => Shader.SetGlobalInteger(CountId, 0);

    /// <summary>يعيد جمع كل الـ Interactors — نادِها بعد إنشاء/حذف Interactor وقت اللعب.</summary>
    public void Refresh()
    {
        interactors = FindObjectsByType<WorldInteractor>(FindObjectsSortMode.None);
        if (interactors.Length > MaxInteractors)
            Debug.LogWarning($"[InteractorManager] عدد الـ Interactors ({interactors.Length}) " +
                             $"يتجاوز الحد الأقصى ({MaxInteractors}) — سيُتجاهل الزائد.");
    }

    private void LateUpdate()
    {
        // في وضع التحرير نعيد الجمع كل إطار حتى يظهر أي Interactor تضيفه أو تحذفه فورًا
        if (!Application.isPlaying) Refresh();

        int count = 0;

        for (int i = 0; i < interactors.Length && count < MaxInteractors; i++)
        {
            var it = interactors[i];
            if (it == null || !it.isActiveAndEnabled) continue;

            Vector3 p = it.transform.position;
            data[count] = new Vector4(p.x, p.y, p.z, it.Radius);
            scales[count] = it.Scale;
            rotations[count] = it.Rotation;
            texIndices[count] = it.TextureIndex;
            count++;
        }

        Shader.SetGlobalInteger(CountId, count);
        Shader.SetGlobalVectorArray(DataId, data);
        Shader.SetGlobalVectorArray(ScaleId, scales);
        Shader.SetGlobalVectorArray(RotationId, rotations);
        Shader.SetGlobalFloatArray(TexIndexId, texIndices);
    }
}
