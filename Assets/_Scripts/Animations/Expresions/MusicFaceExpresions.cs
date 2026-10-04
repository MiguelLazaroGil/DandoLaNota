using UnityEngine;

public class MusicFaceExpresions : MonoBehaviour
{
    public enum FaceExpresions { Iddle, Blink, Angry, Focus, Happy, Sad }
    [SerializeField]
    private Renderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    [SerializeField]
    public FaceExpresions faceExpression;
    [SerializeField]
    public float XUVStep = 0.2656f;
    [SerializeField]
    public float YUVStep = 0.3594f;
    [SerializeField]
    [Tooltip("X= Number of rows Y = number of columns")]
    public Vector2Int dimensions = new Vector2Int(2, 4);
    [SerializeField] private string texturePropertyName = "_BaseMap";

    // Permite probarlo también desde el menú contextual en tiempo de edición
    [ContextMenu("Apply Expression")]
    public void ApplyExpressionOffset()
    {

        if (meshRenderer == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        int index = (int)faceExpression;
        int columns = dimensions.y; // 4 columnas en tu caso

        // Cálculo de la columna y la fila según el índice del Enum
        int column = index % columns;
        int row = index / columns;

        // Desplazamiento X e Y
        float offsetX = column * XUVStep;
        // En Unity las UV crecen hacia arriba, por lo que bajar de fila resta en Y
        float offsetY = -row * YUVStep;


        string stPropertyName = texturePropertyName + "_ST";

        // Obtenemos el Tiling actual (X, Y) para no sobrescribir la escala de la textura
        Vector4 currentST = Vector4.one;

        if (meshRenderer.sharedMaterial != null && meshRenderer.sharedMaterial.HasProperty(stPropertyName))
        {
            currentST = meshRenderer.sharedMaterial.GetVector(stPropertyName);
        }

        // Vector4(Tiling.x, Tiling.y, Offset.x, Offset.y)
        Vector4 newST = new Vector4(currentST.x, currentST.y, offsetX, offsetY);

        // Se lee el bloque actual del Renderer, se actualiza la propiedad y se reasigna
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(stPropertyName, newST);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
