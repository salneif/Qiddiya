using UnityEngine;

/// <summary>
/// Kills the player when they fall below a height threshold (falling off the level).
/// Put it on the player. Death routes through <see cref="PlayerKillable.Kill"/>,
/// so the burn shader plays and the player respawns at the last checkpoint.
/// </summary>
public class FallDeath : MonoBehaviour
{
    [Tooltip("If the player's world Y drops below this value, they die.")]
    [SerializeField] private float killY = -10f;

    [Tooltip("The player's kill handler. Auto-found on this object if left empty.")]
    [SerializeField] private PlayerKillable killable;

    private void Awake()
    {
        if (killable == null)
            killable = GetComponent<PlayerKillable>();
    }

    private void Update()
    {
        if (killable == null || killable.IsDead) return;
        if (transform.position.y < killY)
            killable.Kill();
    }
}
