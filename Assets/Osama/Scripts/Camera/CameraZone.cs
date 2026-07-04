using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// منطقة كاميرا بأسلوب Little Nightmares: عند دخول اللاعب هذه المنطقة تُرفع أولوية
/// كاميرا Cinemachine الخاصة بها، فتنتقل الكاميرا الحيّة إليها بنعومة (blend).
/// حُط هذا السكربت على GameObject فيه Collider (Is Trigger) يغطّي حدود المنطقة،
/// واربطه بكاميرا Cinemachine ثابتة تؤطّر تلك المنطقة.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CameraZone : MonoBehaviour
{
    [Tooltip("كاميرا Cinemachine الثابتة الخاصة بهذه المنطقة")]
    [SerializeField] private CinemachineCamera zoneCamera;

    [Tooltip("أولوية الكاميرا عندما يكون اللاعب داخل المنطقة (الأعلى يفوز)")]
    [SerializeField] private int activePriority = 20;

    [Tooltip("أولوية الكاميرا عندما يكون اللاعب خارج المنطقة")]
    [SerializeField] private int inactivePriority = 0;

    [Tooltip("وسم كائن اللاعب")]
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        // يضمن أن الكولايدر يشتغل كـ Trigger عند إضافة السكربت لأول مرة
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (zoneCamera != null)
            zoneCamera.Priority = inactivePriority;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (zoneCamera == null || !other.CompareTag(playerTag)) return;
        zoneCamera.Priority = activePriority;
    }

    private void OnTriggerExit(Collider other)
    {
        if (zoneCamera == null || !other.CompareTag(playerTag)) return;
        zoneCamera.Priority = inactivePriority;
    }
}
