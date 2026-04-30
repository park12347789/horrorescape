using System;

using UnityEngine;
using UnityEngine.SceneManagement;

public static class RSceneRouter
{
    public static int ResolveExitDestinationFloor(int currentFloorNumber)
    {
        Debug.LogError("RSceneRouter requires an active RRunSessionController to resolve exit destination floors.");
        return 0;
    }

    public static int ResolveExitDestinationFloor(RRunSessionController sessionController, int currentFloorNumber)
    {
        if (sessionController == null)
        {
            Debug.LogError("RSceneRouter requires an active RRunSessionController to resolve exit destination floors.");
            return 0;
        }

        return sessionController.ResolveNextFloorNumber(currentFloorNumber);
    }

    public static void LoadLobbyScene(RRunSessionController sessionController)
    {
        if (sessionController == null)
        {
            Debug.LogError("RSceneRouter requires a session controller to load the lobby scene.");
            return;
        }

        SceneLoadUtility.LoadSceneByPathOrName(
            sessionController.LobbyScenePath,
            nameof(RSceneRouter),
            "RSceneRouter received an empty scene path.");
    }

    public static void LoadTutorialScene(RRunSessionController sessionController)
    {
        if (sessionController == null)
        {
            Debug.LogError("RSceneRouter requires a session controller to load the tutorial scene.");
            return;
        }

        SceneLoadUtility.LoadSceneByPathOrName(
            sessionController.TutorialScenePath,
            nameof(RSceneRouter),
            "RSceneRouter received an empty tutorial scene path.");
    }

    public static void LoadGameplayScene(RRunSessionController sessionController)
    {
        LoadCurrentFloorScene(sessionController);
    }

    public static void LoadCurrentFloorScene(RRunSessionController sessionController)
    {
        if (sessionController == null)
        {
            Debug.LogError("RSceneRouter requires a session controller to load the gameplay scene.");
            return;
        }

        SceneLoadUtility.LoadSceneByPathOrName(
            sessionController.GetCurrentFloorScenePath(),
            nameof(RSceneRouter),
            "RSceneRouter received an empty scene path.");
    }

    public static void LoadFloorScene(RRunSessionController sessionController, int floorNumber)
    {
        if (sessionController == null)
        {
            Debug.LogError("RSceneRouter requires a session controller to load a floor scene.");
            return;
        }

        SceneLoadUtility.LoadSceneByPathOrName(
            sessionController.GetScenePathForFloor(floorNumber),
            nameof(RSceneRouter),
            "RSceneRouter received an empty scene path.");
    }

    public static bool LoadFloorSceneThroughElevatorTransition(
        RRunSessionController sessionController,
        int sourceFloorNumber,
        int destinationFloorNumber)
    {
        if (sessionController == null)
        {
            Debug.LogError("RSceneRouter requires a session controller to load the elevator transition scene.");
            return false;
        }

        if (destinationFloorNumber <= 0)
        {
            Debug.LogError("RSceneRouter received an invalid elevator transition destination floor.");
            return false;
        }

        if (sourceFloorNumber == destinationFloorNumber)
        {
            Debug.LogError("RSceneRouter cannot route an elevator transition to the same floor.");
            return false;
        }

        string destinationScenePath = sessionController.GetScenePathForFloor(destinationFloorNumber);

        if (!SceneLoadUtility.TryResolveSceneBuildIndex(
                destinationScenePath,
                nameof(RSceneRouter),
                "RSceneRouter received an empty destination floor scene path.",
                out _))
        {
            return false;
        }

        string transitionScenePath = sessionController.ElevatorTransitionScenePath;

        if (!SceneLoadUtility.TryResolveSceneBuildIndex(
                transitionScenePath,
                nameof(RSceneRouter),
                "RSceneRouter received an empty elevator transition scene path.",
                out _))
        {
            return false;
        }

        RElevatorTransitionRequestStore.Set(new RElevatorTransitionRequest(
            destinationScenePath,
            sourceFloorNumber,
            destinationFloorNumber,
            "FloorHandoff"));

        SceneLoadUtility.LoadSceneByPathOrName(
            transitionScenePath,
            nameof(RSceneRouter),
            "RSceneRouter received an empty elevator transition scene path.");
        return true;
    }

    public static bool LoadExitDestinationScene(RRunSessionController sessionController, int currentFloorNumber, out int destinationFloorNumber)
    {
        destinationFloorNumber = ResolveExitDestinationFloor(sessionController, currentFloorNumber);

        if (destinationFloorNumber <= 0)
        {
            return false;
        }

        LoadFloorScene(sessionController, destinationFloorNumber);
        return true;
    }

    public static void ReloadCurrentFloorScene(RRunSessionController sessionController)
    {
        if (sessionController == null)
        {
            Debug.LogError("RSceneRouter requires a session controller to reload the current floor scene.");
            return;
        }

        SceneLoadUtility.LoadSceneByPathOrName(
            sessionController.GetCurrentFloorScenePath(),
            errorPrefix: nameof(RSceneRouter),
            emptyPathErrorMessage: "RSceneRouter received an empty scene path.");
    }

    public static bool SceneMatches(Scene scene, string scenePath)
    {
        return MainEscapeSceneIdentityUtility.MatchesScene(scene, scenePath);
    }

}
