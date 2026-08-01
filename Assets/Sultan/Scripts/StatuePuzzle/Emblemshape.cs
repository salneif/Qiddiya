using UnityEngine;

public class EmblemShape : MonoBehaviour
{
    [SerializeField] private StatuePedestal.Animal boundAnimal = StatuePedestal.Animal.Chicken;
    [SerializeField] private float requiredValue = 25f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float springForce = 60f;
    [SerializeField] private float damping = 14f;
    [SerializeField] private float settleAngleThreshold = 0.5f;
    [SerializeField] private float settleSpeedThreshold = 2f;

    private Quaternion _solvedRotation;
    private float _currentAngle;
    private float _targetAngle;
    private float _angularVelocity;

    void Awake()
    {
        _solvedRotation = transform.localRotation;

        _currentAngle = -requiredValue;
        _targetAngle = -requiredValue;
        _angularVelocity = 0f;
        applyRotation();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float force = (_targetAngle - _currentAngle) * springForce;
        _angularVelocity += force * dt;
        _angularVelocity *= Mathf.Exp(-damping * dt);
        _currentAngle += _angularVelocity * dt;

        applyRotation();
    }

    public void SetHost(float hostValue, bool placed)
    {
        _targetAngle = placed ? hostValue - requiredValue : -requiredValue;
    }

    void applyRotation()
    {
        transform.localRotation = Quaternion.AngleAxis(_currentAngle, rotationAxis) * _solvedRotation;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);

        Vector3 axis = transform.parent != null
            ? transform.parent.TransformDirection(rotationAxis.normalized)
            : rotationAxis.normalized;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        Gizmos.DrawLine(transform.position - axis, transform.position + axis);
    }

    public StatuePedestal.Animal BoundAnimal => boundAnimal;
    public float RequiredValue => requiredValue;
    public float CurrentAngle => _currentAngle;

    public bool IsSettled =>
        Mathf.Abs(_targetAngle - _currentAngle) <= settleAngleThreshold &&
        Mathf.Abs(_angularVelocity) <= settleSpeedThreshold;
}