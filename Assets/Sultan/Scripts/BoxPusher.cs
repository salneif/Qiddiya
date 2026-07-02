using UnityEngine;

public class BoxPusher : MonoBehaviour
{
    [SerializeField] private LayerMask boxLayer;
    [SerializeField] private float blockCheckRange = 0.4f;

    private PlayerController _player;
    private int _pushFrame = -1;

    void Start()
    {
        _player = GetComponent<PlayerController>();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody boxRb = hit.collider.attachedRigidbody;
        if (boxRb == null) return;

        Box box = hit.collider.GetComponent<Box>();
        if (box == null || box.State != Box.BoxState.Scattered) return;

        float input = _player.MoveInput;
        if (Mathf.Abs(input) < 0.01f) return;

        if (hit.normal.y > 0.3f) return;

        float pushDir = Mathf.Sign(input);
        if (pushDir * hit.normal.x > 0f) return;

        Vector3 dir = new Vector3(pushDir, 0f, 0f);
        Vector3 rayStart = box.transform.position + dir * 0.36f;
        if (Physics.Raycast(rayStart, dir, blockCheckRange, boxLayer)) return;

        Vector3 vel = boxRb.linearVelocity;
        vel.x = pushDir * Mathf.Abs(_player.CurrentSpeed);
        boxRb.linearVelocity = vel;
        _pushFrame = Time.frameCount;
    }

    public bool IsPushing => Time.frameCount <= _pushFrame + 1;
}