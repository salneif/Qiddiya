using UnityEngine;
using System;

public class Lever : MonoBehaviour
{
    public enum LeverID { A, B, C, D }

    [SerializeField] private LeverID id;
    [SerializeField] private float pressureValue;
    [SerializeField] private Transform handle;
    [SerializeField] private GameObject indicator;
    [SerializeField] private float onAngle = -45f;
    [SerializeField] private float offAngle = 45f;
    [SerializeField] private float animSpeed = 8f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    public event Action<Lever> OnLeverActivated;
    public event Action<Lever> OnLeverDeactivated;

    private bool _isOn;
    private bool _playerInRange;
    private float _currentAngle;

    void Start()
    {
        _currentAngle = offAngle;
        applyRotation();
        if (indicator != null) indicator.SetActive(false);
    }

    void Update()
    {
        if (_playerInRange && Input.GetKeyDown(interactKey))
            toggle();

        float target = _isOn ? onAngle : offAngle;
        _currentAngle = Mathf.Lerp(_currentAngle, target, animSpeed * Time.deltaTime);
        applyRotation();
    }

    void toggle()
    {
        _isOn = !_isOn;
        if (indicator != null) indicator.SetActive(_isOn);

        if (_isOn) OnLeverActivated?.Invoke(this);
        else OnLeverDeactivated?.Invoke(this);
    }

    void applyRotation()
    {
        if (handle == null) return;
        handle.localRotation = Quaternion.Euler(_currentAngle, 0f, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) _playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) _playerInRange = false;
    }

    public LeverID ID => id;
    public float PressureValue => pressureValue;
    public bool IsOn => _isOn;
}