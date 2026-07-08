using UnityEngine;

public class ForceCrouchZone : MonoBehaviour
{
    [SerializeField] private float crouchMoveSpeed = 2.5f;

    private PlayerController _cachedPC;
    private bool _playerInside;

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        _cachedPC = pc;
        _playerInside = true;
        ForceCrouch(pc);
    }

    void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        _playerInside = false;
        _cachedPC = null;

        pc.speed = pc.normalWalkSpeed;
        pc.animator.SetBool("IsCrouch", false);
        Debug.Log(pc.animator.GetBool("IsCrouch"));
        pc.characterController.height = 1f;
        pc.characterController.center = Vector3.zero;
    }

    void LateUpdate()
    {
        if (_playerInside && _cachedPC != null)
            ForceCrouch(_cachedPC);
    }

    void ForceCrouch(PlayerController pc)
    {
        pc.speed = crouchMoveSpeed;
        pc.animator.SetBool("IsCrouch", true);
        pc.characterController.height = 0.5f;
        pc.characterController.center = new Vector3(0f, -0.25f, 0f);
    }
}