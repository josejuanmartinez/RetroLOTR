using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the selected AnimatorController with one state per AnimationClip found
// alongside it, so every clip can be scrubbed by double-clicking its state in the
// Animator window during Play mode (Unity forces the Animator into whatever state
// you double-click, no parameters/transitions required). Meant for eyeballing a raw
// folder of Mixamo animation drops before any real transition graph gets built.
public static class PreviewAnimatorBuilder
{
    [MenuItem("Assets/RetroLOTR/Animation/Populate States From Folder", true)]
    private static bool ValidateBuild() => Selection.activeObject is AnimatorController;

    [MenuItem("Assets/RetroLOTR/Animation/Populate States From Folder")]
    private static void Build()
    {
        var controller = Selection.activeObject as AnimatorController;
        if (controller == null) return;

        string controllerPath = AssetDatabase.GetAssetPath(controller);
        string folder = System.IO.Path.GetDirectoryName(controllerPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(folder)) return;

        // Mixamo bakes every single-take FBX with the internal clip name "mixamo.com"
        // regardless of the source file name, so clip.name is useless for state naming.
        // Use the FBX's own file name instead — that's the descriptive part.
        var namedClips = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .Select(clip => (path, clip)))
            .GroupBy(pc => pc.path)
            .SelectMany(g =>
            {
                string baseName = Path.GetFileNameWithoutExtension(g.Key);
                var items = g.ToArray();
                // Rare case: a single FBX contains multiple clips - disambiguate with the clip name too.
                return items.Length == 1
                    ? new[] { (name: baseName, items[0].clip) }
                    : items.Select(pc => (name: $"{baseName} - {pc.clip.name}", pc.clip));
            })
            .OrderBy(nc => nc.name)
            .ToArray();

        if (namedClips.Length == 0)
        {
            Debug.LogWarning($"PreviewAnimatorBuilder: no AnimationClip assets found under {folder}.");
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(child.state);
        }

        AnimatorState defaultState = null;
        var usedNames = new HashSet<string>();
        foreach (var (name, clip) in namedClips)
        {
            string stateName = name;
            for (int suffix = 1; !usedNames.Add(stateName); suffix++)
            {
                stateName = $"{name} ({suffix})";
            }

            AnimatorState state = stateMachine.AddState(stateName);
            state.motion = clip;
            if (defaultState == null || stateName.Equals("standing idle", System.StringComparison.OrdinalIgnoreCase))
            {
                defaultState = state;
            }
        }
        if (defaultState != null) stateMachine.defaultState = defaultState;

        // Lay states out in a grid instead of stacked on top of each other in the graph view.
        const int columns = 8;
        const float spacingX = 220f;
        const float spacingY = 80f;
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            ChildAnimatorState s = states[i];
            s.position = new Vector3((i % columns) * spacingX, (i / columns) * spacingY, 0f);
            states[i] = s;
        }
        stateMachine.states = states;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"PreviewAnimatorBuilder: added {namedClips.Length} states to '{controller.name}' from {folder} (default: {defaultState?.name}).");
    }
}
