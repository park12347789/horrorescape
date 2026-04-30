using UnityEngine;
using UnityEngine.Tilemaps;

public enum MainEscapeEnemyPlacementKind
{
    Patrol = 0,
    Sentry = 1,
    Chaser = 2,
    Shared = 3
}

[DisallowMultipleComponent]
public sealed class MainEscapeEnemyPlacementMarker : MonoBehaviour
{
    [SerializeField] private MainEscapeEnemyPlacementKind placementKind = MainEscapeEnemyPlacementKind.Patrol;
    [SerializeField] private string placementId = string.Empty;

    public MainEscapeEnemyPlacementKind PlacementKind => placementKind;
    public string PlacementId => string.IsNullOrWhiteSpace(placementId) ? name : placementId.Trim();
    public Vector2 Facing => transform.up.sqrMagnitude > 0.0001f ? ((Vector2)transform.up).normalized : Vector2.up;

    public bool TryResolveCell(Tilemap tilemap, out Vector3Int cell)
    {
        if (tilemap == null)
        {
            cell = Vector3Int.zero;
            return false;
        }

        cell = MainEscapeTilemapCellUtility.WorldToCell2D(tilemap, transform.position);
        return true;
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public void Configure(MainEscapeEnemyPlacementKind configuredPlacementKind, string configuredPlacementId)
    {
        placementKind = configuredPlacementKind;
        placementId = configuredPlacementId ?? string.Empty;
    }

    [ContextMenu("Use GameObject Name As Placement Id")]
    private void UseGameObjectNameAsPlacementId()
    {
        placementId = gameObject.name;
    }
}
