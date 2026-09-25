using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PauseMenuManager : MonoBehaviour
{
    private Canvas pauseCanvas;
    private bool isPaused = false;

    void Start()
    {
        pauseCanvas = GetComponent<Canvas>();
        
        if (pauseCanvas != null)
        {
            pauseCanvas.enabled = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    // Call this method on your Resume Button OnClick() event
    public void ResumeGame()
    {
        if (isPaused)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (pauseCanvas != null)
        {
            pauseCanvas.enabled = isPaused;
            if(isPaused )
           Time.timeScale = 0;
           else
           Time.timeScale = 1;
        }

        // Deselect the button so EventSystem doesn't lock Escape key input
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void ExitToMainMenu()
    {
        SceneManager.LoadScene("Hub-Menu");
    }
}