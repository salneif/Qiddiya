using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// يربط سلايدر واحدًا بقناة صوت في AudioManager: يقرأ القيمة المحفوظة عند الفتح،
/// ويحفظ ويطبّق أي تغيير فورًا، ويحدّث نص النسبة.
/// ضعه على نفس كائن الـ Slider.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    public enum Channel { Master, Music, SFX }

    [SerializeField] private Channel channel = Channel.Master;

    [Tooltip("نص النسبة المئوية (اختياري)")]
    [SerializeField] private TextMeshProUGUI valueLabel;

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        // القيمة المحفوظة تُقرأ عند كل فتح للوحة الإعدادات، لا مرة واحدة فقط
        slider.SetValueWithoutNotify(ReadSavedValue());
        UpdateLabel(slider.value);

        slider.onValueChanged.AddListener(HandleValueChanged);
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(HandleValueChanged);
    }

    private void HandleValueChanged(float value)
    {
        var audio = AudioManager.Instance;
        if (audio != null)
        {
            switch (channel)
            {
                case Channel.Master: audio.SetMasterVolume(value); break;
                case Channel.Music: audio.SetMusicVolume(value); break;
                case Channel.SFX: audio.SetSfxVolume(value); break;
            }
        }

        UpdateLabel(value);
    }

    private float ReadSavedValue()
    {
        var audio = AudioManager.Instance;
        if (audio == null) return 1f;

        switch (channel)
        {
            case Channel.Music: return audio.GetMusicVolume();
            case Channel.SFX: return audio.GetSfxVolume();
            default: return audio.GetMasterVolume();
        }
    }

    private void UpdateLabel(float value)
    {
        if (valueLabel != null)
            valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
