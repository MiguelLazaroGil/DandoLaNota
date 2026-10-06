using UnityEngine;

public abstract class MusicoAccion : MonoBehaviour
{
    [Header("Configuración de Acción")]
    [SerializeField] private float energyCost = 10f;
    [SerializeField] protected float baseUtility = 50f;
 
    [Header("Cooldown / Penalización Individual")]
    [SerializeField] private float individualCooldownDuration = 6f;
    [SerializeField, Range(0f, 1f)] private float minIndividualUtilityMultiplier = 0.1f;

    private float lastTimeUsed = -999f;

    public void MarkAsUsed()
    {
        lastTimeUsed = Time.time;
    }

    public float GetIndividualUtilityMultiplier()
    {
        float timeSinceUse = Time.time - lastTimeUsed;
        if (timeSinceUse >= individualCooldownDuration) return 1.0f;

        // Recuperación progresiva de la utilidad
        float progress = timeSinceUse / individualCooldownDuration;
        return Mathf.Lerp(minIndividualUtilityMultiplier, 1.0f, progress);
    }

    public float EnergyCost => energyCost;

    // ¿La acción es físicamente posible según el contexto del enemigo? (distancia, raycast, etc.)
    public abstract bool IsValid(MusicoController owner);

    // Utilidad bruta calculada por la situación local del enemigo
    public abstract float GetRawUtility(MusicoController owner);

    // Lógica real de la acción
    public abstract void Execute(MusicoController owner);
}