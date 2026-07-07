using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GunManager : MonoBehaviour
{
    // this script will handle the our guns slots system and active the right gun
    // and it will be conected to the TelphoneMenu to swap guns  
    public static GunManager instance {  get; private set; }

    [Header("New Guns")]
    [SerializeField] private GameObject pistol;
    [SerializeField] private GameObject rifle;
    [SerializeField] private GameObject shotGun;

    [SerializeField] private GameObject pistol_ui;
    [SerializeField] private GameObject rifle_ui;
    [SerializeField] private GameObject shotGun_ui;



    [Header("Slots")]
   [SerializeField] private GameObject[] slots = new GameObject[2];// for the guns
   [SerializeField] private GameObject[] slots_UI = new GameObject[2]; // for the ui we will set them both at the same time


   


    private void Awake()
    {
        instance = this;
    }
  

    private void HandleGunSwaping()
    {
       
        if ( slots[0] != null && slots[0].gameObject.activeInHierarchy)
        {
            slots[0].gameObject.SetActive(false);
            slots_UI[0].gameObject.SetActive(false);

            slots[1].gameObject.SetActive(true);
            slots_UI[1].gameObject.SetActive(true);

        }
        else if (slots[1] != null && slots[1].gameObject.activeInHierarchy)
        {
            slots[0].gameObject.SetActive(true);
            slots_UI[0].gameObject.SetActive(true);

            slots[1].gameObject.SetActive(false);
            slots_UI[1].gameObject.SetActive(false);
        }
      
    }

    public void OnSwap(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            float scrolValue = context.ReadValue<Vector2>().y;
            if (Mathf.Abs(scrolValue) > 0.1f)
            {
                HandleGunSwaping();
            }
        }
    }

    public void AddpistolToSlot()
    {
       for(int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = pistol;
                slots_UI[i] = pistol_ui;

                slots[0].gameObject.SetActive(true);
                slots_UI[0].gameObject.SetActive(true);

                return;
            }
        }
            Debug.Log("you dont have space");
        
    }

    public void AddRifleToSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = rifle;
                slots_UI[i] = rifle_ui;

                slots[0].gameObject.SetActive(true);
                slots_UI[0].gameObject.SetActive(true);


                return;
            }
        }
        Debug.Log("you dont have space");

    }
    public void AddShotgunToSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = shotGun;
                slots_UI[i] = shotGun_ui;

                slots[0].gameObject.SetActive(true);
                slots_UI[0].gameObject.SetActive(true);


                return;
            }
        }
        Debug.Log("you dont have space");

    }

    public void RemoveGunsFromSlots()
    {
        for(int i = 0;i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].gameObject.SetActive(false);
                slots_UI[i].gameObject.SetActive(false);


                slots[i] = null;
                slots_UI[i] = null;
            }
        }
    }
}
