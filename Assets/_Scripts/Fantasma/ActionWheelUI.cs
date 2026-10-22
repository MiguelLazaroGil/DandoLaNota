using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActionWheelUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public GameObject wheelVisualContainer; // El Panel dentro de tu Canvas
    public Image iconPrefab;                // Un Prefab simple con un componente UI Image
    public float wheelRadius = 120f;        // Radio del círculo en píxeles

    [Header("Colores de Selección")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.6f);
    public Color selectedColor = Color.white;

    private List<SabotageAction> currentActions = new List<SabotageAction>();
    private List<Image> spawnedIcons = new List<Image>();
    private int selectedIndex = -1;

    public bool IsOpen { get; private set; }

    public void LoadActions(List<SabotageAction> actions)
    {
        currentActions = actions;
        RebuildWheelVisuals();
    }

    private void RebuildWheelVisuals()
    {
        // Limpiar iconos anteriores
        foreach (var img in spawnedIcons)
        {
            if (img != null) Destroy(img.gameObject);
        }
        spawnedIcons.Clear();

        if (currentActions == null || currentActions.Count == 0 || iconPrefab == null || wheelVisualContainer == null)
            return;

        float step = 360f / currentActions.Count;

        for (int i = 0; i < currentActions.Count; i++)
        {
            Image newIcon = Instantiate(iconPrefab, wheelVisualContainer.transform);
            newIcon.sprite = currentActions[i].icon;
            newIcon.color = normalColor;

            // Colocar cada icono en su ángulo correspondiente (centrado en el sector)
            float angleDeg = (i * step) + (step * 0.5f);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * wheelRadius;

            newIcon.rectTransform.anchoredPosition = pos;
            spawnedIcons.Add(newIcon);
        }
    }

    public void OpenWheel()
    {
        if (currentActions == null || currentActions.Count == 0) return;
        IsOpen = true;
        if (wheelVisualContainer != null) wheelVisualContainer.SetActive(true);
        selectedIndex = 0;
        HighlightSelectedIcon();
    }

    public void CloseWheel()
    {
        IsOpen = false;
        if (wheelVisualContainer != null) wheelVisualContainer.SetActive(false);
        selectedIndex = -1;
    }

    public void UpdateSelection(Vector2 inputDirection)
    {
        if (currentActions.Count == 0 || inputDirection.sqrMagnitude < 100f) return; // Zona muerta en el centro

        float angle = Mathf.Atan2(inputDirection.y, inputDirection.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        float step = 360f / currentActions.Count;
        int newIndex = Mathf.FloorToInt(angle / step);

        if (newIndex != selectedIndex)
        {
            selectedIndex = newIndex;
            HighlightSelectedIcon();
            Debug.Log($"Apuntando a [{selectedIndex}]: {currentActions[selectedIndex].actionName}");
        }
    }

    private void HighlightSelectedIcon()
    {
        for (int i = 0; i < spawnedIcons.Count; i++)
        {
            if (spawnedIcons[i] != null)
            {
                spawnedIcons[i].color = (i == selectedIndex) ? selectedColor : normalColor;
                spawnedIcons[i].transform.localScale = (i == selectedIndex) ? Vector3.one * 1.25f : Vector3.one;
            }
        }
    }

    public SabotageAction GetSelectedAction()
    {
        if (selectedIndex >= 0 && selectedIndex < currentActions.Count)
        {
            return currentActions[selectedIndex];
        }
        return null;
    }
}