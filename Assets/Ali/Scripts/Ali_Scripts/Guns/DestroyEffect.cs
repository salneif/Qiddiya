using UnityEngine;

public class DestroyEffect : MonoBehaviour
{
    [SerializeField] private float destroyTimer = 0.1f;



    private void Update()
    {
        Invoke("destroySelf", destroyTimer);
    }



    private void destroySelf()
    {
        Destroy(this);
    }
}
