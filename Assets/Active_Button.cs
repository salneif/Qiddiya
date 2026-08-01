
   using UnityEngine;

public class Active_Button : MonoBehaviour
{
    public Animator animator;
    public GameObject button;
    public string animationStateName = "Load-Anim";

    private bool activated = false;

    void Start()
    {
        button.SetActive(false);
    }

    void Update()
    {
        if (activated) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        // Animation has reached the end
        if (state.IsName(animationStateName) && state.normalizedTime >= 1f)
        {
            button.SetActive(true);
            activated = true;
        }
    }
}

