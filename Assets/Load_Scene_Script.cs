using UnityEngine;
using UnityEngine.SceneManagement;

public class Load_Scene_Script : MonoBehaviour
{
    public string sceneName;

    public void LoadScene()
    {
        SceneManager.LoadScene(sceneName);
    }
}