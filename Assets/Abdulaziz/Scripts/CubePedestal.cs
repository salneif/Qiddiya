using System.Collections;
using UnityEngine;


public class CubePedestal : MonoBehaviour, IInteractable
{
    [Header("Cube")]
    [Tooltip("The cube object on the pedestal that visually rotates.")]
    [SerializeField] private Transform _cube;

    [Tooltip("Degrees the cube turns per use. 90 lines each face up with a room.")]
    [SerializeField] private float _rotationStep = 90f;

    [Tooltip("How fast (deg/sec) the cube turns to its new facing.")]
    [SerializeField] private float _rotationSpeed = 360f;

    [Tooltip("Axis the cube spins around.")]
    [SerializeField] private Vector3 _rotationAxis = Vector3.up;

    [Header("Rooms")]
    [Tooltip("One destination spawn point per cube facing. The door travels " +
             "to whichever one is currently selected.")]
    [SerializeField] private Transform[] roomDestinations;



    private int _currentIndex = 0;
    private bool _isRotating = false;
    private Quaternion _start, _target;
    float _angle;
    public Transform CurrentDestination =>
        (roomDestinations != null && roomDestinations.Length > 0)
            ? roomDestinations[_currentIndex]
            : null;

    public void Interact()
    {
        // checks if the object is rotating, if they are then it wont run this code.
        if (_isRotating) return;

        // if the room doesnt exist or it's length is 0, then it wont run this code.
        if (roomDestinations == null || roomDestinations.Length == 0) return;


        _currentIndex = (_currentIndex + 1) % roomDestinations.Length;

        StartCoroutine(RotateCube());
    }



    private IEnumerator RotateCube()
    {
        _isRotating = true;

        _start = _cube.rotation;
        _target = _start * Quaternion.AngleAxis(_rotationStep, _rotationAxis.normalized);

        _angle = Mathf.Max(Quaternion.Angle(_start, _target), 0.001f);

        float t = 0f;
        // here it does the rotation 
        while (t < 1f)
        {
            t += (_rotationSpeed * Time.deltaTime) / _angle;
            _cube.rotation = Quaternion.Slerp(_start, _target, Mathf.Clamp01(t));
            yield return null;
        }

        _cube.rotation = _target;   // snap to an exact facing so it never drifts
        _isRotating = false;
    }



    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        if (_cube != null)
        {
            Vector3 axis = _cube.rotation * _rotationAxis.normalized;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(_cube.position - axis * 0.6f, _cube.position + axis * 0.6f);
        }

        if (roomDestinations == null) return;

        for (int i = 0; i < roomDestinations.Length; i++)
        {
            Transform dest = roomDestinations[i];
            if (dest == null) continue;

            bool isCurrent = (i == _currentIndex);

            Gizmos.color = isCurrent ? Color.red : new Color(0.55f, 0.55f, 0.55f, 0.9f);
            Gizmos.DrawLine(transform.position, dest.position);
            Gizmos.DrawWireSphere(dest.position, 0.3f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(dest.position, dest.forward);


        }
    }
}