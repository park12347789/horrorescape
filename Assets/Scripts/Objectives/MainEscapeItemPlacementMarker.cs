using UnityEngine;
using UnityEngine.Tilemaps;

public enum MainEscapeItemPlacementCategory
{
    SupportItem = 0,
    Key = 1
}

[DisallowMultipleComponent]
public sealed class MainEscapeItemPlacementMarker : MonoBehaviour
{
    [SerializeField] private MainEscapeItemPlacementCategory category = MainEscapeItemPlacementCategory.SupportItem;
    [SerializeField] private string placementId = string.Empty;

    public MainEscapeItemPlacementCategory Category => category;
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

    public void Configure(MainEscapeItemPlacementCategory configuredCategory, string configuredPlacementId)
    {
        category = configuredCategory;
        placementId = configuredPlacementId ?? string.Empty;
    }

    [ContextMenu("Use GameObject Name As Placement Id")]
    private void UseGameObjectNameAsPlacementId()
    {
        placementId = gameObject.name;
    }
}
