using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Genera un cono de malla que coincide con una Spot Light (rango, ángulo y color)
/// para usarlo con el shader Custom/FakeLightCone.
/// Ponlo como hijo de la Spot Light con posición y rotación locales a 0
/// (la Spot Light apunta hacia su +Z, igual que el cono).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SpotLightCone : MonoBehaviour
{
    [Header("Fuente")]
    [Tooltip("Spot Light de la que copiar rango, ángulo y color. Si está vacío, busca una en los padres.")]
    [SerializeField] Light spotLight;
    [SerializeField] bool copyColorFromLight = true;
    [Tooltip("Ocultar el cono cuando la luz está apagada")]
    [SerializeField] bool hideWhenLightOff = true;
    [Tooltip("Fracción del rango de la luz que ocupa el cono visible")]
    [SerializeField, Range(0.1f, 1f)] float rangeScale = 0.8f;

    [Header("Geometría manual (si no hay Spot Light)")]
    [SerializeField] float length = 5f;
    [SerializeField, Range(1f, 179f)] float angle = 30f;

    [Header("Forma")]
    [Tooltip("Radio en el origen (tamaño de la lente del foco)")]
    [SerializeField] float startRadius = 0.05f;
    [SerializeField, Range(8, 128)] int segments = 48;

    static readonly int ColorID = Shader.PropertyToID("_Color");

    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MaterialPropertyBlock mpb;

    float builtLength = -1f, builtAngle = -1f, builtStartRadius = -1f;
    int builtSegments = -1;

    void Reset()
    {
        spotLight = GetComponentInParent<Light>();
    }

    void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // El cono no debe hacer sombras ni recibir iluminación
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (spotLight == null) spotLight = GetComponentInParent<Light>();

        builtSegments = -1; // forzar reconstrucción
        Refresh();
    }

    void LateUpdate() => Refresh();

    void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }

    void Refresh()
    {
        float len = length;
        float ang = angle;

        bool hasSpot = spotLight != null && spotLight.type == LightType.Spot;
        if (hasSpot)
        {
            len = spotLight.range * rangeScale;
            ang = spotLight.spotAngle;
        }

        if (!Mathf.Approximately(len, builtLength) ||
            !Mathf.Approximately(ang, builtAngle) ||
            !Mathf.Approximately(startRadius, builtStartRadius) ||
            segments != builtSegments)
        {
            BuildMesh(len, ang);
        }

        if (hasSpot && hideWhenLightOff)
            meshRenderer.enabled = spotLight.enabled && spotLight.gameObject.activeInHierarchy;

        if (hasSpot && copyColorFromLight)
        {
            mpb ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorID, spotLight.color);
            meshRenderer.SetPropertyBlock(mpb);
        }
    }

    void BuildMesh(float len, float ang)
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "SpotLightCone" };
            mesh.hideFlags = HideFlags.DontSave;
        }

        int seg = Mathf.Max(3, segments);
        float endRadius = startRadius + Mathf.Tan(ang * 0.5f * Mathf.Deg2Rad) * len;
        float dr = endRadius - startRadius;

        var vertices = new Vector3[(seg + 1) * 2];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[seg * 6];

        for (int i = 0; i <= seg; i++)
        {
            float u = (float)i / seg;
            float a = u * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            var tangent = new Vector3(-dir.y, dir.x, 0f);

            Vector3 v0 = dir * startRadius;                         // en el foco
            Vector3 v1 = dir * endRadius + Vector3.forward * len;  // final del cono

            // Normal perpendicular a la pared inclinada del cono, hacia fuera
            Vector3 side = new Vector3(dir.x * dr, dir.y * dr, len);
            Vector3 n = Vector3.Cross(tangent, side).normalized;

            int k = i * 2;
            vertices[k] = v0;     normals[k] = n;     uvs[k] = new Vector2(u, 0f);
            vertices[k + 1] = v1; normals[k + 1] = n; uvs[k + 1] = new Vector2(u, 1f);
        }

        for (int i = 0, t = 0; i < seg; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
            triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;

        builtLength = len;
        builtAngle = ang;
        builtStartRadius = startRadius;
        builtSegments = segments;
    }
}