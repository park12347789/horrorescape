using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeVentExitAuthoring : MonoBehaviour
{
    [SerializeField] private string exitId;
    [SerializeField] private MainEscapeVentNodeAuthoring connectedNode;
    [SerializeField] private Transform emergeAnchor;
    [SerializeField] private bool canEmerge = true;
    [SerializeField] private bool canRetreat = true;
    [SerializeField] private Vector2 facingDirection = Vector2.down;
    [SerializeField, Min(0f)] private float selectionWeight = 1f;

    public string ExitId => string.IsNullOrWhiteSpace(exitId) ? name : exitId;
    public MainEscapeVentNodeAuthoring ConnectedNode => connectedNode;
    public Transform EmergeAnchor => emergeAnchor != null ? emergeAnchor : transform;
    public bool CanEmerge => canEmerge;
    public bool CanRetreat => canRetreat;
    public Vector2 FacingDirection => facingDirection.sqrMagnitude > 0.001f
        ? facingDirection.normalized
        : Vector2.down;
    public float SelectionWeight => selectionWeight;
    public bool IsValid => connectedNode != null && (canEmerge || canRetreat);

    public void Configure(
        string configuredExitId,
        MainEscapeVentNodeAuthoring configuredConnectedNode,
        Transform configuredEmergeAnchor,
        bool configuredCanEmerge,
        bool configuredCanRetreat,
        Vector2 configuredFacingDirection,
        float configuredSelectionWeight)
    {
        exitId = configuredExitId;
        connectedNode = configuredConnectedNode;
        emergeAnchor = configuredEmergeAnchor;
        canEmerge = configuredCanEmerge;
        canRetreat = configuredCanRetreat;
        facingDirection = configuredFacingDirection;
        selectionWeight = Mathf.Max(0f, configuredSelectionWeight);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        emergeAnchor = transform;
    }
#endif
}
