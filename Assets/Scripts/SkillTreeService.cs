using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class SkillTreeDefinition
{
    public int version;
    public string id;
    public List<string> autoUnlockNodes = new();
    public List<SkillTreeNode> nodes = new();
}

[Serializable]
public class SkillTreeNode
{
    public string id;
    public string name;
    public string role;
    public int tier;
    public int cost;
    public List<string> requiresAll = new();
    public List<string> requiresAny = new();
    public List<string> unlocksActions = new();
}

public static class SkillTreeService
{
    private static SkillTreeDefinition cachedDefinition;
    private static Dictionary<string, SkillTreeNode> nodesById;
    private static Dictionary<string, SkillTreeNode> nodesByAction;
    private static bool loaded;

    public static SkillTreeDefinition GetDefinition()
    {
        EnsureLoaded();
        return cachedDefinition;
    }

    public static bool ShouldUseSkillTree(Character character)
    {
        SkillTreeDefinition tree = GetDefinition();
        return tree != null && tree.nodes != null && tree.nodes.Count > 0;
    }

    public static void InitializeCharacter(Character character)
    {
        if (character == null) return;
        SkillTreeDefinition tree = GetDefinition();
        if (tree == null) return;

        if (ShouldAutoUnlockAllNodes(character))
        {
            UnlockAllNodes(character);
            return;
        }

        if (tree.autoUnlockNodes != null)
        {
            foreach (string nodeId in tree.autoUnlockNodes)
            {
                character.UnlockSkillNode(nodeId, ignoreRequirements: true);
            }
        }

        UnlockRoleRoots(character);
    }

    public static bool IsActionUnlocked(Character character, string actionClassName)
    {
        if (character == null) return true;
        if (!ShouldUseSkillTree(character)) return true;
        SkillTreeNode node = GetNodeForAction(actionClassName);
        if (node == null) return false;
        return character.IsSkillNodeUnlocked(node.id);
    }

    public static string GetNodeNameForAction(string actionClassName)
    {
        SkillTreeNode node = GetNodeForAction(actionClassName);
        return node != null && !string.IsNullOrWhiteSpace(node.name) ? node.name : null;
    }

    public static bool CanUnlockNode(Character character, string nodeId)
    {
        if (character == null || string.IsNullOrWhiteSpace(nodeId)) return false;
        SkillTreeNode node = GetNodeById(nodeId);
        if (node == null) return false;
        if (character.IsSkillNodeUnlocked(nodeId)) return true;

        if (node.requiresAll != null)
        {
            foreach (string req in node.requiresAll)
            {
                if (!character.IsSkillNodeUnlocked(req)) return false;
            }
        }

        if (node.requiresAny != null && node.requiresAny.Count > 0)
        {
            bool anyMet = node.requiresAny.Any(character.IsSkillNodeUnlocked);
            if (!anyMet) return false;
        }

        return true;
    }

    public static void UnlockAllNodes(Character character)
    {
        SkillTreeDefinition tree = GetDefinition();
        if (tree?.nodes == null || character == null) return;
        foreach (SkillTreeNode node in tree.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id)) continue;
            character.UnlockSkillNode(node.id, ignoreRequirements: true);
        }
    }

    private static SkillTreeNode GetNodeById(string nodeId)
    {
        EnsureLoaded();
        if (nodesById == null || string.IsNullOrWhiteSpace(nodeId)) return null;
        return nodesById.TryGetValue(nodeId, out SkillTreeNode node) ? node : null;
    }

    private static SkillTreeNode GetNodeForAction(string actionClassName)
    {
        EnsureLoaded();
        if (nodesByAction == null || string.IsNullOrWhiteSpace(actionClassName)) return null;
        return nodesByAction.TryGetValue(actionClassName, out SkillTreeNode node) ? node : null;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        TextAsset json = Resources.Load<TextAsset>("SkillTree");
        if (json == null)
        {
            cachedDefinition = null;
            nodesById = null;
            nodesByAction = null;
            return;
        }

        cachedDefinition = JsonUtility.FromJson<SkillTreeDefinition>(json.text);
        nodesById = new Dictionary<string, SkillTreeNode>(StringComparer.OrdinalIgnoreCase);
        nodesByAction = new Dictionary<string, SkillTreeNode>(StringComparer.OrdinalIgnoreCase);

        if (cachedDefinition?.nodes == null) return;

        foreach (SkillTreeNode node in cachedDefinition.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id)) continue;
            node.requiresAll ??= new List<string>();
            node.requiresAny ??= new List<string>();
            node.unlocksActions ??= new List<string>();

            if (!nodesById.ContainsKey(node.id))
            {
                nodesById[node.id] = node;
            }

            foreach (string action in node.unlocksActions)
            {
                if (string.IsNullOrWhiteSpace(action)) continue;
                if (!nodesByAction.ContainsKey(action))
                {
                    nodesByAction[action] = node;
                }
            }
        }
    }

    private static bool ShouldAutoUnlockAllNodes(Character character)
    {
        if (character == null) return false;
        if (character is NonPlayableLeader nonPlayable && !nonPlayable.joined) return true;
        Leader owner = character.GetOwner();
        if (owner is NonPlayableLeader nonPlayableOwner && !nonPlayableOwner.joined) return true;
        Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
        return owner is PlayableLeader && game != null && game.player != null && game.player != owner;
    }

    private static void UnlockRoleRoots(Character character)
    {
        if (character == null) return;
        if (character.GetCommander() > 0) character.UnlockSkillNode("commander_root", ignoreRequirements: true);
        if (character.GetAgent() > 0) character.UnlockSkillNode("agent_root", ignoreRequirements: true);
        if (character.GetEmmissary() > 0) character.UnlockSkillNode("emmissary_root", ignoreRequirements: true);
        if (character.GetMage() > 0) character.UnlockSkillNode("mage_root", ignoreRequirements: true);
    }

}
