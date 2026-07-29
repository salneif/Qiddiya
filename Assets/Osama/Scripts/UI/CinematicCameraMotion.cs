using UnityEngine;

/// <summary>
/// حركة كاميرا سينمائية لخلفية القائمة الرئيسية: تمايل ناعم عضوي (Perlin) في
/// الموضع والدوران يعطي إحساسًا "حيًّا" للمشهد، مع دوران بطيء اختياري حول محور.
/// حُط هذا السكربت على كاميرا مشهد القائمة.
/// </summary>
public class CinematicCameraMotion : MonoBehaviour
{
    [Header("تمايل الموضع")]
    [Tooltip("مقدار تمايل الموضع (متر)")]
    [SerializeField] private float positionSwayAmount = 0.35f;
    [Tooltip("سرعة تمايل الموضع")]
    [SerializeField] private float positionSwaySpeed = 0.25f;

    [Header("تمايل الدوران")]
    [Tooltip("مقدار تمايل الدوران (درجات)")]
    [SerializeField] private float rotationSwayAmount = 1.2f;
    [Tooltip("سرعة تمايل الدوران")]
    [SerializeField] private float rotationSwaySpeed = 0.2f;

    [Header("دوران بطيء حول محور (اختياري)")]
    [Tooltip("نقطة الدوران — إذا حُدّدت تدور الكاميرا حولها ببطء وتظل تنظر إليها")]
    [SerializeField] private Transform orbitPivot;
    [Tooltip("سرعة الدوران (درجات/ثانية)")]
    [SerializeField] private float orbitSpeed = 1.5f;

    private Vector3 startPos;
    private Quaternion startRot;
    private Vector3 pivotOffset;
    private float orbitAngle;
    private float sx, sy, sz, rx, ry;

    private void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        if (orbitPivot != null)
            pivotOffset = transform.position - orbitPivot.position;

        sx = Random.value * 100f;
        sy = Random.value * 100f;
        sz = Random.value * 100f;
        rx = Random.value * 100f;
        ry = Random.value * 100f;
    }

    private void Update()
    {
        float t = Time.time;

        // الوضع/الدوران الأساسي (مع الدوران البطيء إن وُجد)
        Vector3 basePos;
        Quaternion baseRot;
        if (orbitPivot != null)
        {
            orbitAngle += orbitSpeed * Time.deltaTime;
            Vector3 off = Quaternion.AngleAxis(orbitAngle, Vector3.up) * pivotOffset;
            basePos = orbitPivot.position + off;
            baseRot = Quaternion.LookRotation(orbitPivot.position - basePos, Vector3.up);
        }
        else
        {
            basePos = startPos;
            baseRot = startRot;
        }

        // تمايل عضوي عبر Perlin (يتراوح حول الصفر)
        Vector3 swayPos = new Vector3(
            Mathf.PerlinNoise(sx, t * positionSwaySpeed) - 0.5f,
            Mathf.PerlinNoise(sy, t * positionSwaySpeed) - 0.5f,
            Mathf.PerlinNoise(sz, t * positionSwaySpeed) - 0.5f) * (positionSwayAmount * 2f);

        Vector3 swayRot = new Vector3(
            Mathf.PerlinNoise(rx, t * rotationSwaySpeed) - 0.5f,
            Mathf.PerlinNoise(ry, t * rotationSwaySpeed) - 0.5f,
            0f) * (rotationSwayAmount * 2f);

        // التمايل يُطبّق في فضاء الكاميرا المحلي فيبدو طبيعيًا
        transform.position = basePos + baseRot * swayPos;
        transform.rotation = baseRot * Quaternion.Euler(swayRot);
    }
}
