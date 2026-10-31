using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Genera una copia invisible de cada mesh con material de contorno rojo.
/// No sustituye los materiales originales ni modifica la animacion del musico.
/// Funciona con MeshRenderer y SkinnedMeshRenderer.
/// </summary>
[DisallowMultipleComponent]
public class GhostOutlineVisual : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    [Tooltip("Dejar vacio para incluir todos los renderers hijos al inicializar.")]
    [SerializeField] private Renderer[] targetRenderers;

    private readonly List<Renderer> createdRenderers = new List<Renderer>();
    private bool built;

    private void Awake()
    {
        BuildIfNeeded();
        SetHighlighted(false);
    }

    public void SetHighlighted(bool visible)
    {
        BuildIfNeeded();
        foreach (Renderer outline in createdRenderers)
            if (outline != null) outline.enabled = visible;
    }

    private void BuildIfNeeded()
    {
        if (built) return;
        built = true;

        if (outlineMaterial == null)
        {
            Debug.LogWarning($"GhostOutlineVisual de {name}: asigna el material GhostOutline.");
            return;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer source in targetRenderers)
        {
            if (source == null) continue;
            Mesh mesh = null;
            if (source is SkinnedMeshRenderer skinSource)
                mesh = skinSource.sharedMesh;
            else if (source is MeshRenderer)
            {
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            if (mesh == null) continue;

            GameObject child = new GameObject("_GhostOutlineVisual");
            child.layer = source.gameObject.layer;
            child.transform.SetParent(source.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            Renderer outlineRenderer;
            if (source is SkinnedMeshRenderer skinned)
            {
                SkinnedMeshRenderer outlineSkin = child.AddComponent<SkinnedMeshRenderer>();
                outlineSkin.sharedMesh = mesh;
                outlineSkin.bones = skinned.bones;
                outlineSkin.rootBone = skinned.rootBone;
                outlineSkin.localBounds = skinned.localBounds;
                outlineSkin.updateWhenOffscreen = true;
                outlineRenderer = outlineSkin;
            }
            else
            {
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                outlineRenderer = child.AddComponent<MeshRenderer>();
            }

            // Un material por submesh, para no dejar partes sin contorno.
            Material[] mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = outlineMaterial;
            outlineRenderer.sharedMaterials = mats;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.enabled = false;
            createdRenderers.Add(outlineRenderer);
        }
    }
}
