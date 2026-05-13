using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ArtifactRepository
{
    private static Dictionary<string, Artifact> catalog;
    private static List<Artifact> allArtifacts;

    public static void EnsureLoaded()
    {
        if (catalog != null) return;
        Load();
    }

    private static void Load()
    {
        catalog = new Dictionary<string, Artifact>(StringComparer.OrdinalIgnoreCase);
        allArtifacts = new List<Artifact>();

        TextAsset jsonFile = Resources.Load<TextAsset>("Artifacts");
        if (jsonFile == null)
        {
            Debug.LogError("ArtifactRepository: Artifacts.json not found in Resources.");
            return;
        }

        ArtifactCollection collection = JsonUtility.FromJson<ArtifactCollection>(jsonFile.text);
        if (collection?.artifacts == null) return;

        foreach (Artifact artifact in collection.artifacts)
        {
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.artifactName)) continue;
            if (!catalog.ContainsKey(artifact.artifactName))
            {
                catalog[artifact.artifactName] = artifact;
                allArtifacts.Add(artifact);
            }
        }
    }

    public static Artifact GetByName(string artifactName)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(artifactName) || catalog == null) return null;
        return catalog.TryGetValue(artifactName, out Artifact template) ? template?.Clone() : null;
    }

    public static Artifact GetTemplateByName(string artifactName)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(artifactName) || catalog == null) return null;
        return catalog.TryGetValue(artifactName, out Artifact template) ? template : null;
    }

    public static List<Artifact> GetAll()
    {
        EnsureLoaded();
        return allArtifacts ?? new List<Artifact>();
    }

    public static List<Artifact> GetAllClones()
    {
        EnsureLoaded();
        return allArtifacts?.Select(a => a?.Clone()).Where(a => a != null).ToList() ?? new List<Artifact>();
    }

    public static List<Artifact> GetUnused(HashSet<string> unavailableNames)
    {
        EnsureLoaded();
        if (allArtifacts == null) return new List<Artifact>();
        return allArtifacts
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.artifactName) && !unavailableNames.Contains(a.artifactName))
            .Select(a => a.Clone())
            .ToList();
    }

    public static int Count
    {
        get
        {
            EnsureLoaded();
            return allArtifacts?.Count ?? 0;
        }
    }
}
