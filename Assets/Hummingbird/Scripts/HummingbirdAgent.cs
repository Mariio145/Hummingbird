using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Machine Learning Agent
/// </summary>
public class HummingbirdAgent : Agent
{
    [Tooltip("Fuerza a aplicar al moverse")]
    public float moveForce = 2f;

    [Tooltip("Velocidad de rotación de la cabeza de arriba a abajo")]
    public float pitchSpeed = 100f;

    [Tooltip("Velocidad de rotación de la cabeza de derecha a izquierda")]
    public float yawSpeed = 100f;

    [Tooltip("Punto de la punta del pico")]
    public Transform beakTip;

    [Tooltip("La camara del agente")]
    public Camera agentCamera;

    [Tooltip("Esta en modo de entrenamiento o de gameplay")]
    public bool trainingMode;

    new private Rigidbody rigidbody;

    // El conjunto de flores en cual está el agente
    private FlowerArea flowerArea;

    // La flor mas cercana al agente
    private Flower nearestFlower;

    // Permiten un movimiento suave
    private float smoothPitchChange = 0f;
    private float smoothYawChange = 0f;

    // Angulo máximo de movimiento de la cabeza
    private const float MaxPitchAngle = 80f;

    // Distancia máxima en la cual el pico acepta nectar
    private const float BeakTipRadius = 0.008f;

    // El agente esta quieto (sin poder volar)
    private bool frozen = false;

    /// <summary>
    /// Cantidad de nectar obtenido en un episodio
    /// </summary>
    public float NectarObtained { get; private set; }

    /// <summary>
    /// Inicializa el agente
    /// </summary>
    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();

        // Si no esta en modo entrenamiento, no hay limite de acciones
        if (!trainingMode) MaxStep = 0;
    }

    /// <summary>
    /// Reinicia el agente al comenzar un nuevo episodio
    /// </summary>
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // Reinicia las flores en modo entrenamiento solo si hay un agente por zona
            flowerArea.ResetFlowers();
        }

        // Reinicia el néctar obtenido
        NectarObtained = 0f;

        // Reinicia los vectores de movimiento para cuando comience un nuevo episodio
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        // Aparece al lado de una flor
        bool inFrontOfFlower = true;
        if (trainingMode)
        {
            // Aparece al lado de una flor un 50% de las veces
            inFrontOfFlower = UnityEngine.Random.value > .5f;
        }

        // Mueve al agente a una posicion aleatoria
        MoveToSafeRandomPosition(inFrontOfFlower);

        // Recalcular el path a la flor más cercana
        UpdateNearestFlower();
    }

    /// <summary>
    /// Se ejecuta cuando una acción es recibida por accion del jugador o de la red neuronal
    /// 
    /// actions contiene una array de acciones continuas (de -1 a +1) que va:
    /// Index 0: move vector x (+1 = right, -1 = left)
    /// Index 1: move vector y (+1 = up, -1 = down)
    /// Index 2: move vector z (+1 = forward, -1 = backward)
    /// Index 3: pitch angle (+1 = pitch up, -1 = pitch down)
    /// Index 4: yaw angle (+1 = turn right, -1 = turn left)
    /// </summary>
    /// <param name="actions">Las acciones a realizar</param>
    /// 
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Ni hacer nada si esta quieto
        if (frozen) return;

        // Calcular direccion de movimiento
        Vector3 move = new Vector3(actions.ContinuousActions.Array[0], actions.ContinuousActions.Array[1], actions.ContinuousActions.Array[2]);

        // Añadir fuerza en la direccion creada
        rigidbody.AddForce(move * moveForce);

        // Rotación actual
        Vector3 rotationVector = transform.rotation.eulerAngles;

        // Calcular rotación de la cabeza
        float pitchChange = actions.ContinuousActions.Array[3];
        float yawChange = actions.ContinuousActions.Array[4];

        // Calcular cambios suaves de la cabeza
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);

        // Calcular rotacion de la cabeza en base a la rotacion suave 
        float pitch = rotationVector.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f;
        // Ajustar la rotación a los limites
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rotationVector.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        // Aplicar la nueva rotación
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// Recoge todos los parametros (observaciones) que servirán como inputs de la red neuronal
    /// </summary>
    /// <param name="sensor">El vector de sensores</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        // ISi no hay flor cercana, no le pasa ningún parámetro
        if (nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }
        
        // Observa la rotacion del agente
        sensor.AddObservation(transform.localRotation.normalized);

        // Vector que va desde el pico hasta la flor más cercana
        Vector3 toFlower = nearestFlower.FlowerCenterPosition - beakTip.position;

        // Observa el vector anterior normalizado
        sensor.AddObservation(toFlower.normalized);

        // Observa un producto que indica si la punta del pico está en frente de la flor
        // (+1 indica que esta en frente, -1 significa detrás)
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observa un producto que indica si la punta del pico está apuntando a la flor
        // (+1 indica que si apunta a la flor, -1 indica que no)
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized));

        // Observa la distancia que hay del pico a la flor
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);

        // 10 total observations
    }

    /// <summary>
    /// Cuando Behavior Type este puesto como "Heuristic Only" en los parámetros del agente,
    /// se llamará a esta función. Los valores devueltos iran a
    /// <see cref="OnActionReceived(ActionBuffers)"/> en vez de usar la red neuronal
    /// </summary>
    /// <param name="actionsOut">Conjunto de acciones/param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Crea placeholders para todos los movimientos/rotaciones
        Vector3 forward = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;

        // Convierte todos los inputs en movimiento
        // Los valores deben estar entre -1 y

        // Forward/backward
        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        // Left/right
        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left = transform.right;

        // Up/down
        if (Input.GetKey(KeyCode.E)) up = transform.up;
        else if (Input.GetKey(KeyCode.C)) up = -transform.up;

        // Pitch up/down
        if (Input.GetKey(KeyCode.UpArrow)) pitch = -1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = 1f;

        // Turn left/right
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        // Combina los vectores de movimiento y los normaliza
        Vector3 combined = (forward + left + up).normalized;

        // Añade los valores a la lista de acciones
        actionsOut.ContinuousActions.Array[0] = combined.x;
        actionsOut.ContinuousActions.Array[1] = combined.y;
        actionsOut.ContinuousActions.Array[2] = combined.z;
        actionsOut.ContinuousActions.Array[3] = pitch;
        actionsOut.ContinuousActions.Array[4] = yaw;
    }

    /// <summary>
    /// Evita que el agente tome acciones
    /// </summary>
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = true;
        rigidbody.Sleep();
    }

    /// <summary>
    /// Devuelve el movimiento al agente
    /// </summary>
    public void UnfreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = false;
        rigidbody.WakeUp();
    }

    /// <summary>
    /// Mueve el agente a una posicion segura
    /// </summary>
    /// <param name="inFrontOfFlower">Si aparecerá delante de una flor</param>
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100; // Para evitar un bucle infinito
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        // Entra en bucle hasta encontrar una posición válida o se quede sin intentos
        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if (inFrontOfFlower)
            {
                // Coge una flor aleatoria
                Debug.Log(flowerArea.Flowers.Count);
                Flower randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];

                // Se posiciona entre 10 y 20 cm de la flor
                float distanceFromFlower = UnityEngine.Random.Range(.1f, .2f);
                potentialPosition = randomFlower.transform.position + randomFlower.FlowerUpVector * distanceFromFlower;

                // Apuntar el pico del Hummingbird a la flor
                Vector3 toFlower = randomFlower.FlowerCenterPosition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                // Coge una altura aleatoria
                float height = UnityEngine.Random.Range(1.2f, 2.5f);

                // Coger un radio aleatorio de la arena
                float radius = UnityEngine.Random.Range(2f, 7f);

                // Rotamos aleatoriamente
                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

                // Combinamos los parámetros anteriores para posicionar el Hummingbird
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                // Coge una rotacion aleatoria de la cabeza
                float pitch = UnityEngine.Random.Range(-60f, 60f);
                float yaw = UnityEngine.Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Comprobar si colisiona con algo cercano
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);

            // Si no colisiona con nada, se considera una posicion segura
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

        // Posicionar el hummingbird
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    /// <summary>
    /// Actializa la flor mas cernaca al agente
    /// </summary>
    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.HasNectar)
            {
                // No hay flor más cercana, así que se pone la primera que tenga néctar
                nearestFlower = flower;
            }
            else if (flower.HasNectar)
            {
                // Calcula las distancias entre la supuesta flor más cercana y la que se quiere comprobar
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                // Actualiza si la flor actual no tiene nectar o si está más cerca que la anterior
                if (!nearestFlower.HasNectar || distanceToFlower < distanceToCurrentNearestFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }

    /// <summary>
    /// Llamado cuando el collider entra en un trigger
    /// </summary>
    /// <param name="other">El collider del trigger</param>
    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    /// Llamado cuando el collider se mantiene dentro de un trigger
    /// </summary>
    /// <param name="other">El collider del trigger</param>
    private void OnTriggerStay(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    /// Comportamiento del agente cuando entre o se mantenga en un trigger
    /// </summary>
    /// <param name="collider">El collider del trigger</param>
    private void TriggerEnterOrStay(Collider collider)
    {
        // Comprueba si el agente está recogiendo nectar
        if (collider.CompareTag("nectar"))
        {
            Vector3 closestPointToBeakTip = collider.ClosestPoint(beakTip.position);

            // Check if the closest collision point is close to the beak tip
            if (Vector3.Distance(beakTip.position, closestPointToBeakTip) < BeakTipRadius)
            {
                // Conseguimos la flor gracias a su trigger de néctar
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                // Intenta recoger .01 de néctar
                // Nota: esto ocurre cada cantidad fija de tiempo (fixedTime), es decir cada .02 s, o 50 veces por segundo
                float nectarReceived = flower.Feed(.01f);

                // Mantiene un recuerdo del néctar recogido
                NectarObtained += nectarReceived;

                if (trainingMode)
                {
                    // Calcula la recompensa por el néctar recogido
                    float bonus = .02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.FlowerUpVector.normalized));
                    AddReward(.01f + bonus);
                }

                // Si se vacía la flor, se busca otra
                if (!flower.HasNectar)
                {
                    UpdateNearestFlower();
                }
            }
        }
    }

    /// <summary>
    /// Llamado cuando el agente colisiona con algo sólido
    /// </summary>
    /// <param name="collision">Información de la colisión</param>
    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("boundary"))
        {
            // Ha chocado con la isla, recompensa negativa
            AddReward(-.5f);
        }
    }

    /// <summary>
    /// Llamado cada frame
    /// </summary>
    private void Update()
    {
        // Dibuja una linea entre el pico y la flor más cercana
        if (nearestFlower != null)
            Debug.DrawLine(beakTip.position, nearestFlower.FlowerCenterPosition, Color.green);
    }

    /// <summary>
    /// Llamado cada .02 s
    /// </summary>
    private void FixedUpdate()
    {
        // Evita la situacion de que el enemigo haya robado la flor que el agente tenia marcada como más cercana y no se actualiza
        if (nearestFlower != null && !nearestFlower.HasNectar)
            UpdateNearestFlower();
    }
}
