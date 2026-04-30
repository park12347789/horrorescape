using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RSceneReferenceLookup
{
    public static TComponent FindComponentByNames<TComponent>(
        Scene scene,
        UnityEngine.Object owner,
        string ownerLabel,
        string label,
        params string[] candidateNames)
        where TComponent : Component
    {
        if (candidateNames == null || candidateNames.Length == 0)
        {
            return FindUniqueComponentInScene<TComponent>(scene, owner, ownerLabel, label);
        }

        TComponent[] components = FindComponentsInScene<TComponent>(scene);
        TComponent matchedComponent = null;
        int matchedCount = 0;

        for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            TComponent component = components[componentIndex];

            if (component == null)
            {
                continue;
            }

            for (int nameIndex = 0; nameIndex < candidateNames.Length; nameIndex++)
            {
                string candidateName = candidateNames[nameIndex];

                if (!string.IsNullOrWhiteSpace(candidateName) && component.name == candidateName)
                {
                    matchedComponent = component;
                    matchedCount++;
                    break;
                }
            }
        }

        if (matchedCount == 1)
        {
            return matchedComponent;
        }

        if (matchedCount > 1)
        {
            Debug.LogWarning(
                $"{ownerLabel} found {matchedCount} authored matches for '{label}'. " +
                $"Wire the reference explicitly instead of relying on automatic lookup.",
                owner);
            return null;
        }

        Debug.LogWarning(
            $"{ownerLabel} could not find an authored match for '{label}' by name. " +
            $"Wire the reference explicitly instead of falling back to the first {typeof(TComponent).Name}.",
            owner);
        return null;
    }

    public static TComponent FindOptionalComponentByNames<TComponent>(Scene scene, params string[] candidateNames)
        where TComponent : Component
    {
        if (candidateNames == null || candidateNames.Length == 0)
        {
            return null;
        }

        TComponent[] components = FindComponentsInScene<TComponent>(scene);

        for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            TComponent component = components[componentIndex];

            if (component == null)
            {
                continue;
            }

            for (int nameIndex = 0; nameIndex < candidateNames.Length; nameIndex++)
            {
                string candidateName = candidateNames[nameIndex];

                if (!string.IsNullOrWhiteSpace(candidateName) && component.name == candidateName)
                {
                    return component;
                }
            }
        }

        return null;
    }

    public static TComponent FindUniqueComponentInScene<TComponent>(
        Scene scene,
        UnityEngine.Object owner,
        string ownerLabel,
        string label)
        where TComponent : Component
    {
        TComponent[] components = FindComponentsInScene<TComponent>(scene);

        if (components.Length == 1)
        {
            return components[0];
        }

        if (components.Length > 1)
        {
            Debug.LogWarning(
                $"{ownerLabel} found {components.Length} scene components for '{label}'. " +
                $"Wire the authored reference explicitly instead of relying on first-match lookup.",
                owner);
        }

        return null;
    }

    public static TComponent FindFirstComponentInScene<TComponent>(Scene scene) where TComponent : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            TComponent component = roots[index].GetComponentInChildren<TComponent>(true);

            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    public static TComponent[] FindComponentsInScene<TComponent>(Scene scene) where TComponent : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return Array.Empty<TComponent>();
        }

        GameObject[] roots = scene.GetRootGameObjects();
        int componentCount = 0;

        for (int index = 0; index < roots.Length; index++)
        {
            componentCount += roots[index].GetComponentsInChildren<TComponent>(true).Length;
        }

        if (componentCount == 0)
        {
            return Array.Empty<TComponent>();
        }

        TComponent[] buffer = new TComponent[componentCount];
        int writeIndex = 0;

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            TComponent[] resolved = roots[rootIndex].GetComponentsInChildren<TComponent>(true);

            for (int componentIndex = 0; componentIndex < resolved.Length; componentIndex++)
            {
                buffer[writeIndex++] = resolved[componentIndex];
            }
        }

        return buffer;
    }

    public static Transform FindTransformInScene(Scene scene, params string[] candidateNames)
    {
        if (!scene.IsValid() || !scene.isLoaded || candidateNames == null || candidateNames.Length == 0)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            for (int nameIndex = 0; nameIndex < candidateNames.Length; nameIndex++)
            {
                string candidateName = candidateNames[nameIndex];

                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    continue;
                }

                Transform match = FindTransformInHierarchy(roots[rootIndex].transform, candidateName);

                if (match != null)
                {
                    return match;
                }
            }
        }

        return null;
    }

    public static Transform FindTransformInHierarchy(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform match = FindTransformInHierarchy(root.GetChild(index), targetName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    public static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }
}
