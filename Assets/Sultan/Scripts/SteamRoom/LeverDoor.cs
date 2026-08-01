using UnityEngine;

public class DoorLever : MonoBehaviour
{
    public Transform leverHandle;
    public Transform doorPivot;
    public Vector3 leverPulledRotation = new Vector3(0f, 0f, -60f);
    public Vector3 doorOpenRotation = new Vector3(0f, 100f, 0f);
    public float leverSpeed = 200f;
    public float doorSpeed = 90f;
    public string playerTag = "Player";

    private bool _isOpen;
    private bool _playerInRange;
    private Quaternion _leverRestRot;
    private Quaternion _leverPulledRot;
    private Quaternion _doorClosedRot;
    private Quaternion _doorOpenRot;

    void Start()
    {
        _leverRestRot = leverHandle.localRotation;
        _leverPulledRot = _leverRestRot * Quaternion.Euler(leverPulledRotation);
        _doorClosedRot = doorPivot.localRotation;
        _doorOpenRot = _doorClosedRot * Quaternion.Euler(doorOpenRotation);
    }

    void Update()
    {
        if (_playerInRange && Input.GetKeyDown(KeyCode.E))
            _isOpen = !_isOpen;

        Quaternion leverTarget = _isOpen ? _leverPulledRot : _leverRestRot;
        leverHandle.localRotation = Quaternion.RotateTowards(leverHandle.localRotation, leverTarget, leverSpeed * Time.deltaTime);

        Quaternion doorTarget = _isOpen ? _doorOpenRot : _doorClosedRot;
        doorPivot.localRotation = Quaternion.RotateTowards(doorPivot.localRotation, doorTarget, doorSpeed * Time.deltaTime);
        // Debug.Log(doorPivot.localRotation);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) _playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag)) _playerInRange = false;
    }

    public bool IsOpen => _isOpen;
}

