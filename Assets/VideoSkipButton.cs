using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class VideoSkipButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string nextSceneName = "Hub-Menu";
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float hideDelay = 3f;

    private CanvasGroup canvasGroup;
    private Coroutine hideCoroutine;
    private Vector3 lastMousePosition;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Start hidden
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        lastMousePosition = Input.mousePosition;
    }

    void Update()
    {
        // Detect mouse movement or key press
        if (Input.mousePosition != lastMousePosition || Input.anyKeyDown)
        {
            lastMousePosition = Input.mousePosition;
            ShowButton();
        }
    }

    public void ShowButton()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        hideCoroutine = StartCoroutine(ShowAndScheduleHide());
    }

    private IEnumerator ShowAndScheduleHide()
    {
        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        // Wait before hiding again
        yield return new WaitForSeconds(hideDelay);

        // Fade out
        elapsed = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    // Assign this method to the Skip Button's OnClick event
    public void SkipVideo()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}