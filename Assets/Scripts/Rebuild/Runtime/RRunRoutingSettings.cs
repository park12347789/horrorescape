using UnityEngine;

[CreateAssetMenu(
    fileName = "RRunRoutingSettings",
    menuName = "Main Escape/Run Routing Settings")]
public sealed class RRunRoutingSettings : ScriptableObject
{
    [SerializeField] private ChapterDefinition defaultChapter;
    [SerializeField] private string lobbyScenePath = MainEscapeSceneIdentityUtility.GetCanonicalLobbyScenePath();
    [SerializeField] private string tutorialScenePath = MainEscapeSceneIdentityUtility.GetCanonicalTutorialScenePath();
    [SerializeField] private string elevatorTransitionScenePath = MainEscapeSceneIdentityUtility.GetCanonicalElevatorTransitionScenePath();
    [SerializeField, Min(1)] private int startingFloorNumber = MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber();
    [SerializeField] private RFloorSceneEntry[] floorScenes = MainEscapeSceneIdentityUtility.GetCanonicalFloorSceneEntries();

    public ChapterDefinition DefaultChapter => defaultChapter;
    public RouteGraphDefinition DefaultRouteGraph => defaultChapter != null ? defaultChapter.StartRouteGraph : null;
    public string LobbyScenePath => lobbyScenePath;
    public string TutorialScenePath => tutorialScenePath;
    public string ElevatorTransitionScenePath => elevatorTransitionScenePath;
    public int StartingFloorNumber => startingFloorNumber;
    public RFloorSceneEntry[] FloorScenes => floorScenes;
}
