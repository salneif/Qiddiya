using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// تريغر بوكس: عند دخول اللاعب هذه المنطقة يتغيّر العالم (يبدّل حالة اللمبة).
/// حُط هذا السكربت على GameObject فيه Collider مفعّل عليه Is Trigger يغطّي المنطقة.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WorldChangeTrigger : MonoBehaviour
{
    private enum TriggerAction { TurnOn, TurnOff, Toggle }

    [Header("الهدف")]
    [Tooltip("اللمبة/المتحكّم الذي سيُبدّل العالم")]
    [SerializeField] private LampSwitch lampSwitch;

    [Tooltip("ماذا يحدث عند الدخول")]
    [SerializeField] private TriggerAction action = TriggerAction.TurnOn;

    [Header("الشروط")]
    [Tooltip("وسم كائن اللاعب")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("يعمل مرة واحدة فقط ثم يتعطّل")]
    [SerializeField] private bool triggerOnce = true;

    [Header("أحداث إضافية")]
    [Tooltip("يُستدعى عند تفعيل التريغر (لأي مؤثرات إضافية)")]
    [SerializeField] private UnityEvent onTriggered;

    private bool used;

    private void Reset()
    {
        // يضمن أن الكولايدر تريغر عند إضافة السكربت لأول مرة
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used && triggerOnce) return;
        if (!other.CompareTag(playerTag)) return;

        used = true;

        if (lampSwitch != null)
        {
            switch (action)
            {
                case TriggerAction.TurnOn: lampSwitch.SetLamp(true); break;
                case TriggerAction.TurnOff: lampSwitch.SetLamp(false); break;
                case TriggerAction.Toggle: lampSwitch.Toggle(); break;
            }
        }

        onTriggered?.Invoke();
    }
}
