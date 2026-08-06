using UnityEngine;
using UnityEngine.SceneManagement;

public class A_AfterIntro : MonoBehaviour
{
    [SerializeField] private float _currentTime = 85;

    private void Update()
    {
        if(_currentTime >= 0)
        {
            _currentTime -= Time.deltaTime;
            if( _currentTime < 0)
            {
                SceneManager.LoadScene(2);
            }
        }
    }
}
