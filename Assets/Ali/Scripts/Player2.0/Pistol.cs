using UnityEngine;

public class Pistol : Shotgun
{
    protected override void Recoil()
    {
        gunAnimator.SetTrigger("Fire");
    }
}
