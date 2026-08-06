using UnityEngine;
using System;

public class Lever : MonoBehaviour
{
    public enum LeverID { A, B, C, D }
    public enum LeverState { Middle, Up, Down }

    [SerializeField] private LeverID id;
    [SerializeField] private float pressureValue;
    [SerializeField] private Transform handle;
    [SerializeField] private GameObject indicator;
    [SerializeField] private float middleAngle = -90f;
    [SerializeField] private float upAngle = -45f;
    [SerializeField] private float downAngle = 45f;
    [SerializeField] private float animSpeed = 8f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip interactClip;

    public event Action<Lever> OnLeverChanged;

    private LeverState _state = LeverState.Middle;
    private bool _playerInRange;
    private float _currentAngle;


    // Ali 
    public event Action<Vector3> OnInteractWithLever;

    void Start()
    {
        _currentAngle = middleAngle;
        applyRotation();
        if (indicator != null) indicator.SetActive(false);
    }

    void Update()
    {
        if (_playerInRange && Input.GetKeyDown(interactKey))
            toggle();

        float target = _state == LeverState.Up ? upAngle : _state == LeverState.Down ? downAngle : middleAngle;

        _currentAngle = Mathf.Lerp(_currentAngle, target, animSpeed * Time.deltaTime);
        applyRotation();
    }

    void toggle()
    {
        if (audioSource != null && interactClip != null)
            audioSource.PlayOneShot(interactClip);

        if (_state == LeverState.Middle) _state = LeverState.Up;
        else if (_state == LeverState.Up) _state = LeverState.Down;
        else _state = LeverState.Middle;

        if (indicator != null) indicator.SetActive(_state != LeverState.Middle);
        OnLeverChanged?.Invoke(this);
    }

    void applyRotation()
    {
        if (handle == null) return;
        // handle.localRotation = Quaternion.Euler(_currentAngle, 0f, 0f);
        Vector3 angles = handle.localEulerAngles;
        handle.localRotation = Quaternion.Euler(_currentAngle, angles.y, angles.z);
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
    public LeverState State => _state;
    public float PressureValue => pressureValue;

    public float PressureContribution =>
        _state == LeverState.Up ? pressureValue : _state == LeverState.Down ? -pressureValue : 0f;
}