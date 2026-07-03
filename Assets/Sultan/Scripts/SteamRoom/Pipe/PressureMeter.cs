// using UnityEngine;

// public class PressureMeter : MonoBehaviour
// {
//     //Object references
//     [SerializeField] private Transform needle;
//     [SerializeField] private GameObject targetZoneHighlight;
//     [SerializeField] private float maxPressure = 120f;
//     [SerializeField] private float minAngle = -135f;
//     [SerializeField] private float maxAngle = 135f;
//     [SerializeField] private float springForce = 80f;
//     [SerializeField] private float damping = 12f;
//     [SerializeField] private float targetMin = 80f;
//     [SerializeField] private float targetMax = 90f;

//     private float _targetAngle;
//     private float _currentAngle;
//     private float _angularVelocity;
//     private float _currentPressure;

//     void Start()
//     {
//         _currentAngle = minAngle;
//         _targetAngle = minAngle;
//         applyNeedleRotation();

//         if (targetZoneHighlight != null)
//             targetZoneHighlight.SetActive(false);
//     }

//     void Update()
//     {
//         float dt = Time.deltaTime;
//         float force = (_targetAngle - _currentAngle) * springForce;
//         _angularVelocity += force * dt;
//         _angularVelocity *= Mathf.Exp(-damping * dt);
//         _currentAngle += _angularVelocity * dt;

//         applyNeedleRotation();
//         updateTargetHighlight();
//     }

//     public void SetPressure(float pressure)
//     {
//         _currentPressure = Mathf.Clamp(pressure, 0f, maxPressure);
//         float t = _currentPressure / maxPressure;
//         _targetAngle = Mathf.Lerp(minAngle, maxAngle, t);
//     }

//     void applyNeedleRotation()
//     {
//         if (needle == null) return;
//         needle.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
//     }

//     void updateTargetHighlight()
//     {
//         if (targetZoneHighlight == null) return;
//         bool inZone = _currentPressure >= targetMin && _currentPressure <= targetMax;
//         targetZoneHighlight.SetActive(inZone);
//     }

//     public float CurrentPressure => _currentPressure;
//     public bool InTargetZone => _currentPressure >= targetMin && _currentPressure <= targetMax;
// }


using UnityEngine;

public class PressureMeter : MonoBehaviour
{
    [SerializeField] private Transform needle;
    [SerializeField] private float maxPressure = 120f;
    [SerializeField] private float minAngle = -135f;
    [SerializeField] private float maxAngle = 135f;
    [SerializeField] private float springForce = 80f;
    [SerializeField] private float damping = 12f;
    [SerializeField] private float targetMin = 80f;
    [SerializeField] private float targetMax = 90f;

    private float _targetAngle;
    private float _currentAngle;
    private float _angularVelocity;
    private float _currentPressure;

    void Start()
    {
        _currentAngle = minAngle;
        _targetAngle = minAngle;
        applyNeedleRotation();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float force = (_targetAngle - _currentAngle) * springForce;
        _angularVelocity += force * dt;
        _angularVelocity *= Mathf.Exp(-damping * dt);
        _currentAngle += _angularVelocity * dt;

        applyNeedleRotation();
    }

    public void SetPressure(float pressure)
    {
        _currentPressure = Mathf.Clamp(pressure, 0f, maxPressure);
        float t = _currentPressure / maxPressure;
        _targetAngle = Mathf.Lerp(minAngle, maxAngle, t);
    }

    void applyNeedleRotation()
    {
        if (needle == null) return;
        needle.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
    }

    public float CurrentPressure => _currentPressure;
    public bool InTargetZone => _currentPressure >= targetMin && _currentPressure <= targetMax;
}