using UnityEngine;

public class BoxPusher : MonoBehaviour
{
    private PlayerController _player;
    private int _pushFrame = -1;

    void Start()
    {
        _player = GetComponent<PlayerController>();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody boxRb = hit.collider.attachedRigidbody;
        if (boxRb == null || boxRb.isKinematic) return;

        if (hit.collider.GetComponent<Box>() == null) return;

        float input = _player.MoveInput;
        if (Mathf.Abs(input) < 0.01f) return;

        if (hit.normal.y > 0.3f) return;

        float pushDir = Mathf.Sign(input);
        if (pushDir * hit.normal.x > 0f) return;

        Vector3 vel = boxRb.linearVelocity;
        vel.x = pushDir * Mathf.Abs(_player.CurrentSpeed);
        boxRb.linearVelocity = vel;
        _pushFrame = Time.frameCount;
    }

    public bool IsPushing => Time.frameCount <= _pushFrame + 1;
}