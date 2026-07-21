using UnityEngine;
using System.Collections;

public class TeleportTent : MonoBehaviour
{
    [SerializeField] private Transform destination;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float holdDelay = 0.5f;
    [SerializeField] private float reentryCooldown = 0.5f;
    [SerializeField] private string playerTag = "Player";

    private static bool _busy;
    private static float _cooldownUntil;

    private void OnTriggerEnter(Collider other)
    {
        if (_busy || Time.time < _cooldownUntil) return;
        if (!other.CompareTag(playerTag)) return;
        if (destination == null) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        CharacterController cc = other.GetComponent<CharacterController>();
        if (pc == null || cc == null) return;

        StartCoroutine(teleportSequence(pc, cc));
    }

    private IEnumerator teleportSequence(PlayerController pc, CharacterController cc)
    {
        _busy = true;
        pc.SetInputEnabled(false);

        yield return fade(0f, 1f);

        cc.enabled = false;
        cc.transform.position = destination.position;
        cc.enabled = true;

        yield return new WaitForSeconds(holdDelay);

        yield return fade(1f, 0f);

        pc.SetInputEnabled(true);
        _cooldownUntil = Time.time + reentryCooldown;
        _busy = false;
    }

    private IEnumerator fade(float from, float to)
    {
        if (fadeOverlay == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        fadeOverlay.alpha = to;
    }

    private void OnDrawGizmos()
    {
        if (destination != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Gizmos.DrawLine(transform.position, destination.position);
            Gizmos.DrawWireSphere(destination.position, 0.3f);
        }

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            Gizmos.DrawCube(box.center, box.size);
    }
}