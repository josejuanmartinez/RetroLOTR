using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;

public class UITutorialObjectivesManager : MonoBehaviour
{
    public static UITutorialObjectivesManager Instance { get; private set; }

    public CanvasGroup canvasGroup;
    public GameObject tutorialObjectivePrefab;
    public Transform verticalLayout;

    private readonly Dictionary<string, GameObject> objectivesById = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        UpdateCanvasVisibility();
        TutorialManager.Instance?.RefreshObjectiveUI();
    }

    public void AddObjective(string id, string text)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        if (objectivesById.TryGetValue(id, out GameObject existing))
        {
            SetObjectiveText(existing, text);
            return;
        }

        if (tutorialObjectivePrefab == null || verticalLayout == null) return;

        GameObject objective = Instantiate(tutorialObjectivePrefab, verticalLayout);
        objective.name = id;
        objectivesById[id] = objective;
        SetObjectiveText(objective, text);
        UpdateCanvasVisibility();
    }

    public void RemoveObjective(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (!objectivesById.TryGetValue(id, out GameObject objective)) return;

        objectivesById.Remove(id);
        if (objective != null) Destroy(objective);
        UpdateCanvasVisibility();
    }

    public void ClearObjectives()
    {
        if (objectivesById.Count == 0)
        {
            UpdateCanvasVisibility();
            return;
        }

        foreach (GameObject objective in objectivesById.Values)
        {
            if (objective != null) Destroy(objective);
        }

        objectivesById.Clear();
        UpdateCanvasVisibility();
    }

    private void SetObjectiveText(GameObject objective, string text)
    {
        if (objective == null) return;
        TextMeshProUGUI label = objective.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = text ?? string.Empty;
    }

    private void UpdateCanvasVisibility()
    {
        if (canvasGroup == null) return;
        bool hasObjectives = objectivesById.Count > 0;
        canvasGroup.alpha = hasObjectives ? 1f : 0f;
        canvasGroup.interactable = hasObjectives;
        canvasGroup.blocksRaycasts = hasObjectives;
    }
}
