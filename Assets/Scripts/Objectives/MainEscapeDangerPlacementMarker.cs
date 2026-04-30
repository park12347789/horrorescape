using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class MainEscapeDangerPlacementMarker : MonoBehaviour
{
    [SerializeField] private string placementId = string.Empty;

    public string PlacementId => string.IsNullOrWhiteSpace(placementId) ? name : placementId.Trim();

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

    public void Configure(string configuredPlacementId)
    {
        placementId = configuredPlacementId ?? string.Empty;
    }

    [ContextMenu("Use GameObject Name As Placement Id")]
    private void UseGameObjectNameAsPlacementId()
    {
        placementId = gameObject.name;
    }
}
