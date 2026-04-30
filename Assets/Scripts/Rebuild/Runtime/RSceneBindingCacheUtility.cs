using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RSceneBindingCacheUtility
{
    public static TBinding[] FindSceneBindings<TBinding>(Scene scene) where TBinding : class
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return System.Array.Empty<TBinding>();
        }

        GameObject[] roots = scene.GetRootGameObjects();
        List<TBinding> bindings = new();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);

            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                if (behaviours[behaviourIndex] is TBinding binding)
                {
                    bindings.Add(binding);
                }
            }
        }

        return bindings.ToArray();
    }

    public static TBinding[] ResolveBindings<TBinding>(Scene scene, MonoBehaviour[] cachedBehaviours) where TBinding : class
    {
        TBinding[] cachedBindings = FilterBindings<TBinding>(cachedBehaviours);
        TBinding[] sceneBindings = FindSceneBindings<TBinding>(scene);

        if (cachedBindings.Length == 0)
        {
            return sceneBindings;
        }

        if (sceneBindings.Length == 0)
        {
            return cachedBindings;
        }

        List<TBinding> mergedBindings = new(cachedBindings.Length + sceneBindings.Length);
        AddUniqueBindings(cachedBindings, mergedBindings);
        AddUniqueBindings(sceneBindings, mergedBindings);
        return mergedBindings.ToArray();
    }

    public static MonoBehaviour[] ResolveBindingBehaviours<TBinding>(params TBinding[] bindings) where TBinding : class
    {
        if (bindings == null || bindings.Length == 0)
        {
            return System.Array.Empty<MonoBehaviour>();
        }

        List<MonoBehaviour> behaviours = new(bindings.Length);

        for (int index = 0; index < bindings.Length; index++)
        {
            if (bindings[index] is MonoBehaviour behaviour && behaviour != null && !behaviours.Contains(behaviour))
            {
                behaviours.Add(behaviour);
            }
        }

        return behaviours.ToArray();
    }

    private static TBinding[] FilterBindings<TBinding>(MonoBehaviour[] behaviours) where TBinding : class
    {
        if (behaviours == null || behaviours.Length == 0)
        {
            return System.Array.Empty<TBinding>();
        }

        List<TBinding> bindings = new(behaviours.Length);

        for (int index = 0; index < behaviours.Length; index++)
        {
            if (behaviours[index] is TBinding binding && binding != null)
            {
                bindings.Add(binding);
            }
        }

        return bindings.ToArray();
    }

    private static void AddUniqueBindings<TBinding>(TBinding[] sourceBindings, List<TBinding> mergedBindings) where TBinding : class
    {
        for (int index = 0; index < sourceBindings.Length; index++)
        {
            TBinding binding = sourceBindings[index];

            if (binding != null && !mergedBindings.Contains(binding))
            {
                mergedBindings.Add(binding);
            }
        }
    }
}
