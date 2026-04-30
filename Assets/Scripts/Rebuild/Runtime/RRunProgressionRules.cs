using UnityEngine;

[CreateAssetMenu(
    fileName = "RRunProgressionRules",
    menuName = "Main Escape/Run Progression Rules")]
public sealed class RRunProgressionRules : ScriptableObject
{
    [SerializeField] private bool directExitRequiresKeyOnAuthoredFloors = true;
    [SerializeField] private string startupClearPanelShortcutTemplate = "Wake up on {floor}F and trigger the clear panel.";
    [SerializeField] private string startupFirstFloorStreetExitTemplate = "Wake up on {floor}F and use the street exit.";
    [SerializeField] private string startupElevatorTemplate = "Wake up on {floor}F. Find the Iron Gate Key, then use the elevator.";
    [SerializeField] private string startupTerminalExitRouteTemplate = "Wake up on {floor}F. Find the Iron Gate Key, then use the exit route.";
    [SerializeField] private string startupUpperFloorTemplate = "Wake up on {floor}F. Find the Iron Gate Key.";
    [SerializeField] private string routeAlreadyClearMessage = "The route is already clear.";
    [SerializeField] private string noFloorToolPickupMessage = "This scene-chain route does not use a floor tool pickup.";
    [SerializeField] private string gateNotPartOfRouteMessage = "The iron gate is not part of this floor route.";
    [SerializeField] private string useElevatorMessage = "Use the elevator.";
    [SerializeField] private string useKeyedExitMessage = "Use the keyed exit.";
    [SerializeField] private string needIronGateKeyMessage = "Need Iron Gate Key.";
    [SerializeField] private string gateAlreadyOpenMessage = "Iron gate already open.";
    [SerializeField] private string keyWentMissingMessage = "Iron Gate Key went missing.";
    [SerializeField] private string gateOpenFailedMessage = "Iron gate route is wired, but the authored main gate did not open.";
    [SerializeField] private string gateUnlockedHeadExitRouteMessage = "Iron gate unlocked. Head for the exit route.";
    [SerializeField] private string gateUnlockedHeadStairsMessage = "Iron gate unlocked. Head for the emergency stairs.";
    [SerializeField] private string noLowerStairRouteMessage = "No lower stair route remains. Find the street exit.";
    [SerializeField] private string clearPanelEscapeSuccessMessage = "Escape successful. Review the clear panel when ready.";
    [SerializeField] private string needKeyForElevatorMessage = "Need Iron Gate Key to use the elevator.";
    [SerializeField] private string needKeyForExitMessage = "Need Iron Gate Key to use the exit.";
    [SerializeField] private string unlockGateBeforeStairsMessage = "Unlock the iron gate before using the stairs.";
    [SerializeField] private string runSessionUnavailableMessage = "Run session unavailable.";
    [SerializeField] private string escapeReturnLobbyMessage = "Escape successful. Return to the lobby when ready.";
    [SerializeField] private string noLowerFloorSceneConfiguredMessage = "No lower floor scene is configured for this exit.";
    [SerializeField] private string arrivalElevatorTerminalTemplate = "Elevator reached {floor}F. Find the Iron Gate Key, then escape.";
    [SerializeField] private string arrivalDropTerminalTemplate = "Dropped to {floor}F. Find the Iron Gate Key, then escape.";
    [SerializeField] private string arrivalElevatorTemplate = "Elevator reached {floor}F. Find the Iron Gate Key.";
    [SerializeField] private string arrivalDropTemplate = "Dropped to {floor}F. Find the Iron Gate Key.";
    [SerializeField] private string arrivalStreetTemplate = "Dropped to {floor}F. Head for the street exit.";
    [SerializeField] private string streetAccessOnlyFromFirstFloorMessage = "Street access only opens from 1F.";
    [SerializeField] private string finalExitSuccessMessage = "Escape successful. Use the 1F exit route to return to the lobby.";

    public bool DirectExitRequiresKeyOnAuthoredFloors => directExitRequiresKeyOnAuthoredFloors;
    public string RouteAlreadyClearMessage => routeAlreadyClearMessage;
    public string NoFloorToolPickupMessage => noFloorToolPickupMessage;
    public string GateNotPartOfRouteMessage => gateNotPartOfRouteMessage;
    public string UseElevatorMessage => useElevatorMessage;
    public string UseKeyedExitMessage => useKeyedExitMessage;
    public string NeedIronGateKeyMessage => needIronGateKeyMessage;
    public string GateAlreadyOpenMessage => gateAlreadyOpenMessage;
    public string KeyWentMissingMessage => keyWentMissingMessage;
    public string GateOpenFailedMessage => gateOpenFailedMessage;
    public string NoLowerStairRouteMessage => noLowerStairRouteMessage;
    public string ClearPanelEscapeSuccessMessage => clearPanelEscapeSuccessMessage;
    public string NeedKeyForElevatorMessage => needKeyForElevatorMessage;
    public string NeedKeyForExitMessage => needKeyForExitMessage;
    public string UnlockGateBeforeStairsMessage => unlockGateBeforeStairsMessage;
    public string RunSessionUnavailableMessage => runSessionUnavailableMessage;
    public string EscapeReturnLobbyMessage => escapeReturnLobbyMessage;
    public string NoLowerFloorSceneConfiguredMessage => noLowerFloorSceneConfiguredMessage;
    public string StreetAccessOnlyFromFirstFloorMessage => streetAccessOnlyFromFirstFloorMessage;
    public string FinalExitSuccessMessage => finalExitSuccessMessage;

    public string BuildStartupMessage(
        int currentFloorNumber,
        bool usesFinalClearPanelShortcut,
        bool usesElevatorPropDirectExit,
        bool isTerminalRouteFloor)
    {
        if (currentFloorNumber <= 1)
        {
            return FormatFloor(
                usesFinalClearPanelShortcut
                    ? startupClearPanelShortcutTemplate
                    : startupFirstFloorStreetExitTemplate,
                currentFloorNumber);
        }

        if (usesElevatorPropDirectExit)
        {
            return FormatFloor(startupElevatorTemplate, currentFloorNumber);
        }

        return FormatFloor(
            isTerminalRouteFloor
                ? startupTerminalExitRouteTemplate
                : startupUpperFloorTemplate,
            currentFloorNumber);
    }

    public string BuildGateUnlockedMessage(bool isTerminalRouteFloor)
    {
        return isTerminalRouteFloor
            ? gateUnlockedHeadExitRouteMessage
            : gateUnlockedHeadStairsMessage;
    }

    public string BuildDirectExitReadyMessage(bool usesElevatorPropDirectExit)
    {
        return usesElevatorPropDirectExit
            ? useElevatorMessage
            : useKeyedExitMessage;
    }

    public string BuildNeedKeyForDirectExitMessage(bool usesElevatorPropDirectExit)
    {
        return usesElevatorPropDirectExit
            ? needKeyForElevatorMessage
            : needKeyForExitMessage;
    }

    public string BuildFloorArrivalMessage(
        int destinationFloorNumber,
        bool destinationIsTerminal,
        bool usesElevatorPropDirectExit)
    {
        if (destinationFloorNumber <= 1)
        {
            return FormatFloor(arrivalStreetTemplate, 1);
        }

        if (destinationIsTerminal)
        {
            return FormatFloor(
                usesElevatorPropDirectExit
                    ? arrivalElevatorTerminalTemplate
                    : arrivalDropTerminalTemplate,
                destinationFloorNumber);
        }

        return FormatFloor(
            usesElevatorPropDirectExit
                ? arrivalElevatorTemplate
                : arrivalDropTemplate,
            destinationFloorNumber);
    }

    private static string FormatFloor(string template, int floorNumber)
    {
        string resolvedTemplate = string.IsNullOrWhiteSpace(template)
            ? "{floor}F"
            : template;
        return resolvedTemplate.Replace("{floor}", Mathf.Max(1, floorNumber).ToString());
    }
}
