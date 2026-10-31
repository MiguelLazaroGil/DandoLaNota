using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IA de sabotajes para single player: selecciona acciones, marca musicos
/// y recibe correcciones. Es independiente del tutorial y del input.
/// </summary>
public class FantasmasIA : MonoBehaviour
{
    [Header("Periodo de gracia")]
    [SerializeField, Min(0f)] private float initialQuietTime = 5f;

    [Header("Energia global")]
    [SerializeField] private float currentEnergy = 0f;
    [SerializeField, Min(1f)] private float maxEnergy = 100f;
    [SerializeField] private float initialEnergyRate = 2f;
    [SerializeField] private float energyGrowthRate = 0.05f;

    [Header("Overdrive")]
    [SerializeField, Range(0.8f, 1f)] private float overdriveTriggerPercentage = 0.95f;
    [SerializeField, Min(0.01f)] private float overdriveDelayMultiplier = 0.3f;
    [SerializeField, Range(0f, 1f)] private float overdriveConsecutiveChance = 0.6f;
    [SerializeField] private float overdriveEndEnergyThreshold = 10f;
    [SerializeField] private bool isOverdriveActive;
    public bool IsOverdriveActive => isOverdriveActive;

    [Header("Pacing")]
    [SerializeField, Min(0.1f)] private float initialMinDelay = 3f;
    [SerializeField, Min(0.01f)] private float absoluteMinDelay = 0.4f;
    [SerializeField, Min(0f)] private float delayDecayRate = 0.02f;
    [SerializeField, Min(0f)] private float randomDelayVariance = 1f;

    [Header("Rafagas y repeticion")]
    [SerializeField, Range(0f, 1f)] private float consecutiveActionChance = 0.15f;
    [SerializeField] private bool preventSameEnemyConsecutive = true;

    [Header("Seleccion por utilidad")]
    [SerializeField, Range(0f, 1f)] private float randomSelectionChance = 0.3f;
    [SerializeField] private float minUtilityThreshold = 5f;
    [SerializeField, Min(0f)] private float categoryCooldownDuration = 10f;
    [SerializeField, Range(0f, 1f)] private float minCategoryUtilityMultiplier = 0.1f;

    [Header("Control reutilizable")]
    [Tooltip("La IA sigue activa para recibir comandos cuando se desactiva el modo automatico.")]
    [SerializeField] private bool automaticActionsEnabled = true;
    [Tooltip("Limita cuantos musicos pueden estar marcados al mismo tiempo.")]
    [SerializeField, Min(1)] private int maxSimultaneousGhosts = 3;

    [Header("Musicos registrados")]
    public List<Musician> registeredEnemies = new List<Musician>();

    public bool AutomaticActionsEnabled => automaticActionsEnabled;
    public event Action<Musician, SabotageAction> OnSabotajeEjecutado;
    public event Action<Musician> OnMusicoCorregido;

    private float elapsedTime;
    private float actionTimer;
    private float targetDelay;
    private bool lastStepWasAction;
    private Musician lastEnemyWhoActed;
    private readonly Dictionary<Type, float> categoryLastUsedTime = new Dictionary<Type, float>();

    private struct ActionCandidate
    {
        public Musician musician;
        public GhostCorrectionTarget target;
        public SabotageAction action;
        public float utility;
    }

    private void Start()
    {
        CalculateNextDelay();
    }

    private void Update()
    {
        if (!automaticActionsEnabled || Time.timeScale == 0f)
            return;

        float dt = Time.deltaTime;
        elapsedTime += dt;
        float energyRate = initialEnergyRate + energyGrowthRate * elapsedTime;
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + energyRate * dt);

        if (!isOverdriveActive && currentEnergy >= maxEnergy * overdriveTriggerPercentage)
            StartOverdrive();
        else if (isOverdriveActive && currentEnergy <= overdriveEndEnergyThreshold)
            EndOverdrive();

        if (elapsedTime < initialQuietTime) return;

        actionTimer += dt;
        if (actionTimer >= targetDelay)
            TryExecuteUtilityAction();
    }

    /// <summary>Detiene/reanuda solo la seleccion automatica. Sirve en tutorial y eventos guionizados.</summary>
    public void SetAutomaticActionsEnabled(bool enabled)
    {
        automaticActionsEnabled = enabled;
        CalculateNextDelay();
    }

    /// <summary>
    /// Ejecuta exactamente UNA accion. Con musico=null elige UN musico aleatorio
    /// valido; con musico asignado, solo ese. -1 elige la accion mas util.
    /// No consume energia, no aplica cooldown y no reactiva la IA automatica.
    /// Retorna el musico afectado o null si no pudo ejecutarse.
    /// </summary>
    public Musician EjecutarUnSabotaje(Musician musico = null, int indiceAccion = -1)
    {
        if (CountActiveGhosts() >= maxSimultaneousGhosts)
        {
            Debug.LogWarning("FantasmasIA: se alcanzo el limite de musicos saboteados.");
            return null;
        }

        if (musico != null)
        {
            if (!TryGetTarget(musico, out GhostCorrectionTarget target)) return null;
            SabotageAction accion = ChooseManualAction(musico, indiceAccion);
            return ApplySabotage(musico, target, accion, false) ? musico : null;
        }

        // Randomizado ENTRE candidatos elegibles: un solo musico, una sola accion.
        List<Musician> valid = new List<Musician>();
        foreach (Musician candidate in registeredEnemies)
        {
            if (TryGetTarget(candidate, out _) && ChooseManualAction(candidate, indiceAccion) != null)
                valid.Add(candidate);
        }
        if (valid.Count == 0)
        {
            Debug.LogWarning("FantasmasIA: no hay musicos elegibles para sabotaje controlado.");
            return null;
        }

        Musician selected = valid[UnityEngine.Random.Range(0, valid.Count)];
        TryGetTarget(selected, out GhostCorrectionTarget selectedTarget);
        SabotageAction selectedAction = ChooseManualAction(selected, indiceAccion);
        return ApplySabotage(selected, selectedTarget, selectedAction, false) ? selected : null;
    }

    /// <summary>Alias para compatibilidad con las llamadas anteriores del tutorial.</summary>
    public bool EjecutarSabotajeTutorial(Musician musico, int indiceAccion = -1)
    {
        return EjecutarUnSabotaje(musico, indiceAccion) != null;
    }

    /// <summary>Corrige el musico alcanzado por la batuta.</summary>
    public bool CorregirMusico(GhostCorrectionTarget target)
    {
        if (target == null || !target.TieneFantasma) return false;
        Musician musico = target.GetComponent<Musician>();
        if (musico == null || !registeredEnemies.Contains(musico)) return false;
        target.DesactivarSabotaje();

        OnMusicoCorregido?.Invoke(musico);
        return true;
    }

    public void RegisterMusician(Musician musico)
    {
        if (musico != null && !registeredEnemies.Contains(musico))
            registeredEnemies.Add(musico);
    }

    public void UnregisterMusician(Musician musico)
    {
        registeredEnemies.Remove(musico);
    }

    private bool TryGetTarget(Musician musico, out GhostCorrectionTarget target)
    {
        target = null;
        if (musico == null || !musico.IsAvailable || musico.availableActions == null)
            return false;

        target = musico.GetComponent<GhostCorrectionTarget>();
        if (target == null || target.TieneFantasma) return false;

        return true;
    }

    private SabotageAction ChooseManualAction(Musician musico, int index)
    {
        SabotageAction best = null;
        float bestUtility = float.NegativeInfinity;
        int current = 0;

        foreach (SabotageAction action in musico.availableActions)
        {
            if (index >= 0 && current != index) { current++; continue; }
            current++;
            if (action == null || action.playerOnly) continue;
            if (index >= 0) return action;

            float utility = action.GetRawUtility();
            if (utility > bestUtility)
            {
                bestUtility = utility;
                best = action;
            }
        }
        return best;
    }

    private void TryExecuteUtilityAction()
    {
        // Siempre reiniciar el temporizador, incluso si no hay candidatos.
        CalculateNextDelay();
        if (CountActiveGhosts() >= maxSimultaneousGhosts) return;

        float burstChance = isOverdriveActive ? overdriveConsecutiveChance : consecutiveActionChance;
        if (lastStepWasAction && UnityEngine.Random.value > burstChance)
        {
            lastStepWasAction = false;
            return;
        }

        List<ActionCandidate> candidates = new List<ActionCandidate>();
        float totalUtility = 0f;
        foreach (Musician musician in registeredEnemies)
        {
            if (!TryGetTarget(musician, out GhostCorrectionTarget target)) continue;
            if (preventSameEnemyConsecutive && musician == lastEnemyWhoActed) continue;

            foreach (SabotageAction action in musician.availableActions)
            {
                if (action == null || action.playerOnly || action.energyCost > currentEnergy) continue;
                float utility = action.GetRawUtility() * GetCategoryUtilityMultiplier(action.GetType());
                if (utility < minUtilityThreshold) continue;
                candidates.Add(new ActionCandidate { musician = musician, target = target, action = action, utility = utility });
                totalUtility += utility;
            }
        }

        if (candidates.Count == 0)
        {
            lastStepWasAction = false;
            return;
        }

        ActionCandidate selected;
        if (UnityEngine.Random.value < randomSelectionChance && totalUtility > 0f)
        {
            float choice = UnityEngine.Random.Range(0f, totalUtility);
            selected = candidates[candidates.Count - 1];
            foreach (ActionCandidate candidate in candidates)
            {
                choice -= candidate.utility;
                if (choice <= 0f) { selected = candidate; break; }
            }
        }
        else
        {
            selected = candidates[0];
            foreach (ActionCandidate candidate in candidates)
                if (candidate.utility > selected.utility) selected = candidate;
        }

        if (!ApplySabotage(selected.musician, selected.target, selected.action, true))
        {
            lastStepWasAction = false;
            return;
        }
        lastStepWasAction = true;
    }

    private int CountActiveGhosts()
    {
        int count = 0;
        foreach (Musician musician in registeredEnemies)
            if (musician != null && musician.TryGetComponent(out GhostCorrectionTarget target) && target.TieneFantasma)
                count++;
        return count;
    }

    private bool ApplySabotage(Musician musician, GhostCorrectionTarget target, SabotageAction action, bool spendEnergy)
    {
        if (target == null || target.TieneFantasma || action == null) return false;
        if (spendEnergy)
        {
            currentEnergy -= action.energyCost;
            categoryLastUsedTime[action.GetType()] = Time.time;
            lastEnemyWhoActed = musician;
        }

        action.ApplySabotagge(musician);
        target.ActivarSabotaje(action);

        OnSabotajeEjecutado?.Invoke(musician, action);
        return true;
    }

    private void StartOverdrive()
    {
        isOverdriveActive = true;
        CalculateNextDelay();
        Debug.Log("[FantasmasIA] INICIO OLA INTENSA");
    }

    private void EndOverdrive()
    {
        isOverdriveActive = false;
        CalculateNextDelay();
        Debug.Log("[FantasmasIA] Fin de la ola");
    }

    private float GetCategoryUtilityMultiplier(Type category)
    {
        if (!categoryLastUsedTime.TryGetValue(category, out float lastUsed)) return 1f;
        if (categoryCooldownDuration <= 0f) return 1f;
        float elapsed = Time.time - lastUsed;
        if (elapsed >= categoryCooldownDuration) return 1f;
        return Mathf.Lerp(minCategoryUtilityMultiplier, 1f, elapsed / categoryCooldownDuration);
    }

    private void CalculateNextDelay()
    {
        actionTimer = 0f;
        float baseDelay = Mathf.Max(absoluteMinDelay, initialMinDelay - delayDecayRate * elapsedTime);
        baseDelay += UnityEngine.Random.Range(0f, randomDelayVariance);
        targetDelay = isOverdriveActive
            ? Mathf.Max(absoluteMinDelay, baseDelay * overdriveDelayMultiplier)
            : baseDelay;
    }
    [ContextMenu("DEBUG/Probar sabotaje manual")]
    private void DebugProbarSabotaje()
    {
        Musician musico = EjecutarUnSabotaje();

        if (musico != null)
            Debug.Log($"[FantasmasIA] Sabotaje ejecutado en {musico.name}");
        else
            Debug.LogError("[FantasmasIA] No se encontró un músico válido.");
    }

    [ContextMenu("DEBUG/Diagnosticar musicos registrados")]
    private void DiagnosticarMusicos()
    {
        HashSet<Musician> musicosEncontrados = new HashSet<Musician>();

        Debug.Log(
            $"[FantasmasIA] Automatico: {automaticActionsEnabled}, " +
            $"Energia: {currentEnergy:F1}, " +
            $"Musicos registrados: {registeredEnemies.Count}"
        );

        for (int i = 0; i < registeredEnemies.Count; i++)
        {
            Musician musico = registeredEnemies[i];

            if (musico == null)
            {
                Debug.LogWarning($"Musico [{i}]: NULL");
                continue;
            }

            bool duplicado = !musicosEncontrados.Add(musico);

            GhostCorrectionTarget target =
                musico.GetComponent<GhostCorrectionTarget>();

            int accionesValidas = 0;
            int accionesAutomaticas = 0;

            if (musico.availableActions != null)
            {
                foreach (SabotageAction action in musico.availableActions)
                {
                    if (action == null || action.playerOnly)
                        continue;

                    accionesValidas++;

                    float utilidad = action.GetRawUtility() *
                        GetCategoryUtilityMultiplier(action.GetType());

                    if (action.energyCost <= currentEnergy &&
                        utilidad >= minUtilityThreshold)
                    {
                        accionesAutomaticas++;
                    }
                }
            }

            Debug.Log(
                $"[Musico {i}] {musico.name}\n" +
                $"Duplicado: {duplicado}\n" +
                $"Escena: {musico.gameObject.scene.name} | " +
                $"Escena valida: {musico.gameObject.scene.IsValid()}\n" +
                $"IsAvailable: {musico.IsAvailable}\n" +
                $"CurrentAction: {(musico.currentAction != null ? "OCUPADO" : "NULL")}\n" +
                $"IsPossessed: {musico.IsPossessed}\n" +
                $"GhostCorrectionTarget: {(target != null ? "SI" : "NO")}\n" +
                $"TieneFantasma: {(target != null && target.TieneFantasma)}\n" +
                $"Acciones validas: {accionesValidas}\n" +
                $"Acciones automaticas posibles: {accionesAutomaticas}",
                musico
            );
        }
    }

}
