using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona un conjunto de plantas y de flores unidas a esas plantas
/// </summary>
public class FlowerArea : MonoBehaviour
{
    // El diámetro del área donde se pueden usar el agente y las flores 
    // para observar la distancia relativa entre el agente y la flor.
    public const float AreaDiameter = 20f;

    // Lista de todas las plantas que hay en esta area (las plantas tienen varias flores)
    private List<GameObject> flowerPlants;

    // Diccionario que relaciona el collider del nectar con la flor asociada
    private Dictionary<Collider, Flower> nectarFlowerDictionary;

    /// <summary>
    /// Lista de todas las flores en el area
    /// </summary>
    public List<Flower> Flowers { get; private set; }

    /// <summary>
    /// Reinicia las plantas y las flores
    /// </summary>
    public void ResetFlowers()
    {
        // Rota cada planta en el eje Y y un poco el los otros dos
        foreach (GameObject flowerPlant in flowerPlants)
        {
            float xRotation = UnityEngine.Random.Range(-5f, 5f);
            float yRotation = UnityEngine.Random.Range(-180f, 180f);
            float zRotation = UnityEngine.Random.Range(-5f, 5f);
            flowerPlant.transform.localRotation = Quaternion.Euler(xRotation, yRotation, zRotation);
        }

        // Reinicia cada flor
        foreach (Flower flower in Flowers)
        {
            flower.ResetFlower();
        }
    }

    /// <summary>
    /// Devuelve la <see cref="Flower"/> a la que pertenece el nectar
    /// </summary>
    /// <param name="collider">El collider del nectar</param>
    /// <returns>La flor a la que corresponde</returns>
    public Flower GetFlowerFromNectar(Collider collider)
    {
        return nectarFlowerDictionary[collider];
    }

    /// <summary>
    /// Llamado cuando un area se activa
    /// </summary>
    private void Awake()
    {
        // Initializar variables
        flowerPlants = new List<GameObject>();
        nectarFlowerDictionary = new Dictionary<Collider, Flower>();
        Flowers = new List<Flower>();

        FindChildFlowers(transform);
    }

    private void Start()
    {
        // Encuentra todas las flores que son hijos de este GameObject
        
    }

    /// <summary>
    /// Encuentra recursivamente todas las plantas y flores que son hijos de un transform
    /// </summary>
    /// <param name="parent">El padre de las flores/plantas a buscar</param>
    private void FindChildFlowers(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.CompareTag("flower_plant"))
            {
                // Encontró una planta y la añade a la lista
                flowerPlants.Add(child.gameObject);

                // Buscar flores dentro de la planta
                FindChildFlowers(child);
            }
            else
            {
                // No es una planta, mirar a ver si es una flor
                Flower flower = child.GetComponent<Flower>();
                if (flower != null)
                {
                    // Es una flor, la añade a la lista de flores
                    Flowers.Add(flower);

                    // Añade el nectarCollider junto a la flor en el diccionario
                    nectarFlowerDictionary.Add(flower.nectarCollider, flower);
                }
                else
                {
                    // No es una flor, seguir buscando dentro
                    FindChildFlowers(child);
                }
            }
        }
    }
}
