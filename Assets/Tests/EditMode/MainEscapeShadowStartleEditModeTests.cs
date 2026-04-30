using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class MainEscapeShadowStartleEditModeTests
{
    [Test]
    public void Marker_ConsumeTrigger_BlocksOneShotUntilReset()
    {
        GameObject playerRoot = new("Player");
        GameObject markerRoot = new("ShadowStartle_01");

        try
        {
            Component player = CreateProjectComponent(playerRoot, "WasdPlayerController");
            Component marker = CreateProjectComponent(markerRoot, "MainEscapeShadowStartleMarker");

            playerRoot.transform.position = new Vector3(1f, 0f, 0f);
            markerRoot.transform.position = Vector3.zero;

            Assert.That(
                InvokeBool(marker, "CanTrigger", new[] { player.GetType(), ResolveProjectType("FlashlightFogOfWarOverlay") }, player, null),
                Is.True,
                "Fresh one-shot marker should be triggerable.");

            InvokeVoid(marker, "ConsumeTrigger");

            Assert.That(
                InvokeBool(marker, "CanTrigger", new[] { player.GetType(), ResolveProjectType("FlashlightFogOfWarOverlay") }, player, null),
                Is.False,
                "Consumed one-shot marker should block until reset.");

            InvokeVoid(marker, "ResetSceneResidencyState");

            Assert.That(
                InvokeBool(marker, "CanTrigger", new[] { player.GetType(), ResolveProjectType("FlashlightFogOfWarOverlay") }, player, null),
                Is.True,
                "Reset should make the marker triggerable again.");
        }
        finally
        {
            UnityObject.DestroyImmediate(playerRoot);
            UnityObject.DestroyImmediate(markerRoot);
        }
    }

    [Test]
    public void FloorAuthoring_ResolvesShadowStartleMarkers_FromScareMarkersRoot()
    {
        GameObject floorRoot = new("FloorRoot");

        try
        {
            Component floorAuthoring = CreateProjectComponent(floorRoot, "MainEscapeFloorAuthoring");
            GameObject authoringMarkers = new("AuthoringMarkers");
            GameObject scareMarkers = new("ScareMarkers");
            GameObject markerA = new("ShadowStartle_01");
            GameObject markerB = new("ShadowStartle_02");

            authoringMarkers.transform.SetParent(floorRoot.transform, false);
            scareMarkers.transform.SetParent(authoringMarkers.transform, false);
            markerA.transform.SetParent(scareMarkers.transform, false);
            markerB.transform.SetParent(scareMarkers.transform, false);
            CreateProjectComponent(markerA, "MainEscapeShadowStartleMarker");
            CreateProjectComponent(markerB, "MainEscapeShadowStartleMarker");

            InvokeVoid(floorAuthoring, "CacheReferencesFromHierarchy");
            Array resolvedMarkers = InvokeArray(floorAuthoring, "GetShadowStartleMarkers");

            Assert.That(resolvedMarkers, Is.Not.Null);
            Assert.That(resolvedMarkers.Length, Is.EqualTo(2));
            Assert.That(resolvedMarkers.GetValue(0), Is.Not.Null);
            Assert.That(resolvedMarkers.GetValue(1), Is.Not.Null);
        }
        finally
        {
            UnityObject.DestroyImmediate(floorRoot);
        }
    }

    [Test]
    public void ShadowCue_UsesGroundEnemyBindingShape_ForPresentation()
    {
        GameObject cueRoot = new("ShadowCue");
        GameObject playerRoot = new("Player");
        GameObject markerRoot = new("ShadowStartle_01");

        try
        {
            Component cue = CreateProjectComponent(cueRoot, "RShadowStartleCue");
            Component player = CreateProjectComponent(playerRoot, "WasdPlayerController");
            Component marker = CreateProjectComponent(markerRoot, "MainEscapeShadowStartleMarker");

            markerRoot.transform.position = Vector3.zero;
            playerRoot.transform.position = new Vector3(1f, 0f, 0f);
            InvokeVoid(
                cue,
                "Configure",
                new[] { marker.GetType(), player.GetType() },
                marker,
                player);

            Component bindings = cueRoot.GetComponent(ResolveProjectType("EnemyPrefabBindings"));
            Assert.That(bindings, Is.Not.Null, "Cue should expose the same presentation binding shape as ground enemies.");

            Transform visualRoot = GetPropertyValue<Transform>(bindings, "VisualRoot");
            SpriteRenderer bodyRenderer = GetPropertyValue<SpriteRenderer>(bindings, "BodyRenderer");

            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(bodyRenderer, Is.Not.Null);
            Assert.That(visualRoot.name, Is.EqualTo("VisualRoot"));
            Assert.That(bodyRenderer.gameObject.name, Is.EqualTo("BodyArtwork"));
        }
        finally
        {
            UnityObject.DestroyImmediate(cueRoot);
            UnityObject.DestroyImmediate(playerRoot);
            UnityObject.DestroyImmediate(markerRoot);
        }
    }

    [Test]
    public void RuntimeContracts_ExposeFootstepDrivenBehindPlayerStartleHooks()
    {
        Type playerAudioType = ResolveProjectType("PrototypePlayerAudio");
        Type directorType = ResolveProjectType("RShadowStartleDirector");
        Type cueType = ResolveProjectType("RShadowStartleCue");
        Type requestType = ResolveProjectType("ShadowStartleCueRequest");

        Assert.That(playerAudioType.GetEvent("FootstepPlayed"), Is.Not.Null, "PrototypePlayerAudio should expose a footstep event.");
        Assert.That(directorType.GetField("footstepBehindPlayerStartleChance", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        Assert.That(directorType.GetMethod("HandlePlayerFootstep", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        Assert.That(cueType.GetMethod("Configure", new[] { requestType }), Is.Not.Null, "RShadowStartleCue should support runtime cue requests.");
    }

    private static Component CreateProjectComponent(GameObject root, string typeName)
    {
        Type type = ResolveProjectType(typeName);
        return root.AddComponent(type);
    }

    private static Type ResolveProjectType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Expected project type '{typeName}' in Assembly-CSharp.");
        return type;
    }

    private static void InvokeVoid(Component target, string methodName)
    {
        InvokeVoid(target, methodName, Type.EmptyTypes);
    }

    private static void InvokeVoid(Component target, string methodName, Type[] parameterTypes, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, parameterTypes);
        Assert.That(method, Is.Not.Null, $"Expected {target.GetType().Name}.{methodName} signature.");
        method.Invoke(target, args);
    }

    private static bool InvokeBool(Component target, string methodName, Type[] parameterTypes, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, parameterTypes);
        Assert.That(method, Is.Not.Null, $"Expected {target.GetType().Name}.{methodName} signature.");
        return (bool)method.Invoke(target, args);
    }

    private static Array InvokeArray(Component target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, Type.EmptyTypes);
        Assert.That(method, Is.Not.Null, $"Expected {target.GetType().Name}.{methodName} signature.");
        return (Array)method.Invoke(target, null);
    }

    private static T GetPropertyValue<T>(Component target, string propertyName)
        where T : class
    {
        var property = target.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"Expected {target.GetType().Name}.{propertyName} property.");
        return property.GetValue(target) as T;
    }
}
