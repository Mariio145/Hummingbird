using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona una flor con nectar
/// </summary>
public class Flower : MonoBehaviour
{
    [Tooltip("Color de la flor cuando esta contenga nectar")]
    public Color fullFlowerColor = new Color(1f, 0f, .3f);

    [Tooltip("Color de la flor cuando este vacia")]
    public Color emptyFlowerColor = new Color(.5f, 0f, 1f);

    /// <summary>
    /// El trigger que representa el nectar
    /// </summary>
    [HideInInspector]
    public Collider nectarCollider;

    // El collider de los petalos
    private Collider flowerCollider;

    // El material de la flor
    private Material flowerMaterial;

    /// <summary>
    /// Vector que apunta hacia afuera de la flor
    /// </summary>
    public Vector3 FlowerUpVector
    {
        get
        {
            return nectarCollider.transform.up;
        }
    }

    /// <summary>
    /// Posicion del nectar
    /// </summary>
    public Vector3 FlowerCenterPosition
    {
        get
        {
            return nectarCollider.transform.position;
        }
    }

    /// <summary>
    /// Cantidad restante de nectar en la flor
    /// </summary>
    public float NectarAmount { get; private set; }

    /// <summary>
    /// Devuelve true si queda nectar
    /// </summary>
    public bool HasNectar
    {
        get
        {
            return NectarAmount > 0f;
        }
    }

    /// <summary>
    /// Intenta recolectar nectar de la flor
    /// </summary>
    /// <param name="amount">Cantidad de nectar que se solicita eliminar</param>
    /// <returns>Cantidad de nectar que se ha conseguido eliminar</returns>
    public float Feed(float amount)
    {
        // Sacamos la cantidad que si se ha conseguido recolectar (no puede ser mas de la que hay disponible)
        float nectarTaken = Mathf.Clamp(amount, 0f, NectarAmount);

        // Restar el nectar
        NectarAmount -= amount;

        if (NectarAmount <= 0)
        {
            // No queda nectar
            NectarAmount = 0;

            // Desactivar los colliders
            flowerCollider.gameObject.SetActive(false);
            nectarCollider.gameObject.SetActive(false);

            // Cambiar el color de la flor
            flowerMaterial.SetColor("_BaseColor", emptyFlowerColor);
        }

        // Devuelve la cantidad eliminada
        return nectarTaken;
    }

    /// <summary>
    /// Reinicia la flor
    /// </summary>
    public void ResetFlower()
    {
        // Rellena el nectar
        NectarAmount = 1f;

        // Activa los colliders
        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);

        // Cambia el color de la flor
        flowerMaterial.SetColor("_BaseColor", fullFlowerColor);
    }

    /// <summary>
    /// Llamada cuando la flor se activa
    /// </summary>
    private void Awake()
    {
        // Se guarda el meshRenderer y el material en sus variables
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        flowerMaterial = meshRenderer.material;

        // FSelecciona los colliders de la flor y del nectar
        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }
}
