using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Entry point for the build_and_release skill (.agents/skills/build_and_release).
// Invoked in batch mode via: -executeMethod ReleaseBuilder.BuildWindows
public static class ReleaseBuilder
{
    private const string OutputDir = "Builds/Windows";
    private const string ExeName = "Runeboard.exe";

    [MenuItem("Build/Build Windows Release")]
    public static void BuildWindows()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new Exception("No enabled scenes in Build Settings — cannot build.");

        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), OutputDir);
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, true);
        Directory.CreateDirectory(outputDir);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[ReleaseBuilder] result={summary.result} size={summary.totalSize}B errors={summary.totalErrors} warnings={summary.totalWarnings}");

        if (summary.result != BuildResult.Succeeded)
            throw new Exception($"[ReleaseBuilder] Build failed: result={summary.result}, errors={summary.totalErrors}");
    }
}
