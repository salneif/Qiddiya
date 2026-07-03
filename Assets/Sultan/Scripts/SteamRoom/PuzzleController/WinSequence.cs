using UnityEngine;
using System.Collections;

public class WinSequence : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float holdDuration = 2f;
    [SerializeField] private float fadeDuration = 1.5f;

    public void Trigger()
    {
        StartCoroutine(sequence());
    }

    IEnumerator sequence()
    {
        player.SetInputEnabled(false);

        yield return new WaitForSeconds(holdDuration);

        if (fadeOverlay != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = elapsed / fadeDuration;
                yield return null;
            }
            fadeOverlay.alpha = 1f;
        }
    }
}
