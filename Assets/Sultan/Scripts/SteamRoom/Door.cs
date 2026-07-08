using UnityEngine;

public class Door : MonoBehaviour
{
    public Transform doorPivot;
    public Vector3 doorOpenRotation = new Vector3(0f, -90f, 0f);
    public float doorSpeed = 90f;
    public string playerTag = "Player";
    private bool _playerInRange;
    private bool _isOpen;
    private float _stayTime;

    private Quaternion _doorClosedRot;
    private Quaternion _doorOpenRot;

    void Start()
    {
        _doorClosedRot = doorPivot.localRotation;
        _doorOpenRot = _doorClosedRot * Quaternion.Euler(doorOpenRotation);
    }

    void Update()
    {
        _stayTime -= Time.deltaTime;
        _playerInRange = _stayTime > 0f;

        if (_playerInRange && Input.GetKeyDown(KeyCode.E))
            _isOpen = !_isOpen;
        Quaternion doorTarget = _isOpen ? _doorOpenRot : _doorClosedRot;
        doorPivot.localRotation = Quaternion.RotateTowards(doorPivot.localRotation, doorTarget, doorSpeed * Time.deltaTime);
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(playerTag)) _stayTime = 0.2f;
    }

    public bool IsOpen => _isOpen;
}