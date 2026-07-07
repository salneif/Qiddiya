using MoreMountains.Feedbacks;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Shotgun : MonoBehaviour
{
    [Header("Connections")]
    // 'protected' means the Child (Shotgun) can see this, but other scripts cannot.
    [SerializeField] protected RaycastHit gunRaycastInfo;
    [SerializeField] private Transform maincam;

    [Header("Stats")]
    [SerializeField] protected float gunRange = 100f;
    [SerializeField] protected float gunDamage = 10f;
    [SerializeField] protected float fireRate = 0.5f;
    [SerializeField] protected float bulletSpeed = 50f;
    [SerializeField] protected int gunAmmo = 999;

    [Header("Visuals")]
    [SerializeField] protected ParticleSystem muzzleEffect;
    [SerializeField] protected float muzzleEffectDuration = 0.1f;
    [SerializeField] protected Animator gunAnimator;
    [SerializeField] protected TrailRenderer bulletTrail;
    [SerializeField] protected ParticleSystem impactParticleSystem;
    [SerializeField] protected Transform trailSpawnPoint;

    [Header("hitscan")]
    [SerializeField] LayerMask hitLayers;


    [Header("Swaping System")]
    [SerializeField] private bool isEquiepd;
    [SerializeField] private int slotNumber;



    [Header("TMP REF HERE")]
    [SerializeField] private TMP_Text text_Ammo;

    // Timer to track when we can shoot again
    protected float nextFireTime;


    //input button
    protected bool attackTrigger;

    //feedback
    [SerializeField] private MMF_Player shotFeedback;

    public void Start()
    {

        gunAnimator = GetComponent<Animator>();
        if (maincam == null)
        {
            maincam = Camera.main.transform;
        }

    }

    public void Update()
    {
        HandleShooting();

        text_Ammo.text = gunAmmo.ToString();
    }

    protected virtual void HandleShooting()
    {
        if (attackTrigger && nextFireTime <= Time.time)
        {

            shotFeedback.PlayFeedbacks();
            Recoil();

            muzzleEffect.Play();

            if (HandleHitScan(out gunRaycastInfo))
            {
                IDamgeable damageable = gunRaycastInfo.collider.GetComponent<IDamgeable>();

                if (damageable != null)
                {
                    damageable.TakeDamage(gunDamage);
                }
                StartCoroutine(HandleTrail(gunRaycastInfo));
            }
            else
            {
                StartCoroutine(HandleLostTrail());// if we didnt hit anything in the range of the gun 
            }
            gunAmmo--;
            nextFireTime = Time.time + fireRate;
        }
    }


    // protected virtual IEnumerator Flashmuzzle()
    // {
    //   muzzleEffect.SetActive(true);
    //   yield return new WaitForSeconds(muzzleEffectDuration);
    //   muzzleEffect.SetActive(false);

    //  }


    protected virtual void Recoil()
    {
        gunAnimator.Play("Shoting");

    }


    protected virtual bool HandleHitScan(out RaycastHit hitInfo)
    {

        Debug.DrawRay(maincam.position, maincam.forward * gunRange, Color.red, 2f);// just so we can see the line
        if (Physics.Raycast(maincam.position, maincam.forward, out hitInfo, gunRange, hitLayers))
        {
            return true;
        }
        return false;
    }


    protected virtual IEnumerator HandleTrail(RaycastHit gunRaycasthitInfo)
    {
        TrailRenderer instance = Instantiate(bulletTrail, trailSpawnPoint.position, Quaternion.identity);
        while (Vector3.Distance(instance.transform.position, gunRaycasthitInfo.point) > 0.1f)
        {
            instance.transform.position = Vector3.MoveTowards(
                instance.transform.position,
                gunRaycasthitInfo.point,
                bulletSpeed * Time.deltaTime

                );
            yield return null;// wait for the next frame and redo the while again 

        }
        ParticleSystem instanceofParticleSystem = Instantiate(impactParticleSystem, gunRaycasthitInfo.point, Quaternion.LookRotation(gunRaycastInfo.normal));
        Destroy(instanceofParticleSystem.gameObject, 2f);
        Destroy(instance.gameObject, instance.time);
    }


    protected virtual IEnumerator HandleLostTrail()
    {
        Vector3 longetPointYouCanGetToInRange = maincam.transform.position + (maincam.forward * gunRange);
        TrailRenderer instance = Instantiate(bulletTrail, trailSpawnPoint.position, Quaternion.identity);
        while (Vector3.Distance(instance.transform.position, longetPointYouCanGetToInRange) > 0.1f)
        {
            instance.transform.position = Vector3.MoveTowards(
                instance.transform.position,
                longetPointYouCanGetToInRange,
                bulletSpeed * Time.deltaTime
                );
            yield return null;
        }

        Destroy(instance.gameObject, instance.time);
    }

    public virtual void OnAttack(InputAction.CallbackContext context)
    {

        if (context.started)
        {
            attackTrigger = true;

        }
        else if (context.canceled)
        {
            attackTrigger = false;
        }
    }

}
