using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class MainEscapeChaseThreatReadabilityPlayModeSmokeTests
{
    private const string GameplaySceneName = "RMainScene_5F";
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const float ThreatCueIntensityFloor = 0.20f;
    private const float VisibleAlphaFloor = 0.08f;

    [UnityTest]
    public IEnumerator GameplayScene_ThreatFeedbackInfrastructure_IsPresent()
    {
        yield return LoadGameplaySceneOrInconclusive();

        Component threatHud = FindSceneComponentByTypeName("IRPlayerThreatHudBinder");
        Component threatPanel = FindSceneComponentByTypeName("IRThreatPanelView");

        Assert.That(threatHud, Is.Not.Null, "IRPlayerThreatHudBinder is missing in gameplay scene.");
        Assert.That(threatPanel, Is.Not.Null, "IRThreatPanelView is missing in gameplay scene.");
        Assert.That(GetBoolProperty(threatPanel, "HasRenderableEdges"), Is.True, "IRThreatPanelView edges are not configured.");

        Type registryType = FindTypeByName("PlayerThreatFeedbackRegistry");
        Assert.That(registryType, Is.Not.Null, "PlayerThreatFeedbackRegistry type is missing.");

        float endTime = Time.realtimeSinceStartup + 4f;

        while (Time.realtimeSinceStartup < endTime && GetThreatSourceCount(registryType) == 0)
        {
            yield return null;
        }

        Assert.That(GetThreatSourceCount(registryType), Is.GreaterThan(0), "Threat feedback source registry is empty.");
    }

    [UnityTest]
    public IEnumerator GameplayScene_ChasingEnemy_ExposesThreatCue_WhenChaseOccurs()
    {
        yield return LoadGameplaySceneOrInconclusive();

        Component threatPanel = FindSceneComponentByTypeName("IRThreatPanelView");
        Assert.That(threatPanel, Is.Not.Null, "IRThreatPanelView is missing in gameplay scene.");
        Assert.That(GetBoolProperty(threatPanel, "HasRenderableEdges"), Is.True, "IRThreatPanelView edges are not configured.");

        Component chasingEnemy = null;
        float waitUntil = Time.realtimeSinceStartup + 12f;

        while (Time.realtimeSinceStartup < waitUntil)
        {
            chasingEnemy = FindSceneComponentsByTypeName("EnemyStateMachine")
                .FirstOrDefault(enemy => string.Equals(GetPropertyValue(enemy, "CurrentState")?.ToString(), "Chase", StringComparison.Ordinal));

            if (chasingEnemy != null)
            {
                break;
            }

            yield return null;
        }

        if (chasingEnemy == null)
        {
            Assert.Inconclusive("No enemy entered Chase state during smoke window.");
        }

        float settleUntil = Time.realtimeSinceStartup + 0.35f;

        while (Time.realtimeSinceStartup < settleUntil)
        {
            yield return null;
        }

        bool isConfirmedThreat = GetBoolProperty(chasingEnemy, "IsConfirmedThreat");
        float threatIntensity = GetFloatProperty(chasingEnemy, "ThreatIntensityNormalized");
        bool shouldForceMarker = GetBoolProperty(chasingEnemy, "ShouldForceThreatFeedbackVisible");
        SpriteRenderer markerRenderer = GetPropertyValue<SpriteRenderer>(chasingEnemy, "ThreatMarkerRenderer");

        bool hasIntensityCue = isConfirmedThreat && threatIntensity >= ThreatCueIntensityFloor;
        bool hasForcedMarkerCue = shouldForceMarker && IsMarkerVisible(markerRenderer);

        Assert.That(
            hasIntensityCue || hasForcedMarkerCue,
            Is.True,
            $"Chasing enemy '{chasingEnemy.name}' did not expose a readable threat cue (intensity={threatIntensity:0.00}, forceMarker={shouldForceMarker}).");
    }

    private static IEnumerator LoadGameplaySceneOrInconclusive()
    {
        string sceneName = ResolveGameplaySceneName();

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Assert.Inconclusive("Canonical gameplay scene name is not configured.");
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Assert.Inconclusive($"Scene '{sceneName}' is not loadable from current build settings.");
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        Assert.That(loadOperation, Is.Not.Null, $"Could not start loading scene '{sceneName}'.");

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        yield return null;
    }

    private static string ResolveGameplaySceneName()
    {
        return GameplaySceneName;
    }

    private static IEnumerable<Component> FindSceneComponentsByTypeName(string typeName)
    {
        Type targetType = FindTypeByName(typeName);

        if (targetType == null)
        {
            return Array.Empty<Component>();
        }

        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<Component> matches = new();

        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour != null && targetType.IsAssignableFrom(behaviour.GetType()))
            {
                matches.Add(behaviour);
            }
        }

        return matches;
    }

    private static Component FindSceneComponentByTypeName(string typeName)
    {
        return FindSceneComponentsByTypeName(typeName).FirstOrDefault();
    }

    private static int GetThreatSourceCount(Type registryType)
    {
        if (registryType == null)
        {
            return 0;
        }

        PropertyInfo sourcesProperty = registryType.GetProperty("Sources", StaticMemberFlags);
        object sources = sourcesProperty?.GetValue(null);

        if (sources is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        if (sources is IEnumerable<object> enumerable)
        {
            return enumerable.Count();
        }

        if (sources is System.Collections.IEnumerable untypedEnumerable)
        {
            int count = 0;

            foreach (object _ in untypedEnumerable)
            {
                count++;
            }

            return count;
        }

        return 0;
    }

    private static bool GetBoolProperty(object instance, string propertyName)
    {
        return GetPropertyValue(instance, propertyName) is bool value && value;
    }

    private static float GetFloatProperty(object instance, string propertyName)
    {
        return GetPropertyValue(instance, propertyName) is float value ? value : 0f;
    }

    private static T GetPropertyValue<T>(object instance, string propertyName) where T : class
    {
        return GetPropertyValue(instance, propertyName) as T;
    }

    private static object GetPropertyValue(object instance, string propertyName)
    {
        if (instance == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        PropertyInfo property = instance.GetType().GetProperty(propertyName, MemberFlags);
        return property?.GetValue(instance);
    }

    private static Type FindTypeByName(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int index = 0; index < assemblies.Length; index++)
        {
            Type found = assemblies[index].GetType(typeName, false);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsMarkerVisible(SpriteRenderer markerRenderer)
    {
        return markerRenderer != null
            && markerRenderer.enabled
            && markerRenderer.gameObject.activeInHierarchy
            && markerRenderer.color.a > VisibleAlphaFloor;
    }
}
