// دالة Custom Function لشيدر تغيير العالم (WorldChange Shader Graph).
// تقرأ بيانات الـ Interactors المرسلة من InteractorManager كمتغيرات Global،
// وتحسب قناع (Mask) مجمّعًا لكل الكائنات: لكل Interactor تُدوَّر نقطة العالم
// حول محوره، تُقسم على الحجم لتصير UV، ثم يُقرأ شكل البصمة من Texture 2D Array.
//
// الاستخدام في Shader Graph: عقدة Custom Function → Type: File → هذا الملف،
// Name: CalculateInteractors — المدخلات: WorldPosition (Vector3)
// و TextureArray (Texture2DArray) — المخرج: OutMask (Float).
#ifndef OSAMA_INTERACTORS_INCLUDED
#define OSAMA_INTERACTORS_INCLUDED

#define MAX_INTERACTORS 100

// مصفوفات Global يرسلها InteractorManager (يجب تطابق الأسماء حرفيًا)
float4 _InteractorData[MAX_INTERACTORS];     // xyz = الموقع، w = نصف القطر
float4 _InteractorScale[MAX_INTERACTORS];    // xyz = الحجم
float4 _InteractorRotation[MAX_INTERACTORS]; // xyz = الدوران بالدرجات
float  _InteractorTexIndex[MAX_INTERACTORS]; // رقم الشكل في المصفوفة
int    _InteractorCount;                     // عدد الكائنات الفعّالة

// يدوّر نقطة حول المحاور الثلاثة (بترتيب Y ثم X ثم Z)
float3 RotateEulerDeg(float3 p, float3 eulerDeg)
{
    float3 r = radians(eulerDeg);
    float sx, cx; sincos(r.x, sx, cx);
    float sy, cy; sincos(r.y, sy, cy);
    float sz, cz; sincos(r.z, sz, cz);

    // حول محور Y
    p = float3(cy * p.x + sy * p.z, p.y, -sy * p.x + cy * p.z);
    // حول محور X
    p = float3(p.x, cx * p.y - sx * p.z, sx * p.y + cx * p.z);
    // حول محور Z
    p = float3(cz * p.x - sz * p.y, sz * p.x + cz * p.y, p.z);
    return p;
}

void CalculateInteractors_float(float3 WorldPosition, UnityTexture2DArray TextureArray,
                                out float OutMask)
{
    OutMask = 0.0;

    [loop]
    for (int i = 0; i < _InteractorCount; i++)
    {
        // الإزاحة عن مركز الكائن، مدوّرة بعكس دورانه (حتى تدور البصمة معه)
        float3 offset = WorldPosition - _InteractorData[i].xyz;
        offset = RotateEulerDeg(offset, -_InteractorRotation[i].xyz);

        // القسمة على الحجم × نصف القطر ثم +0.5 لتوسيط الصورة على الكائن
        float3 s = max(_InteractorScale[i].xyz, 0.0001) * max(_InteractorData[i].w, 0.0001);
        float2 uv = offset.xz / s.xz + 0.5;

        // تجاهل ما هو خارج حدود الصورة (0..1)
        float inside = step(0.0, uv.x) * step(uv.x, 1.0)
                     * step(0.0, uv.y) * step(uv.y, 1.0);

        // قراءة شكل البصمة من مصفوفة الصور (LOD 0 — آمنة داخل اللوب)
        float mask = SAMPLE_TEXTURE2D_ARRAY_LOD(TextureArray.tex, TextureArray.samplerstate,
                                                uv, _InteractorTexIndex[i], 0).r;

        OutMask += mask * inside;
    }

    OutMask = saturate(OutMask);
}

#endif
