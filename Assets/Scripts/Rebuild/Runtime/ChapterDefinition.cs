using UnityEngine;

[CreateAssetMenu(
    fileName = "ChapterDefinition",
    menuName = "Main Escape/Contracts/Chapter Definition")]
public sealed class ChapterDefinition : ScriptableObject
{
    [SerializeField] private string chapterId = "hospital";
    [SerializeField] private string displayName = "Hospital";
    [SerializeField] private RouteGraphDefinition startRouteGraph;
    [SerializeField] private bool unlockedByDefault = true;

    public string ChapterId => chapterId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ChapterId : displayName.Trim();
    public RouteGraphDefinition StartRouteGraph => startRouteGraph;
    public bool UnlockedByDefault => unlockedByDefault;
    public bool IsValid => !string.IsNullOrWhiteSpace(ChapterId) && startRouteGraph != null && startRouteGraph.IsValid;
}
