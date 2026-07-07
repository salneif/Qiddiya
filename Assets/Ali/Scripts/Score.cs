using TMPro;
using UnityEngine;

public class Score : MonoBehaviour
{
    [SerializeField] private TMP_Text Dtext;
    [SerializeField] private int score;


    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.CompareTag("Duck"))
        {
            Destroy(hit.gameObject);
            score++;
            Dtext.text = score.ToString();
        }
    }
}
