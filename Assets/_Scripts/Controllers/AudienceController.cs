using System;
using System.Collections.Generic;
using UnityEngine;

public class AudienceController : MonoBehaviour
{
    [System.Serializable]
    public struct AudienceData
    {
        public Sprite sprite;
        [Range(0f, 100f)] public float weight;
    }

    [Header("Referencias principales")]
    [Tooltip("Transform hacia donde se dirigirá la audiencia según la puntuación.")]
    [SerializeField] private Transform exit;
    [Tooltip("Lista de transforms existentes en la escena que ya contienen un SpriteRenderer.")]
    [SerializeField] private List<Transform> sits = new List<Transform>();

    [Header("Tipos de Audiencia y Pesos (0-100)")]
    [SerializeField] private List<AudienceData> audienceTypes = new List<AudienceData>();

    [Header("Puntuación (0 a 100)")]
    [Range(0f, 100f)]
    [SerializeField] private float score = 100f; // Por defecto a 100 para que empiecen todos en su sitio

    [Header("Parámetros de Movimiento")]
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("Altura de cada saltito.")]
    [SerializeField] private float hopHeight = 0.2f;
    [Tooltip("Frecuencia de los saltos (cuántos saltitos dan por unidad de distancia).")]
    [SerializeField] private float hopFrequency = 15f;

    [Header("Detección de llegada")]
    [Tooltip("Distancia mínima para considerar que ha llegado a la salida o al asiento.")]
    [SerializeField] private float reachThreshold = 0.05f;

    [SerializeField] private AudienceManager audienceManager;
    // Clase interna para gestionar los datos de cada miembro y sus saltos
    private class AudienceMember
    {
        public Transform transform;
        public Vector3 initialPosition;
        public SpriteRenderer spriteRenderer;
        public bool hasLeft = false;

        // Variables para controlar el movimiento suave con saltitos
        public Vector3 moveStartPos;
        public Vector3 moveTargetPos;
        public float journeyLength;
        public float startTime;
        bool wasMovingToExit = false;

        // Propiedad pública interna para el setter
        public bool WasMovingToExit
        {
            get => wasMovingToExit;
            set => wasMovingToExit = value;
        }
    }

    private List<AudienceMember> audienceMembers = new List<AudienceMember>();

    void Start()
    {
        InitializeAudience();
    }

    void Update()
    {
        UpdateAudienceBehavior();
    }

    /// <summary>
    /// Configura los sprites en los transforms existentes según los pesos, guarda posiciones y mezcla la lista aleatoriamente.
    /// </summary>
    private void InitializeAudience()
    {
        if (sits == null || sits.Count == 0)
        {
            Debug.LogWarning("La lista de asientos (sits) está vacía.");
            return;
        }

        if (audienceTypes == null || audienceTypes.Count == 0)
        {
            Debug.LogWarning("No hay tipos de audiencia configurados.");
            return;
        }

        audienceMembers.Clear();

        foreach (var sit in sits)
        {
            if (sit == null) continue;

            SpriteRenderer sr = sit.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = sit.gameObject.AddComponent<SpriteRenderer>();
            }

            sr.sprite = GetWeightedRandomSprite();

            AudienceMember member = new AudienceMember
            {
                transform = sit,
                initialPosition = sit.position,
                spriteRenderer = sr,
                hasLeft = false,
                moveStartPos = sit.position,
                moveTargetPos = sit.position,
                journeyLength = 0f,
                startTime = Time.time
            };

            audienceMembers.Add(member);
        }

        // Mezclar aleatoriamente la lista (Algoritmo Fisher-Yates)
        for (int i = 0; i < audienceMembers.Count; i++)
        {
            AudienceMember temp = audienceMembers[i];
            int randomIndex = UnityEngine.Random.Range(i, audienceMembers.Count);
            audienceMembers[i] = audienceMembers[randomIndex];
            audienceMembers[randomIndex] = temp;
        }
        if (audienceManager == null)
        {
            audienceManager = FindAnyObjectByType<AudienceManager>();
        }
        if (audienceManager != null)
        {
            SetScore(audienceManager.currentAudience);
        }

    }

    /// <summary>
    /// Selecciona un sprite de forma aleatoria ponderada según sus porcentajes de peso.
    /// </summary>
    private Sprite GetWeightedRandomSprite()
    {
        float totalWeight = 0f;
        foreach (var audience in audienceTypes)
        {
            totalWeight += audience.weight;
        }

        if (totalWeight <= 0f) return audienceTypes[0].sprite;

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentSum = 0f;

        foreach (var audience in audienceTypes)
        {
            currentSum += audience.weight;
            if (randomValue <= currentSum)
            {
                return audience.sprite;
            }
        }

        return audienceTypes[0].sprite;
    }

    /// <summary>
    /// Controla el desplazamiento dando saltitos hacia la salida o de vuelta al asiento según la puntuación.
    /// </summary>
    private void UpdateAudienceBehavior()
    {
        if (audienceMembers.Count == 0 || exit == null) return;

        float normalizedScore = Mathf.Clamp01(score / 100f);
        int totalCount = audienceMembers.Count;
        int targetLeavingCount = Mathf.RoundToInt(totalCount * (1f - normalizedScore));

        for (int i = 0; i < totalCount; i++)
        {
            AudienceMember member = audienceMembers[i];
            bool shouldLeave = i < targetLeavingCount;
            Vector3 targetPosition = shouldLeave ? exit.position : member.initialPosition;

            // Si cambia de objetivo, reiniciamos el trayecto
            if (shouldLeave != member.WasMovingToExit || member.moveTargetPos != targetPosition)
            {
                member.moveStartPos = member.transform.position;
                member.moveTargetPos = targetPosition;
                member.journeyLength = Vector3.Distance(member.moveStartPos, member.moveTargetPos);
                member.startTime = Time.time;
                member.WasMovingToExit = shouldLeave;
            }

            if (member.journeyLength > 0.001f)
            {
                float distanceCovered = (Time.time - member.startTime) * moveSpeed;
                float fractionOfJourney = Mathf.Clamp01(distanceCovered / member.journeyLength);

                // Posición lineal base entre origen y destino
                Vector3 currentPos = Vector3.Lerp(member.moveStartPos, member.moveTargetPos, fractionOfJourney);

                // Añadimos múltiples saltitos usando el valor absoluto de la función Seno basado en la distancia recorrida
                float currentHop = Mathf.Abs(Mathf.Sin(distanceCovered * hopFrequency)) * hopHeight;
                currentPos.y += currentHop;

                member.transform.position = currentPos;
            }
            else
            {
                member.transform.position = targetPosition;
            }

            // Actualizar estado de llegada
            if (shouldLeave)
            {
                if (Vector3.Distance(member.transform.position, exit.position) <= reachThreshold)
                {
                    member.hasLeft = true;
                }
            }
            else
            {
                if (Vector3.Distance(member.transform.position, member.initialPosition) <= reachThreshold)
                {
                    member.hasLeft = false;
                }
            }
        }
        if (audienceManager != null)
        {
            SetScore(audienceManager.currentAudience);
        }
    }

    /// <summary>
    /// Método público para actualizar la puntuación desde otro script externo.
    /// </summary>
    public void SetScore(float newScore)
    {
        score = Mathf.Clamp(newScore, 0f, 100f);
    }
}