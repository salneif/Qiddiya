using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private Vector3 cameraRotation = Vector3.zero;
    [SerializeField] private float xFollowSpeed = 7f;
    [SerializeField] private bool clampX = true;
    [SerializeField] private float roomWidth = 20f;
    [SerializeField] private float firstRoomCenterX = 0f;
    [SerializeField] private float boundaryPadding = 0.3f;
    [SerializeField] private float roomTransitionSpeed = 3f;
    [SerializeField] private float clampMargin = 4f;
    [SerializeField] private bool followTargetY = true;
    [SerializeField] private float yFollowSpeed = 5f;
    [SerializeField] private bool followTargetZ = false;
    [SerializeField] private float zFollowSpeed = 7f;

    private float _roomCenterX;
    private float _windowCenter;
    private Vector3 _baseOffset;
    private Vector3 _baseRotation;
    private Vector3 _anchor;
    private Vector3 _activeOffset;
    private Vector3 _activeRotation;
    private Vector3 _goalOffset;
    private Vector3 _goalRotation;
    private float _blendSpeed = 5f;
    private bool _lockX;
    private float _lockedX;
    private bool _centerOnTarget;
    private bool _followY;
    private Object _overrideOwner;

    private void Start()
    {
        _followY = followTargetY;

        _roomCenterX = firstRoomCenterX;

        if (target != null)
            _roomCenterX = roomCenterFromX(target.position.x);

        _windowCenter = _roomCenterX;

        if (target != null)
        {
            float halfWindow = Mathf.Max(0f, roomWidth * 0.5f - clampMargin);
            float startX = clampX
                ? Mathf.Clamp(target.position.x, _windowCenter - halfWindow, _windowCenter + halfWindow)
                : target.position.x;
            _anchor = new Vector3(startX, target.position.y, target.position.z);
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

        updateRoom();

        if (_overrideOwner == null)
        {
            _goalOffset = _baseOffset + offset;
            _goalRotation = _baseRotation + cameraRotation;
            _followY = followTargetY;
        }

        float dt = Time.deltaTime;

        float bt = 1f - Mathf.Exp(-_blendSpeed * dt);
        _activeOffset = Vector3.Slerp(_activeOffset, _goalOffset, bt);
        _activeRotation = Vector3.Lerp(_activeRotation, _goalRotation, bt);

        float halfWindow = Mathf.Max(0f, roomWidth * 0.5f - clampMargin);

        if (clampX)
        {
            float tw = 1f - Mathf.Exp(-roomTransitionSpeed * dt);
            _windowCenter = Mathf.Lerp(_windowCenter, _roomCenterX, tw);
        }

        float goalX;
        if (_lockX)
            goalX = _lockedX;
        else if (_centerOnTarget)
            goalX = target.position.x;
        else if (clampX)
            goalX = Mathf.Clamp(target.position.x, _windowCenter - halfWindow, _windowCenter + halfWindow);
        else
            goalX = target.position.x;

        float tx = 1f - Mathf.Exp(-xFollowSpeed * dt);
        _anchor.x = Mathf.Lerp(_anchor.x, goalX, tx);

        if (_followY)
        {
            float ty = 1f - Mathf.Exp(-yFollowSpeed * dt);
            _anchor.y = Mathf.Lerp(_anchor.y, target.position.y, ty);
        }

        if (followTargetZ)
        {
            float tz = 1f - Mathf.Exp(-zFollowSpeed * dt);
            _anchor.z = Mathf.Lerp(_anchor.z, target.position.z, tz);
        }

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

    public void SetOverride(Object owner, Vector3 overrideOffset, Vector3 overrideRotation, float speed, bool lockX = false, float lockedXPos = 0f, bool centerOnTarget = false, bool followPlayerY = false)
    {
        _overrideOwner = owner;
        _goalOffset = _baseOffset + overrideOffset;
        _goalRotation = _baseRotation + overrideRotation;
        _blendSpeed = speed;
        _lockX = lockX;
        _lockedX = lockedXPos;
        _centerOnTarget = centerOnTarget;
        _followY = followPlayerY;
    }

    public void ClearOverride(Object owner, float speed)
    {
        if (_overrideOwner != owner) return;

        _overrideOwner = null;
        _blendSpeed = speed;
        _lockX = false;
        _centerOnTarget = false;
        _followY = followTargetY;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void OnDrawGizmosSelected()
    {
        float y = target != null ? target.position.y : 0f;
        float z = target != null ? target.position.z : 0f;
        float halfW = roomWidth * 0.5f;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        for (int i = -4; i <= 5; i++)
        {
            float boundaryX = firstRoomCenterX - halfW + i * roomWidth;
            Gizmos.DrawLine(new Vector3(boundaryX, y - 3f, z), new Vector3(boundaryX, y + 3f, z));
        }

        if (!clampX) return;

        float halfWindow = Mathf.Max(0f, halfW - clampMargin);
        float center = Application.isPlaying ? _windowCenter : firstRoomCenterX;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
        Gizmos.DrawLine(new Vector3(center - halfWindow, y - 3f, z), new Vector3(center - halfWindow, y + 3f, z));
        Gizmos.DrawLine(new Vector3(center + halfWindow, y - 3f, z), new Vector3(center + halfWindow, y + 3f, z));
    }
}