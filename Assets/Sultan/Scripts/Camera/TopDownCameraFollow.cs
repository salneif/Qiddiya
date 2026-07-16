using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private Vector3 cameraRotation = Vector3.zero;
    [SerializeField] private float xFollowSpeed = 7f;
    [SerializeField] private float zFollowSpeed = 7f;
    [SerializeField] private bool anchorYFromTargetOnStart = true;
    [SerializeField] private float anchorY = 0f;
    [SerializeField] private float yFollowSpeed = 5f;

    private Vector3 _baseOffset;
    private Vector3 _baseRotation;
    private Vector3 _anchor;
    private Vector3 _activeOffset;
    private Vector3 _activeRotation;
    private Vector3 _goalOffset;
    private Vector3 _goalRotation;
    private float _blendSpeed = 5f;
    private bool _followTargetY;
    private Object _overrideOwner;

    private void Start()
    {
        if (target != null)
        {
            if (anchorYFromTargetOnStart)
                anchorY = target.position.y;

            _anchor = new Vector3(target.position.x, anchorY, target.position.z);
        }

        _baseOffset = transform.position - _anchor;

        _baseOffset.x = 0f;

        _baseRotation = transform.eulerAngles;

        _activeOffset = _baseOffset + offset;
        _activeRotation = _baseRotation + cameraRotation;
        _goalOffset = _activeOffset;
        _goalRotation = _activeRotation;

        transform.eulerAngles = _activeRotation;
        if (target != null)
            transform.position = _anchor + _activeOffset;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (_overrideOwner == null)
        {
            _goalOffset = _baseOffset + offset;
            _goalRotation = _baseRotation + cameraRotation;
        }

        float dt = Time.deltaTime;

        float bt = 1f - Mathf.Exp(-_blendSpeed * dt);
        _activeOffset = Vector3.Slerp(_activeOffset, _goalOffset, bt);
        _activeRotation = Vector3.Lerp(_activeRotation, _goalRotation, bt);

        float tx = 1f - Mathf.Exp(-xFollowSpeed * dt);
        _anchor.x = Mathf.Lerp(_anchor.x, target.position.x, tx);

        float goalY = _followTargetY ? target.position.y : anchorY;
        float ty = 1f - Mathf.Exp(-yFollowSpeed * dt);
        _anchor.y = Mathf.Lerp(_anchor.y, goalY, ty);

        float tz = 1f - Mathf.Exp(-zFollowSpeed * dt);
        _anchor.z = Mathf.Lerp(_anchor.z, target.position.z, tz);

        transform.eulerAngles = _activeRotation;
        transform.position = _anchor + _activeOffset;
    }

    public void SetOverride(Object owner, Vector3 overrideOffset, Vector3 overrideRotation, float speed, bool followTargetY = false, float cameraYaw = 0f)
    {
        _overrideOwner = owner;

        Quaternion turn = Quaternion.Euler(0f, cameraYaw, 0f);
        _goalOffset = turn * (_baseOffset + overrideOffset);
        _goalRotation = _baseRotation + new Vector3(0f, cameraYaw, 0f) + overrideRotation;
        _blendSpeed = speed;
        _followTargetY = followTargetY;
    }

    public void ClearOverride(Object owner, float speed)
    {
        if (_overrideOwner != owner) return;

        if (_followTargetY && target != null)
            anchorY = target.position.y;

        _overrideOwner = null;
        _blendSpeed = speed;
        _followTargetY = false;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public float HeadingYaw => _goalRotation.y;

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Vector3 a = Application.isPlaying ? _anchor : target.position;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(a, 0.4f);
        Gizmos.DrawLine(transform.position, a);
    }
}