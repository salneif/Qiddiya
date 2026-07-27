using UnityEngine;

public class CluePlate : MonoBehaviour
{
    [SerializeField] private AnimalPlate.AnimalId id;

    public AnimalPlate.AnimalId Id => id;
}