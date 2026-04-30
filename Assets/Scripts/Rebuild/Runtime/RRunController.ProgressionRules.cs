using UnityEngine;

public sealed partial class RRunController
{
    [Header("Progression Rules")]
    [SerializeField] private RRunProgressionRules progressionRules;

    private bool ResolveDirectExitRequiresKeyOnAuthoredFloors()
    {
        return progressionRules != null
            ? progressionRules.DirectExitRequiresKeyOnAuthoredFloors
            : directExitRequiresKeyOnAuthoredFloors;
    }

    private string ResolveRouteAlreadyClearMessage()
    {
        return progressionRules != null
            ? progressionRules.RouteAlreadyClearMessage
            : "The route is already clear.";
    }

    private string ResolveNoFloorToolPickupMessage()
    {
        return progressionRules != null
            ? progressionRules.NoFloorToolPickupMessage
            : "This scene-chain route does not use a floor tool pickup.";
    }

    private string ResolveGateNotPartOfRouteMessage()
    {
        return progressionRules != null
            ? progressionRules.GateNotPartOfRouteMessage
            : "The iron gate is not part of this floor route.";
    }

    private string ResolveDirectExitReadyMessage()
    {
        return progressionRules != null
            ? progressionRules.BuildDirectExitReadyMessage(UsesElevatorPropDirectExit)
            : UsesElevatorPropDirectExit
                ? "Use the elevator."
                : "Use the keyed exit.";
    }

    private string ResolveNeedIronGateKeyMessage()
    {
        return progressionRules != null
            ? progressionRules.NeedIronGateKeyMessage
            : "Need Iron Gate Key.";
    }

    private string ResolveGateAlreadyOpenMessage()
    {
        return progressionRules != null
            ? progressionRules.GateAlreadyOpenMessage
            : "Iron gate already open.";
    }

    private string ResolveKeyWentMissingMessage()
    {
        return progressionRules != null
            ? progressionRules.KeyWentMissingMessage
            : "Iron Gate Key went missing.";
    }

    private string ResolveGateOpenFailedMessage()
    {
        return progressionRules != null
            ? progressionRules.GateOpenFailedMessage
            : "Iron gate route is wired, but the authored main gate did not open.";
    }

    private string ResolveGateUnlockedMessage()
    {
        return progressionRules != null
            ? progressionRules.BuildGateUnlockedMessage(IsTerminalRouteFloor(CurrentFloorNumber))
            : IsTerminalRouteFloor(CurrentFloorNumber)
                ? "Iron gate unlocked. Head for the exit route."
                : "Iron gate unlocked. Head for the emergency stairs.";
    }

    private string ResolveNoLowerStairRouteMessage()
    {
        return progressionRules != null
            ? progressionRules.NoLowerStairRouteMessage
            : "No lower stair route remains. Find the street exit.";
    }

    private string ResolveClearPanelEscapeSuccessMessage()
    {
        return progressionRules != null
            ? progressionRules.ClearPanelEscapeSuccessMessage
            : "Escape successful. Review the clear panel when ready.";
    }

    private string ResolveNeedKeyForDirectExitMessage()
    {
        return progressionRules != null
            ? progressionRules.BuildNeedKeyForDirectExitMessage(UsesElevatorPropDirectExit)
            : UsesElevatorPropDirectExit
                ? "Need Iron Gate Key to use the elevator."
                : "Need Iron Gate Key to use the exit.";
    }

    private string ResolveUnlockGateBeforeStairsMessage()
    {
        return progressionRules != null
            ? progressionRules.UnlockGateBeforeStairsMessage
            : "Unlock the iron gate before using the stairs.";
    }

    private string ResolveRunSessionUnavailableMessage()
    {
        return progressionRules != null
            ? progressionRules.RunSessionUnavailableMessage
            : "Run session unavailable.";
    }

    private string ResolveEscapeReturnLobbyMessage()
    {
        return progressionRules != null
            ? progressionRules.EscapeReturnLobbyMessage
            : "Escape successful. Return to the lobby when ready.";
    }

    private string ResolveNoLowerFloorSceneConfiguredMessage()
    {
        return progressionRules != null
            ? progressionRules.NoLowerFloorSceneConfiguredMessage
            : "No lower floor scene is configured for this exit.";
    }

    private string ResolveFloorArrivalMessage(int destinationFloorNumber, bool destinationIsTerminal)
    {
        return progressionRules != null
            ? progressionRules.BuildFloorArrivalMessage(destinationFloorNumber, destinationIsTerminal, UsesElevatorPropDirectExit)
            : destinationFloorNumber > 1
                ? destinationIsTerminal
                    ? UsesElevatorPropDirectExit
                        ? $"Elevator reached {destinationFloorNumber}F. Find the Iron Gate Key, then escape."
                        : $"Dropped to {destinationFloorNumber}F. Find the Iron Gate Key, then escape."
                    : UsesElevatorPropDirectExit
                        ? $"Elevator reached {destinationFloorNumber}F. Find the Iron Gate Key."
                        : $"Dropped to {destinationFloorNumber}F. Find the Iron Gate Key."
                : "Dropped to 1F. Head for the street exit.";
    }

    private string ResolveStreetAccessOnlyFromFirstFloorMessage()
    {
        return progressionRules != null
            ? progressionRules.StreetAccessOnlyFromFirstFloorMessage
            : "Street access only opens from 1F.";
    }

    private string ResolveFinalExitSuccessMessage()
    {
        return progressionRules != null
            ? progressionRules.FinalExitSuccessMessage
            : "Escape successful. Use the 1F exit route to return to the lobby.";
    }
}
