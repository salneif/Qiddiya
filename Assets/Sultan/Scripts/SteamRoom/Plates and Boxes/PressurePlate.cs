using UnityEngine;
using System;
using System.Collections.Generic;

public class PressurePlate : MonoBehaviour
{
    public enum PlateID { A, B, C, D }

    [SerializeField] private PlateID id;
    [SerializeField] private float pressureValue;
    [SerializeField] private float depressDepth = 0.05f;
    [SerializeField] private Transform plateTop;
    [SerializeField] private GameObject indicator;

    public event Action<PressurePlate> OnPlatePressed;
    public event Action<PressurePlate> OnPlateReleased;

    private HashSet<Collider> _occupants = new HashSet<Collider>();
    private bool _isPressed;
    private Vector3 _topRestPos;

    void Start()
    {
        if (plateTop != null)
            _topRestPos = plateTop.localPosition;
        if (indicator != null)
            indicator.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        _occupants.Add(other);
        refresh();
    }

    void OnTriggerExit(Collider other)
    {
        _occupants.Remove(other);
        refresh();
    }

    void refresh()
    {
        _occupants.RemoveWhere(c => c == null);
        bool pressed = _occupants.Count > 0;
        if (pressed == _isPressed) return;
        _isPressed = pressed;

        if (plateTop != null)
            plateTop.localPosition = _topRestPos + (_isPressed ? Vector3.down * depressDepth : Vector3.zero);
        if (indicator != null)
            indicator.SetActive(_isPressed);

        if (_isPressed) OnPlatePressed?.Invoke(this);
        else OnPlateReleased?.Invoke(this);
    }

    public PlateID ID => id;
    public float PressureValue => pressureValue;
    public bool IsPressed => _isPressed;

    public bool HasBox
    {
        get
        {
            foreach (var c in _occupants)
                if (c != null && c.GetComponent<Box>() != null) return true;
            return false;
        }
    }
}