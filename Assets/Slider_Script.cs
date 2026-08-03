using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Slider_Script : MonoBehaviour
{
   
    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI sliderText;

    private void Start()
    {
        slider.onValueChanged.AddListener(UpdateValue);

        // Update text when the game starts
        UpdateValue(slider.value);
    }

    private void UpdateValue(float value)
    {
        sliderText.text = Mathf.RoundToInt(value * 100) + "%";
    }
}

