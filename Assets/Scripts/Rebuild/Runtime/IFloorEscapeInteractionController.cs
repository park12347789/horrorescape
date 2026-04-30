using UnityEngine;

public interface IFloorEscapeInteractionController
{
    bool IsPickupVisible { get; }
    bool IsEscaped { get; }
    bool UsesDirectAuthoredExitInteraction { get; }
    bool UsesFinalClearPanelShortcut { get; }
    bool IsAuthoredGateVisible { get; }
    bool RequiresAuthoredGateInteraction { get; }
    bool IsAuthoredStairsVisible { get; }
    bool IsAuthoredGateUnlocked { get; }
    bool HasAuthoredGateKey { get; }
    bool IsTransitionVisible(FloorEscapeTransitionKind kind);
    bool IsTransitionUnlocked(FloorEscapeTransitionKind kind);
    bool TryRecoverCurrentTool();
    bool TryUnlockKeyGate();
    bool TryUseEmergencyStairs();
    bool TryUseFinalExit();
}

public interface IGateAnchorReadModel
{
    bool TryGetGateWorldPosition(out Vector3 worldPosition);
}
