using UnityEngine;
using UnityEngine.SceneManagement;

public class Pause_Menu : MonoBehaviour
{
    public void LoadMainMenu()
{
    Time.timeScale = 1f;
    SceneManager.LoadScene(0);
}
    }

