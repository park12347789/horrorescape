using System;
using System.Collections.Generic;

using UnityEngine;

public static class RRunSceneConfigurationUtility
{
    public static string ResolveLobbyScenePath(MainEscapeRuntimeSettings settings, string sceneLocalPath, bool preferSceneLocal = false)
    {
        if (!string.IsNullOrWhiteSpace(sceneLocalPath))
        {
            return sceneLocalPath.Trim();
        }

        return settings != null && !string.IsNullOrWhiteSpace(settings.FallbackLobbyScenePath)
            ? settings.FallbackLobbyScenePath
            : sceneLocalPath?.Trim() ?? string.Empty;
    }

    public static int ResolveStartingFloorNumber(MainEscapeRuntimeSettings settings, int sceneLocalFloorNumber, bool preferSceneLocal = false)
    {
        if (sceneLocalFloorNumber > 0)
        {
            return Mathf.Max(1, sceneLocalFloorNumber);
        }

        return settings != null && settings.FallbackStartFloorNumber > 0
            ? settings.FallbackStartFloorNumber
            : Mathf.Max(1, sceneLocalFloorNumber);
    }

    public static RFloorSceneEntry[] ResolveFloorScenes(
        MainEscapeRuntimeSettings settings,
        RFloorSceneEntry[] sceneLocalRoutes,
        bool preferSceneLocal = false)
    {
        if (HasValidRoutes(sceneLocalRoutes))
        {
            return CloneValidRoutes(sceneLocalRoutes);
        }

        RFloorSceneEntry[] configuredScenes = settings != null ? settings.FallbackFloorSceneRoutes : null;
        return configuredScenes != null && configuredScenes.Length > 0
            ? configuredScenes
            : CloneValidRoutes(sceneLocalRoutes);
    }

    public static int ResolveNextFloorNumber(int currentFloorNumber, RFloorSceneEntry[] routes)
    {
        if (routes == null || routes.Length == 0)
        {
            return 0;
        }

        int bestCandidate = int.MinValue;

        for (int index = 0; index < routes.Length; index++)
        {
            int candidate = routes[index].floorNumber;

            if (candidate <= 0 || candidate >= currentFloorNumber)
            {
                continue;
            }

            if (bestCandidate == int.MinValue || candidate > bestCandidate)
            {
                bestCandidate = candidate;
            }
        }

        return bestCandidate == int.MinValue ? 0 : bestCandidate;
    }

    public static int ResolveTerminalFloorNumber(int startingFloorNumber, RFloorSceneEntry[] routes)
    {
        int resolvedStartingFloor = Mathf.Max(1, startingFloorNumber);

        if (routes == null || routes.Length == 0)
        {
            return resolvedStartingFloor;
        }

        int terminalFloor = resolvedStartingFloor;
        bool hasTerminalFloor = false;

        for (int index = 0; index < routes.Length; index++)
        {
            int candidate = routes[index].floorNumber;

            if (candidate <= 0 || candidate > resolvedStartingFloor)
            {
                continue;
            }

            if (!hasTerminalFloor || candidate < terminalFloor)
            {
                terminalFloor = candidate;
                hasTerminalFloor = true;
            }
        }

        return hasTerminalFloor ? terminalFloor : resolvedStartingFloor;
    }

    public static int ResolveRouteFloorCount(int startingFloorNumber, RFloorSceneEntry[] routes)
    {
        int resolvedStartingFloor = Mathf.Max(1, startingFloorNumber);

        if (routes == null || routes.Length == 0)
        {
            return resolvedStartingFloor;
        }

        var seenFloors = new HashSet<int>();

        for (int index = 0; index < routes.Length; index++)
        {
            int candidate = routes[index].floorNumber;

            if (candidate <= 0 || candidate > resolvedStartingFloor)
            {
                continue;
            }

            seenFloors.Add(candidate);
        }

        return seenFloors.Count > 0 ? seenFloors.Count : resolvedStartingFloor;
    }

    public static int ResolveClearedFloorCountForDestination(
        int destinationFloorNumber,
        int startingFloorNumber,
        RFloorSceneEntry[] routes)
    {
        int resolvedDestinationFloor = Mathf.Max(1, destinationFloorNumber);

        if (routes == null || routes.Length == 0)
        {
            return Mathf.Max(0, Mathf.Max(1, startingFloorNumber) - resolvedDestinationFloor);
        }

        var seenFloors = new HashSet<int>();

        for (int index = 0; index < routes.Length; index++)
        {
            int candidate = routes[index].floorNumber;

            if (candidate <= resolvedDestinationFloor || candidate > Mathf.Max(1, startingFloorNumber))
            {
                continue;
            }

            seenFloors.Add(candidate);
        }

        return seenFloors.Count;
    }

    private static bool HasValidRoutes(RFloorSceneEntry[] routes)
    {
        return routes != null && routes.Length > 0 && CloneValidRoutes(routes).Length > 0;
    }

    private static RFloorSceneEntry[] CloneValidRoutes(RFloorSceneEntry[] routes)
    {
        if (routes == null || routes.Length == 0)
        {
            return Array.Empty<RFloorSceneEntry>();
        }

        var validRoutes = new List<RFloorSceneEntry>(routes.Length);

        for (int index = 0; index < routes.Length; index++)
        {
            RFloorSceneEntry route = routes[index];

            if (route.floorNumber <= 0 || string.IsNullOrWhiteSpace(route.scenePath))
            {
                continue;
            }

            validRoutes.Add(new RFloorSceneEntry(route.floorNumber, route.scenePath.Trim()));
        }

        return validRoutes.Count > 0 ? validRoutes.ToArray() : Array.Empty<RFloorSceneEntry>();
    }
}
