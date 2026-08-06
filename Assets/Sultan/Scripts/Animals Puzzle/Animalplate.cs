using UnityEngine;

public class AnimalPlate : MonoBehaviour
{
    public enum AnimalId { Wolf, Elephant, Chicken }

    [SerializeField] private AnimalId id;
    [SerializeField] private int startSlot;

    private int _slotIndex = -1;
    private Vector3 _from;
    private Vector3 _to;
    private Vector3 _travelOffset;
    private float _duration;
    private float _t;
    private bool _moving;

    void Update()
    {
        if (!_moving) return;

        _t = Mathf.Clamp01(_t + Time.deltaTime / _duration);
        float eased = Mathf.SmoothStep(0f, 1f, _t);

        transform.position = Vector3.Lerp(_from, _to, eased) + _travelOffset * Mathf.Sin(eased * Mathf.PI);

        if (_t < 1f) return;

        transform.position = _to;
        _moving = false;
    }

    public void SnapTo(Transform slot, int slotIndex)
    {
        _moving = false;
        _t = 0f;
        transform.position = slot.position;
        _slotIndex = slotIndex;
    }

    public void MoveTo(Transform slot, int slotIndex, float duration, Vector3 travelOffset)
    {
        _from = transform.position;
        _to = slot.position;
        _travelOffset = travelOffset;
        _duration = Mathf.Max(0.01f, duration);
        _t = 0f;
        _moving = true;
        _slotIndex = slotIndex;
    }

    public AnimalId Id => id;
    public int StartSlot => startSlot;
    public int SlotIndex => _slotIndex;
    public bool IsMoving => _moving;
}