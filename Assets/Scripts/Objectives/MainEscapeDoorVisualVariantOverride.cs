using System;
using UnityEngine;

[Serializable]
public enum MainEscapeDoorVisualVariantKind
{
    None,
    FrontDoor,
    SideDoor42
}

[DisallowMultipleComponent]
public sealed class MainEscapeDoorVisualVariantOverride : MonoBehaviour
{
    [SerializeField] private MainEscapeDoorVisualVariantKind visualVariant = MainEscapeDoorVisualVariantKind.None;

    public MainEscapeDoorVisualVariantKind VisualVariant => visualVariant;

    public bool HasOverride => visualVariant != MainEscapeDoorVisualVariantKind.None;

    public void Configure(MainEscapeDoorVisualVariantKind configuredVisualVariant)
    {
        visualVariant = configuredVisualVariant;
    }
}

public static class MainEscapeDoorVisualVariantResolver
{
    public static MainEscapeDoorVisualVariantKind ResolveForVisualRoots(Transform[] roots)
    {
        if (roots == null || roots.Length == 0)
        {
            return MainEscapeDoorVisualVariantKind.None;
        }

        MainEscapeDoorVisualVariantKind resolvedVariant = MainEscapeDoorVisualVariantKind.None;

        for (int index = 0; index < roots.Length; index++)
        {
            MainEscapeDoorVisualVariantKind candidate = ResolveForVisualRoot(roots[index]);

            if (candidate == MainEscapeDoorVisualVariantKind.SideDoor42)
            {
                return candidate;
            }

            if (candidate == MainEscapeDoorVisualVariantKind.FrontDoor)
            {
                resolvedVariant = candidate;
            }
        }

        return resolvedVariant;
    }

    public static MainEscapeDoorVisualVariantKind ResolveForVisualRoot(Transform root)
    {
        if (TryResolveOverride(root, out MainEscapeDoorVisualVariantKind explicitVariant))
        {
            return explicitVariant;
        }

        return ResolveLegacyHeuristic(root);
    }

    public static bool IsSideDoorRoot(Transform root)
    {
        return ResolveForVisualRoot(root) == MainEscapeDoorVisualVariantKind.SideDoor42;
    }

    public static bool IsFrontDoorRoot(Transform root)
    {
        return ResolveForVisualRoot(root) == MainEscapeDoorVisualVariantKind.FrontDoor;
    }

    private static bool TryResolveOverride(Transform root, out MainEscapeDoorVisualVariantKind variant)
    {
        variant = MainEscapeDoorVisualVariantKind.None;

        Transform current = root;

        for (int depth = 0; depth < 2 && current != null; depth++)
        {
            MainEscapeDoorVisualVariantOverride variantOverride = current.GetComponent<MainEscapeDoorVisualVariantOverride>();

            if (variantOverride != null && variantOverride.HasOverride)
            {
                variant = variantOverride.VisualVariant;
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static MainEscapeDoorVisualVariantKind ResolveLegacyHeuristic(Transform root)
    {
        if (root == null)
        {
            return MainEscapeDoorVisualVariantKind.None;
        }

        string name = root.name ?? string.Empty;

        if (name.IndexOf("VexedTileBProp_42", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("CustomSideDoor", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("SideDoor", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MainEscapeDoorVisualVariantKind.SideDoor42;
        }

        if (root.parent != null
            && root.parent.name.IndexOf("sidedoor", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MainEscapeDoorVisualVariantKind.SideDoor42;
        }

        if (name.IndexOf("VexedTileBProp_01_Top", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MainEscapeDoorVisualVariantKind.FrontDoor;
        }

        return MainEscapeDoorVisualVariantKind.None;
    }
}
