using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private Vector3 cameraRotation = Vector3.zero;

    [SerializeField] private float roomWidth = 20f;
    [SerializeField] private float firstRoomCenterX = 0f;
    [SerializeField] private float boundaryPadding = 0.3f;
    [SerializeField] private float roomTransitionSpeed = 3f;

    [SerializeField] private float zFollowSpeed = 7f;

    [SerializeField] private bool anchorYFromTargetOnStart = true;
    [SerializeField] private float anchorY = 0f;
    [SerializeField] private float yFollowSpeed = 5f;

    private Vector3 _baseOffset;
    private Vector3 _baseRotation;
    private float _roomCenterX;
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

            _roomCenterX = roomCenterFromX(target.position.x);
            _anchor = new Vector3(_roomCenterX, anchorY, target.position.z);
        }

        _baseOffset = transform.position - _anchor;
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

        updateRoom();

        if (_overrideOwner == null)
        {
            _goalOffset = _baseOffset + offset;
            _goalRotation = _baseRotation + cameraRotation;
        }

        float dt = Time.deltaTime;

        float bt = 1f - Mathf.Exp(-_blendSpeed * dt);
        _activeOffset = Vector3.Lerp(_activeOffset, _goalOffset, bt);
        _activeRotation = Vector3.Lerp(_activeRotation, _goalRotation, bt);

        float tx = 1f - Mathf.Exp(-roomTransitionSpeed * dt);
        _anchor.x = Mathf.Lerp(_anchor.x, _roomCenterX, tx);

        float goalY = _followTargetY ? target.position.y : anchorY;
        float ty = 1f - Mathf.Exp(-yFollowSpeed * dt);
        _anchor.y = Mathf.Lerp(_anchor.y, goalY, ty);

        float tz = 1f - Mathf.Exp(-zFollowSpeed * dt);
        _anchor.z = Mathf.Lerp(_anchor.z, target.position.z, tz);

        transform.eulerAngles = _activeRotation;
        transform.position = _anchor + _activeOffset;
    }

    private void updateRoom()
    {
        float halfW = roomWidth * 0.5f;
        float x = target.position.x;

        if (x > _roomCenterX + halfW + boundaryPadding || x < _roomCenterX - halfW - boundaryPadding)
            _roomCenterX = roomCenterFromX(x);
    }

    private float roomCenterFromX(float x)
    {
        int index = Mathf.FloorToInt((x - firstRoomCenterX + roomWidth * 0.5f) / roomWidth);
        return firstRoomCenterX + index * roomWidth;
    }

    public void SetOverride(Object owner, Vector3 overrideOffset, Vector3 overrideRotation, float speed, bool followTargetY = false)
    {
        _overrideOwner = owner;
        _goalOffset = _baseOffset + overrideOffset;
        _goalRotation = _baseRotation + overrideRotation;
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

    private void OnDrawGizmosSelected()
    {
        float halfW = roomWidth * 0.5f;
        float y = Application.isPlaying ? _anchor.y : anchorY;
        float z = target != null ? target.position.z : 0f;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        for (int i = -4; i <= 5; i++)
        {
            float boundaryX = firstRoomCenterX - halfW + i * roomWidth;
            Gizmos.DrawLine(new Vector3(boundaryX, y, z - 6f), new Vector3(boundaryX, y, z + 6f));
        }

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
        float centerX = Application.isPlaying ? _roomCenterX : firstRoomCenterX;
        Gizmos.DrawLine(new Vector3(centerX, y, z - 6f), new Vector3(centerX, y, z + 6f));
    }
}