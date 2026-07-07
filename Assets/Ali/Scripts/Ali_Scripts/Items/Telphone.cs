using UnityEngine;
using UnityEngine.InputSystem;

public class Telphone : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject normalUI;
    [SerializeField] private GameObject telephoneUI;


    [SerializeField] private GameObject gunMangerButton;

    [Header("Guns Buttons")]
    [SerializeField] private GameObject pistolButton;
    [SerializeField] private GameObject rifleButton;
    [SerializeField] private GameObject shotgunButton;
    private bool ingunMangerMenu = false;


    private GunManager gunManagerInstance;

    private void Awake()
    {
        pistolButton.SetActive(false);
        rifleButton.SetActive(false);
        shotgunButton.SetActive(false);
    }
    private void Start()
    {
        gunManagerInstance = GunManager.instance;
    }


    public void Interact()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
       normalUI.SetActive(false);
        telephoneUI.SetActive(true);

        Time.timeScale = 0;

    }

    public void UnInteract()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        normalUI.SetActive(true);
        telephoneUI.SetActive(false);

        Time.timeScale = 1;

    }


    public void OnQuit(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (ingunMangerMenu)
            {
                QuitGunsMenu();
            }
            else
            {
                UnInteract();
            }
        }
    }

    public void EneterGunsMenu()
    {
        pistolButton.SetActive(true);
        rifleButton.SetActive(true);
        shotgunButton.SetActive(true);

        gunMangerButton.SetActive(false);
        ingunMangerMenu = true;

        gunManagerInstance.RemoveGunsFromSlots();
    }
    public void QuitGunsMenu()
    {
        pistolButton.SetActive(false);
        rifleButton.SetActive(false);
        shotgunButton.SetActive(false);

        gunMangerButton.SetActive(true);
        ingunMangerMenu = false;
    }
    
}
