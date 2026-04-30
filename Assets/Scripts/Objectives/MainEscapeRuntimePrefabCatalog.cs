using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(
    fileName = "MainEscapeRuntimePrefabCatalog",
    menuName = "Main Escape/Runtime Prefab Catalog")]
public sealed class MainEscapeRuntimePrefabCatalog : ScriptableObject
{
    [Header("Items")]
    [SerializeField] private FloorEscapeGoalPickup floorToolPickupPrefab;
    [SerializeField] private GameObject ironGateKeyVisualPrefab;
    [SerializeField] private FloorEscapeTransitionPoint emergencyStairsPrefab;
    [SerializeField] private FloorEscapeTransitionPoint finalExitPrefab;
    [SerializeField] private PrototypeInventoryPickup flashlightBatteryPickupPrefab;
    [SerializeField] private PrototypeInventoryPickup glassBottlePickupPrefab;
    [SerializeField] private PrototypeInventoryPickup medkitPickupPrefab;

    [Header("Enemies")]
    [SerializeField] private EnemyPrefabBindings groundEnemyPrefab;
    [SerializeField] private CeilingVentEnemyPrefabBindings ventEnemyPrefab;

    [Header("Hazards")]
    [SerializeField] private NoiseFloorPanel glassTrapPanelPrefab;

    [Header("Characters")]
    [SerializeField] private GameObject playerPrefab;

    public FloorEscapeGoalPickup FloorToolPickupPrefab => floorToolPickupPrefab;
    public GameObject IronGateKeyVisualPrefab => ironGateKeyVisualPrefab;
    public FloorEscapeTransitionPoint EmergencyStairsPrefab => emergencyStairsPrefab;
    public FloorEscapeTransitionPoint FinalExitPrefab => finalExitPrefab;
    public PrototypeInventoryPickup FlashlightBatteryPickupPrefab => flashlightBatteryPickupPrefab;
    public PrototypeInventoryPickup GlassBottlePickupPrefab => glassBottlePickupPrefab;
    public PrototypeInventoryPickup MedkitPickupPrefab => medkitPickupPrefab;
    public EnemyPrefabBindings GroundEnemyPrefab => groundEnemyPrefab;
    public CeilingVentEnemyPrefabBindings VentEnemyPrefab => ventEnemyPrefab;
    public NoiseFloorPanel GlassTrapPanelPrefab => glassTrapPanelPrefab;
    public GameObject PlayerPrefab => playerPrefab;

    public static MainEscapeRuntimePrefabCatalog Load()
    {
        return MainEscapeRuntimePrefabCatalogOverrideResolver.Load();
    }

    public static MainEscapeRuntimePrefabCatalog LoadDefault()
    {
        return MainEscapeRuntimePrefabCatalogOverrideResolver.LoadDefault();
    }

    public static MainEscapeRuntimePrefabCatalog LoadForScene(Scene scene)
    {
        return MainEscapeRuntimePrefabCatalogOverrideResolver.LoadForScene(scene);
    }

    public static MainEscapeRuntimePrefabCatalog LoadForSceneName(string sceneName)
    {
        return MainEscapeRuntimePrefabCatalogOverrideResolver.LoadForSceneName(sceneName);
    }
}
