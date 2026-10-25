using UnityEngine;

public abstract class MusicoAccion : MonoBehaviour
{
    [Header("Configuraci�n de Acci�n")]
    [SerializeField] private float energyCost = 10f;
    [SerializeField] protected float baseUtility = 50f;
 
    [Header("Cooldown / Penalizaci�n Individual")]
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

        // Recuperaci�n progresiva de la utilidad
        float progress = timeSinceUse / individualCooldownDuration;
        return Mathf.Lerp(minIndividualUtilityMultiplier, 1.0f, progress);
    }

    public float EnergyCost => energyCost;

    // �La acci�n es f�sicamente posible seg�n el contexto del enemigo? (distancia, raycast, etc.)
    public abstract bool IsValid(Musician owner);

    // Utilidad bruta calculada por la situaci�n local del enemigo
    public abstract float GetRawUtility(Musician owner);

    // L�gica real de la acci�n
    public abstract void Execute(Musician owner);
}