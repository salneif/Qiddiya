using UnityEngine;

using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour

{

    public int HP = 100;

    void Update()

    {

        // إذا صار الهيلث صفر

        if (HP <= 0)

        {

            SceneManager.LoadScene("END");

        }

        // إذا ضغط R يرجع للمين منيو

        if (Input.GetKeyDown(KeyCode.R))

        {

            SceneManager.LoadScene("MainMenu");

        }

    }

    // دالة تنقص الدم

    public void TakeDamage(int damage)

    {

        HP -= damage;

    }

}