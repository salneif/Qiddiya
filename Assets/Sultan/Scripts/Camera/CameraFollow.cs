using UnityEngine;
using System.Collections.Generic;

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

    private class ZoneSettings
    {
        public Object owner;
        public Vector3 offset;
        public Vector3 rotation;
        public float speed;
        public bool lockX;
        public float lockedX;
        public bool centerOnTarget;
        public bool followY;
        public bool freezeX;
        public bool freezeZ;
        public float frozenX;
        public float frozenZ;
    }

    private readonly List<ZoneSettings> _zones = new List<ZoneSettings>();

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
    private bool _freezeX;
    private bool _freezeZ;
    private float _frozenX;
    private float _frozenZ;
    private bool _followY;

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

        pruneDeadZones();
        updateRoom();

        if (_zones.Count == 0)
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

        if (_freezeX)
        {
            float tf = 1f - Mathf.Exp(-_blendSpeed * dt);
            _anchor.x = Mathf.Lerp(_anchor.x, _frozenX, tf);
        }
        else
        {
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
        }

        if (_followY)
        {
            float ty = 1f - Mathf.Exp(-yFollowSpeed * dt);
            _anchor.y = Mathf.Lerp(_anchor.y, target.position.y, ty);
        }

        if (followTargetZ)
        {
            float tz = 1f - Mathf.Exp(-zFollowSpeed * dt);
            if (_freezeZ)
                _anchor.z = Mathf.Lerp(_anchor.z, _frozenZ, tz);
            else
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

    public void SetOverride(Object owner, Vector3 overrideOffset, Vector3 overrideRotation, float speed, bool lockX = false, float lockedXPos = 0f, bool centerOnTarget = false, bool followPlayerY = false, bool freezeX = false, bool freezeZ = false)
    {
        ZoneSettings z = findZone(owner);
        if (z != null)
            _zones.Remove(z);
        else
            z = new ZoneSettings();

        z.owner = owner;
        z.offset = overrideOffset;
        z.rotation = overrideRotation;
        z.speed = speed;
        z.lockX = lockX;
        z.lockedX = lockedXPos;
        z.centerOnTarget = centerOnTarget;
        z.followY = followPlayerY;
        z.freezeX = freezeX;
        z.freezeZ = freezeZ;

        if (z.freezeX)
            z.frozenX = captureFreezeX(z);
        if (z.freezeZ)
            z.frozenZ = target != null ? target.position.z : _anchor.z;

        _zones.Add(z);
        applyTop();
    }

    public void ClearOverride(Object owner, float speed)
    {
        ZoneSettings z = findZone(owner);
        if (z == null) return;

        bool wasTop = _zones[_zones.Count - 1] == z;
        _zones.Remove(z);

        if (!wasTop) return;

        if (_zones.Count > 0)
            applyTop();
        else
            revertToBase(speed);
    }

    private ZoneSettings findZone(Object owner)
    {
        for (int i = 0; i < _zones.Count; i++)
            if (_zones[i].owner == owner) return _zones[i];
        return null;
    }

    private void applyTop()
    {
        ZoneSettings z = _zones[_zones.Count - 1];
        _goalOffset = _baseOffset + z.offset;
        _goalRotation = _baseRotation + z.rotation;
        _blendSpeed = z.speed;
        _lockX = z.lockX;
        _lockedX = z.lockedX;
        _centerOnTarget = z.centerOnTarget;
        _followY = z.followY;
        _freezeX = z.freezeX;
        _freezeZ = z.freezeZ;

        if (z.freezeX)
            _frozenX = z.frozenX;
        if (z.freezeZ)
            _frozenZ = z.frozenZ;
    }

    private float captureFreezeX(ZoneSettings z)
    {
        if (target == null) return _anchor.x;
        if (z.lockX) return z.lockedX;
        if (z.centerOnTarget) return target.position.x;

        if (clampX)
        {
            float halfWindow = Mathf.Max(0f, roomWidth * 0.5f - clampMargin);
            return Mathf.Clamp(target.position.x, _windowCenter - halfWindow, _windowCenter + halfWindow);
        }

        return target.position.x;
    }

    private void revertToBase(float speed)
    {
        _blendSpeed = speed;
        _lockX = false;
        _centerOnTarget = false;
        _freezeX = false;
        _freezeZ = false;
        _followY = followTargetY;
    }

    private void pruneDeadZones()
    {
        bool removedTop = false;
        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            if (_zones[i].owner != null) continue;
            if (i == _zones.Count - 1) removedTop = true;
            _zones.RemoveAt(i);
        }

        if (!removedTop) return;

        if (_zones.Count > 0)
            applyTop();
        else
            revertToBase(_blendSpeed);
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