using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InputUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_TextMeshPro;
    [SerializeField] public TMP_InputField m_InputField;

   public void Enter()
    {
        m_TextMeshPro.text = m_InputField.text;
    }
}
