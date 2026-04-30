#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RMainEscapeLoopSmokeBridge
{
    private const string ReportFileRelativePath = "Temp/RMainEscapeLoopSmokeReport.txt";
    private const string LogPrefix = "[RMainEscapeLoopSmokeBridge]";
    private const string TestAssemblyName = "HorrorStealth.Tests.PlayMode";
    private const string TestFullName = "MainEscapeFullLoopPlayModeTests.Lobby_RunProgressesFrom5FTo1F_ThenReturnsToLobby";

    [MenuItem("Tools/HorrorStealth/Run Main Escape Full Loop Smoke")]
    public static void RunFromMenu()
    {
        StartRunIfSafe();
    }

    public static bool StartRunIfSafe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WriteReport(
                "Result: ABORTED\nReason: Unity is already in a play mode transition, so the loop smoke test was not started.");
            Debug.LogWarning($"{LogPrefix} Aborted because Unity is already entering or exiting play mode.");
            return false;
        }

        if (HasDirtyLoadedScenes(out List<string> dirtySceneNames))
        {
            WriteReport(
                "Result: ABORTED\nReason: Dirty loaded scenes were detected, so the loop smoke test was not started.\n"
                + string.Join("\n", dirtySceneNames));
            Debug.LogWarning(
                $"{LogPrefix} Aborted because dirty loaded scenes are present: {string.Join(", ", dirtySceneNames)}");
            return false;
        }

        RunLoopSmokeTest();
        return true;
    }

    private static void RunLoopSmokeTest()
    {
        StringBuilder headerBuilder = new StringBuilder();
        headerBuilder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        headerBuilder.AppendLine("Result: RUNNING");
        headerBuilder.AppendLine($"Test: {TestFullName}");
        headerBuilder.AppendLine();
        WriteReport(headerBuilder.ToString());

        TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
        LoopSmokeCallbacks callbacks = ScriptableObject.CreateInstance<LoopSmokeCallbacks>();
        callbacks.Initialize(api, GetProjectRelativeAbsolutePath(ReportFileRelativePath));
        api.RegisterCallbacks(callbacks);

        Filter filter = new Filter
        {
            assemblyNames = new[] { TestAssemblyName },
            testNames = new[] { TestFullName },
            testMode = TestMode.PlayMode,
        };

        ExecutionSettings executionSettings = new ExecutionSettings(new[] { filter });
        api.Execute(executionSettings);
        Debug.Log($"{LogPrefix} Started loop smoke test '{TestFullName}'.");
    }

    private static bool HasDirtyLoadedScenes(out List<string> dirtySceneNames)
    {
        dirtySceneNames = new List<string>();

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.isLoaded && scene.isDirty)
            {
                dirtySceneNames.Add(scene.path);
            }
        }

        return dirtySceneNames.Count > 0;
    }

    private static void WriteReport(string text)
    {
        string reportPath = GetProjectRelativeAbsolutePath(ReportFileRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? throw new InvalidOperationException("Could not create the loop smoke report directory."));
        File.WriteAllText(reportPath, text, Encoding.UTF8);
    }

    private static string GetProjectRelativeAbsolutePath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the project root.");
        return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class LoopSmokeCallbacks : ScriptableObject, ICallbacks
    {
        private readonly StringBuilder reportBuilder = new StringBuilder();
        private TestRunnerApi ownerApi;
        private string reportPath;

        public void Initialize(TestRunnerApi api, string outputPath)
        {
            ownerApi = api;
            reportPath = outputPath;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            reportBuilder.Clear();
            reportBuilder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            reportBuilder.AppendLine("Result: RUNNING");
            reportBuilder.AppendLine($"Test: {TestFullName}");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Events:");
            reportBuilder.AppendLine("- Test run started.");
            Flush();
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            int totalCount = CountLeafResults(result);
            reportBuilder.Clear();
            reportBuilder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            reportBuilder.AppendLine($"Result: {(result.PassCount == totalCount && result.FailCount == 0 ? "PASS" : "FAIL")}");
            reportBuilder.AppendLine($"Test: {TestFullName}");
            reportBuilder.AppendLine($"Total: {totalCount}");
            reportBuilder.AppendLine($"Passed: {result.PassCount}");
            reportBuilder.AppendLine($"Failed: {result.FailCount}");
            reportBuilder.AppendLine($"Skipped: {result.SkipCount}");
            reportBuilder.AppendLine($"DurationSeconds: {result.Duration:F2}");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Failures:");

            if (result.FailCount == 0)
            {
                reportBuilder.AppendLine("- none");
            }
            else
            {
                AppendFailures(result);
            }

            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Summary:");
            reportBuilder.AppendLine($"- ResultState: {result.ResultState}");
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                reportBuilder.AppendLine($"- Message: {result.Message}");
            }

            Flush();
            Debug.Log($"{LogPrefix} Finished loop smoke test with result '{result.ResultState}'.");

            if (ownerApi != null)
            {
                ownerApi.UnregisterCallbacks(this);
            }
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }

        private void AppendFailures(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                reportBuilder.AppendLine($"- {result.Name}: {result.Message}");
                if (!string.IsNullOrWhiteSpace(result.StackTrace))
                {
                    reportBuilder.AppendLine(result.StackTrace);
                }

                return;
            }

            foreach (ITestResultAdaptor child in result.Children)
            {
                if (child.FailCount <= 0 && child.ResultState != "Failed")
                {
                    continue;
                }

                AppendFailures(child);
            }
        }

        private void Flush()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? throw new InvalidOperationException("Could not create the loop smoke report directory."));
            File.WriteAllText(reportPath, reportBuilder.ToString(), Encoding.UTF8);
        }

        private static int CountLeafResults(ITestResultAdaptor result)
        {
            if (result == null)
            {
                return 0;
            }

            if (!result.HasChildren)
            {
                return 1;
            }

            int total = 0;

            foreach (ITestResultAdaptor child in result.Children)
            {
                total += CountLeafResults(child);
            }

            return total;
        }
    }
}
#endif
