/*
 * File Role:
 * Adds and configures 2D shadow caster data at runtime from collider shapes.
 *
 * Runtime Use:
 * Lets generated walls, doors, and props cast flashlight shadows without hand-authored prefabs.
 *
 * Study Notes:
 * Good reference for understanding how the project bridges procedural geometry and URP shadows.
 */

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

public static class RuntimeShadowCaster2DConfigurator
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly Type ColliderProviderType =
        typeof(ShadowCaster2D).Assembly.GetType("UnityEngine.Rendering.Universal.ShadowShape2DProvider_Collider2D");

    private static readonly FieldInfo ShadowShapeComponentField =
        typeof(ShadowCaster2D).GetField("m_ShadowShape2DComponent", InstanceFlags);

    private static readonly FieldInfo ShadowShapeProviderField =
        typeof(ShadowCaster2D).GetField("m_ShadowShape2DProvider", InstanceFlags);

    private static readonly FieldInfo ShadowCastingSourceField =
        typeof(ShadowCaster2D).GetField("m_ShadowCastingSource", InstanceFlags);

    private static readonly FieldInfo ShadowMeshField =
        typeof(ShadowCaster2D).GetField("m_ShadowMesh", InstanceFlags);

    private static readonly MethodInfo ShadowCasterAwakeMethod =
        typeof(ShadowCaster2D).GetMethod("Awake", InstanceFlags);

    private static readonly PropertyInfo ShadowMeshProperty =
        typeof(ShadowCaster2D).Assembly
            .GetType("UnityEngine.Rendering.Universal.ShadowMesh2D")
            ?.GetProperty("mesh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static bool sceneRepairHookInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSceneRepairHooks()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        sceneRepairHookInstalled = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSceneRepairHooks()
    {
        if (!sceneRepairHookInstalled)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneRepairHookInstalled = true;
        }

        RepairLoadedShadowCastersInLoadedScenes();
    }

    public static bool TryConfigureFromCollider(GameObject owner, Collider2D sourceCollider, out ShadowCaster2D shadowCaster)
    {
        shadowCaster = null;

        if (owner == null
            || sourceCollider == null
            || ColliderProviderType == null
            || ShadowShapeComponentField == null
            || ShadowShapeProviderField == null
            || ShadowCastingSourceField == null)
        {
            return false;
        }

        shadowCaster = owner.GetComponent<ShadowCaster2D>();

        EnsureValidShadowMesh(shadowCaster);

        if (shadowCaster != null && IsConfiguredForCollider(shadowCaster, sourceCollider))
        {
            ApplyCommonShadowCasterSettings(shadowCaster);
            return true;
        }

        if (shadowCaster == null)
        {
            shadowCaster = owner.AddComponent<ShadowCaster2D>();
        }

        object provider = Activator.CreateInstance(ColliderProviderType, true);

        if (provider == null)
        {
            return false;
        }

        ApplyCommonShadowCasterSettings(shadowCaster);

        ShadowShapeComponentField.SetValue(shadowCaster, sourceCollider);
        ShadowShapeProviderField.SetValue(shadowCaster, provider);
        ShadowCastingSourceField.SetValue(
            shadowCaster,
            Enum.Parse(ShadowCastingSourceField.FieldType, "ShapeProvider"));

        // Refresh the component so the collider-backed provider initializes in both editor play mode and builds.
        shadowCaster.enabled = false;
        shadowCaster.enabled = true;
        return true;
    }

    private static bool IsConfiguredForCollider(ShadowCaster2D shadowCaster, Collider2D sourceCollider)
    {
        if (shadowCaster == null || sourceCollider == null)
        {
            return false;
        }

        object configuredCollider = ShadowShapeComponentField.GetValue(shadowCaster);
        object configuredProvider = ShadowShapeProviderField.GetValue(shadowCaster);
        object configuredSource = ShadowCastingSourceField.GetValue(shadowCaster);
        return ReferenceEquals(configuredCollider, sourceCollider)
            && configuredProvider != null
            && configuredSource != null
            && string.Equals(configuredSource.ToString(), "ShapeProvider", StringComparison.Ordinal);
    }

    private static void ApplyCommonShadowCasterSettings(ShadowCaster2D shadowCaster)
    {
        shadowCaster.castsShadows = true;
        shadowCaster.selfShadows = true;
        shadowCaster.alphaCutoff = 0.1f;
        SetUseRendererSilhouette(shadowCaster, true);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        RepairLoadedShadowCasters(scene);
    }

    private static void RepairLoadedShadowCastersInLoadedScenes()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            RepairLoadedShadowCasters(scene);
        }
    }

    private static void RepairLoadedShadowCasters(Scene scene)
    {
        ShadowCaster2D[] shadowCasters = RSceneReferenceLookup.FindComponentsInScene<ShadowCaster2D>(scene);

        int repairedCount = 0;

        for (int index = 0; index < shadowCasters.Length; index++)
        {
            if (EnsureValidShadowMesh(shadowCasters[index]))
            {
                repairedCount++;
            }
        }

        if (repairedCount > 0)
        {
            Debug.LogWarning($"{nameof(RuntimeShadowCaster2DConfigurator)} repaired {repairedCount} shadow caster mesh reference(s) after scene load.");
        }
    }

    private static bool EnsureValidShadowMesh(ShadowCaster2D shadowCaster)
    {
        if (shadowCaster == null || ShadowMeshField == null || ShadowCasterAwakeMethod == null)
        {
            return false;
        }

        object shadowMesh = ShadowMeshField.GetValue(shadowCaster);
        if (!RequiresShadowMeshRepair(shadowMesh))
        {
            return false;
        }

        bool restoreEnabledState = shadowCaster.enabled && shadowCaster.gameObject.activeInHierarchy;

        if (restoreEnabledState)
        {
            shadowCaster.enabled = false;
        }

        ShadowMeshField.SetValue(shadowCaster, null);
        ShadowCasterAwakeMethod.Invoke(shadowCaster, null);

        if (restoreEnabledState)
        {
            shadowCaster.enabled = true;
        }

        return true;
    }

    private static bool RequiresShadowMeshRepair(object shadowMesh)
    {
        if (shadowMesh == null)
        {
            return true;
        }

        if (ShadowMeshProperty == null)
        {
            return false;
        }

        object meshValue = ShadowMeshProperty.GetValue(shadowMesh);

        if (meshValue == null)
        {
            return false;
        }

        return meshValue is UnityEngine.Object unityObject && unityObject == null;
    }

    private static void SetUseRendererSilhouette(ShadowCaster2D shadowCaster, bool enabled)
    {
#pragma warning disable 618
        shadowCaster.useRendererSilhouette = enabled;
#pragma warning restore 618
    }
}

