using UnityEngine;

// 3. Instrumento equivocado (Trompeta de plástico)
[CreateAssetMenu(menuName = "Fantasma/Acciones/3. Instrumento Equivocado")]
public class ActionWrongInstrument : SabotageAction
{
    public GameObject fakeInstrumentPrefab;

    public override void Execute(GhostEntity ghost, Musician target)
    {
        target.SwapToFakeInstrument(fakeInstrumentPrefab);
        Debug.Log($"{target.name} tiene intrumento que no es, bromita");
    }
}
