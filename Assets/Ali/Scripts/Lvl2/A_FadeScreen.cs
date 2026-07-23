using System.Drawing;
using UnityEngine;
using UnityEngine.UI;

public class A_FadeScreen : MonoBehaviour
{
    [SerializeField] private RawImage FadeScreen;
    [SerializeField] private float fadeSpeed;
    [SerializeField] private float unFadeSpeed;



    private float _currentFadeSpeedTime;
    private float _currentUnFadeSpeedTime;
   [SerializeField] private bool _isFading = false;
    private bool _isUnFading;

    private void Update()
    {
        if( _isFading)
        {
            UnityEngine. Color color = FadeScreen.color;
            color.a += fadeSpeed * Time.deltaTime;
            color.a = Mathf.Clamp01(color.a);
            FadeScreen.color = color;

            if(color.a >= 1f)
            {
                _isFading = false;
                Invoke("HandleUnFade", 2);

            }
        }
        if (_isUnFading)
        {
            UnityEngine. Color color = FadeScreen.color;
            color.a -= unFadeSpeed * Time.deltaTime;
            color.a = Mathf.Clamp01(color.a);
            FadeScreen.color = color;

            if (color.a <= 0)
            {
                _isUnFading = false;
            }
        }
    }

    private void HandleUnFade()
    {
        _isUnFading = true;
    }
}
