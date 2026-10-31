/// <summary>
/// Opcional. Implementar en los sabotajes que produzcan efectos persistentes
/// (pitch, animacion, etc.) para deshacerlos al golpear con la batuta.
/// </summary>
public interface IGhostReversibleSabotage
{
    void RevertSabotage(Musician musician);
}
