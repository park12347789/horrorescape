#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PortfolioBuildExporter
{
    private const string BuildRoot = "Builds/Portfolio/HorrorStealth_Windows";
    private const string ExecutableName = "HorrorStealth.exe";
    private const string ReportPath = "Builds/Portfolio/HorrorStealth_Windows_BuildReport.txt";

    [MenuItem("Tools/Portfolio/Build Windows 64-bit")]
    public static void BuildWindows64()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve project root.");
        string buildDirectory = Path.Combine(projectRoot, BuildRoot);
        string executablePath = Path.Combine(buildDirectory, ExecutableName);
        string reportPath = Path.Combine(projectRoot, ReportPath);

        if (Directory.Exists(buildDirectory))
        {
            Directory.Delete(buildDirectory, true);
        }

        Directory.CreateDirectory(buildDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)
            ?? throw new InvalidOperationException("Could not resolve report directory."));

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        string reportText =
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
            $"Result: {summary.result}{Environment.NewLine}" +
            $"Target: {summary.platform}{Environment.NewLine}" +
            $"Output: {executablePath}{Environment.NewLine}" +
            $"TotalSizeBytes: {summary.totalSize}{Environment.NewLine}" +
            $"Scenes:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", scenes)}{Environment.NewLine}";
        File.WriteAllText(reportPath, reportText);

        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Portfolio build failed: {summary.result}. See {reportPath}");
        }

        Debug.Log($"Portfolio build succeeded: {executablePath}");
    }
}
#endif
