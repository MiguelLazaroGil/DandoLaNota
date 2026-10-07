using System.Collections.Generic;
using UnityEngine;
using System;

public class FantasmasIA : MonoBehaviour
{

    [Header("--- PERIODO DE GRACIA ---")]
    [Tooltip("Tiempo en segundos al inicio del nivel en el que NO se ejecutarán acciones.")]
    [SerializeField] private float initialQuietTime = 5.0f;

    [Header("--- ENERGÍA GLOBAL ---")]
    [SerializeField] private float currentEnergy = 0f;
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float initialEnergyRate = 2f;
    [SerializeField] private float energyGrowthRate = 0.05f;

    [Header("--- MODO OLA / OVERDRIVE ---")]
    [Tooltip("Porcentaje de energía máxima (0.8 a 1.0) necesario para activar la ola.")]
    [SerializeField, Range(0.8f, 1f)] private float overdriveTriggerPercentage = 0.95f;

    [Tooltip("Multiplicador del tiempo de espera entre acciones durante la ola (ej. 0.3 = 70% más rápido).")]
    [SerializeField] private float overdriveDelayMultiplier = 0.3f;

    [Tooltip("Probabilidad de acciones consecutivas/ráfaga mientras dura la ola.")]
    [SerializeField, Range(0f, 1f)] private float overdriveConsecutiveChance = 0.6f;

    [Tooltip("Nivel de energía al que termina la ola y el director vuelve al estado normal.")]
    [SerializeField] private float overdriveEndEnergyThreshold = 10f;

    // Estado interno del modo furia
    [SerializeField,ReadOnly]
    private bool isOverdriveActive = false;
    public bool IsOverdriveActive => isOverdriveActive; // Útil si quieres conectar música o UI

    [Header("--- PACING Y SEPARACIÓN TEMPORAL ---")]
    [SerializeField] private float initialMinDelay = 3f;
    [SerializeField] private float absoluteMinDelay = 0.4f;
    [SerializeField] private float delayDecayRate = 0.02f;
    [SerializeField] private float randomDelayVariance = 1.0f;

    [Header("--- CONTROL DE RÁFAGAS Y REPETICIÓN ---")]
    [Tooltip("Probabilidad (0-1) de ejecutar una acción en ráfaga inmediatamente después de otra.")]
    [SerializeField, Range(0f, 1f)] private float consecutiveActionChance = 0.15f;
    [Tooltip("Si es verdadero, impide que el MISMO enemigo ejecute dos acciones seguidas.")]
    [SerializeField] private bool preventSameEnemyConsecutive = true;

    [Header("--- SELECCIÓN Y ALEATORIEDAD ---")]
    [Tooltip("Probabilidad (0 a 1) de elegir una acción ponderada por utilidad (ruleta) en vez de escoger siempre la de mayor utilidad de forma estricta.")]
    [SerializeField, Range(0f, 1f)] private float randomSelectionChance = 0.3f;

    [Tooltip("Si la utilidad final cae por debajo de este umbral, la acción se descarta.")]
    [SerializeField] private float minUtilityThreshold = 5f;

    [Header("--- PENALIZACIÓN GLOBAL POR TIPO DE ACCIÓN ---")]
    [Tooltip("Tiempo (segundos) que tarda una categoría de acción en recuperar su utilidad normal tras usarse.")]
    [SerializeField] private float categoryCooldownDuration = 10f;
    [Tooltip("Multiplicador de utilidad inmediatamente después de usar la acción (ej: 0.1 = 10% de la utilidad normal).")]
    [SerializeField, Range(0f, 1f)] private float minCategoryUtilityMultiplier = 0.1f;

    // Estado interno
    private float elapsedTime;
    private float actionTimer;
    private float targetDelay;
    private bool lastStepWasAction;

    private MusicoController lastEnemyWhoActed;

    public List<MusicoController> registeredEnemies = new List<MusicoController>();
    private readonly Dictionary<Type, float> categoryLastUsedTime = new Dictionary<Type, float>();



    private void Start()
    {
        CalculateNextDelay();
    }
    private void Update()
    {
        float dt = Time.deltaTime;
        elapsedTime += dt;

        // Regeneración normal de energía
        float effectiveEnergyRate = initialEnergyRate + (energyGrowthRate * elapsedTime);
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + effectiveEnergyRate * dt);

        // 1. CONTROL DE ENTRADA Y SALIDA DE OLA
        if (!isOverdriveActive && currentEnergy >= maxEnergy * overdriveTriggerPercentage)
        {
            StartOverdrive();
        }
        else if (isOverdriveActive && currentEnergy <= overdriveEndEnergyThreshold)
        {
            EndOverdrive();
        }

        if (elapsedTime < initialQuietTime) return;

        actionTimer += dt;

        if (actionTimer >= targetDelay)
        {
            TryExecuteUtilityAction();
        }
    }

    private void StartOverdrive()
    {
        isOverdriveActive = true;
        CalculateNextDelay();
        Debug.Log("<b>[DIRECTOR]</b> ¡INICIO DE OLA INTENSA!");
    }

    private void EndOverdrive()
    {
        isOverdriveActive = false;
        CalculateNextDelay();
        Debug.Log("<b>[DIRECTOR]</b> Fin de la ola. Volviendo a ritmo normal.");
    }
    // Estructura auxiliar para almacenar candidatos válidos
    private struct ActionCandidate
    {
        public MusicoAccion action;
        public MusicoController enemy;
        public float utility;
    }

    private void TryExecuteUtilityAction()
    {
        // ... (Mantener tu chequeo previo de ráfaga/timers) ...

        float activeBurstChance = isOverdriveActive ? overdriveConsecutiveChance : consecutiveActionChance;

        if (lastStepWasAction)
        {
            if (UnityEngine.Random.value > activeBurstChance)
            {
                lastStepWasAction = false;
                CalculateNextDelay();
                return;
            }
        }

        List<ActionCandidate> validCandidates = new List<ActionCandidate>();
        float totalUtilitySum = 0f;



        // 1. Recolectar todos los candidatos válidos
        for (int i = registeredEnemies.Count - 1; i >= 0; i--)
        {
            MusicoController enemy = registeredEnemies[i];
            if (enemy == null || !enemy.IsAvailable) continue;
            if (preventSameEnemyConsecutive && enemy == lastEnemyWhoActed) continue;

            foreach (var action in enemy.Actions)
            {
                if (action == null || action.EnergyCost > currentEnergy || !action.IsValid(enemy)) continue;

                // Ponderación: Utilidad base * Multiplicador Global Tipo * Multiplicador Individual
                float rawUtility = action.GetRawUtility(enemy);
                float globalMultiplier = GetCategoryUtilityMultiplier(action.GetType());
                float individualMultiplier = action.GetIndividualUtilityMultiplier();

                float finalUtility = rawUtility * globalMultiplier * individualMultiplier;

                if (finalUtility >= minUtilityThreshold)
                {
                    validCandidates.Add(new ActionCandidate
                    {
                        action = action,
                        enemy = enemy,
                        utility = finalUtility
                    });
                    totalUtilitySum += finalUtility;
                }
            }
        }

        if (validCandidates.Count == 0)
        {
            lastStepWasAction = false;
            return;
        }

        // 2. Elegir acción según el nivel de aleatoriedad
        ActionCandidate selectedCandidate;

        if (UnityEngine.Random.value < randomSelectionChance)
        {
            // SELECCIÓN POR RULETA PONDERADA (Mayor utilidad = Más probabilidades, pero no garantizado)
            selectedCandidate = SelectWeightedRandomCandidate(validCandidates, totalUtilitySum);
        }
        else
        {
            // SELECCIÓN STRICTA (Best Utility)
            selectedCandidate = SelectBestCandidate(validCandidates);
        }

        // 3. Ejecutar acción elegida
        currentEnergy -= selectedCandidate.action.EnergyCost;
        categoryLastUsedTime[selectedCandidate.action.GetType()] = Time.time; // Global
        selectedCandidate.action.MarkAsUsed();                                 // Individual del enemigo

        lastEnemyWhoActed = selectedCandidate.enemy;
        selectedCandidate.action.Execute(selectedCandidate.enemy);

        lastStepWasAction = true;
        CalculateNextDelay();
    }

    private ActionCandidate SelectBestCandidate(List<ActionCandidate> candidates)
    {
        ActionCandidate best = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].utility > best.utility)
                best = candidates[i];
        }
        return best;
    }

    private ActionCandidate SelectWeightedRandomCandidate(List<ActionCandidate> candidates, float totalUtility)
    {
        float randomPoint = UnityEngine.Random.Range(0f, totalUtility);
        float currentSum = 0f;

        foreach (var candidate in candidates)
        {
            currentSum += candidate.utility;
            if (randomPoint <= currentSum)
                return candidate;
        }

        return candidates[candidates.Count - 1];
    }
    private float GetCategoryUtilityMultiplier(Type category)
    {
        if (!categoryLastUsedTime.TryGetValue(category, out float lastUsed))
        {
            return 1.0f; // Nunca se ha usado, 100% de utilidad
        }

        float timeSinceUse = Time.time - lastUsed;
        if (timeSinceUse >= categoryCooldownDuration)
        {
            return 1.0f; // Ya pasó el cooldown global
        }

        // Transición lineal desde minCategoryUtilityMultiplier hasta 1.0f conforme pasa el tiempo
        float progress = timeSinceUse / categoryCooldownDuration;
        return Mathf.Lerp(minCategoryUtilityMultiplier, 1.0f, progress);
    }
    private void CalculateNextDelay()
    {
        actionTimer = 0f;

        float currentMinDelay = Mathf.Max(absoluteMinDelay, initialMinDelay - (delayDecayRate * elapsedTime));
        float noise = UnityEngine.Random.Range(0f, randomDelayVariance);
        float baseDelay = currentMinDelay + noise;

        // Si la ola está activa, reducimos el delay multiplicándolo por overdriveDelayMultiplier
        if (isOverdriveActive)
        {
            targetDelay = Mathf.Max(absoluteMinDelay, baseDelay * overdriveDelayMultiplier);
        }
        else
        {
            targetDelay = baseDelay;
        }
    }

}