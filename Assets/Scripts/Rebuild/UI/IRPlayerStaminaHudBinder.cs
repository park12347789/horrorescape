using UnityEngine;

[DisallowMultipleComponent]
public sealed class IRPlayerStaminaHudBinder : MonoBehaviour, IRebuildHudBinder
{
    [SerializeField] private IRHudCanvas hudCanvas;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;

    private PlayerStamina stamina;
    private StaminaHudView panelView;

    private void Awake()
    {
        CacheDependencies();
        ResolvePanelView();
        BindView();
    }

    private void OnValidate()
    {
        CacheDependencies();
        ResolvePanelView();
    }

    private void OnEnable()
    {
        BindView();
    }

    public void BindHudCanvas(IRHudCanvas canvas)
    {
        hudCanvas = canvas;
        ResolvePanelView();
        BindView();
    }

    public void BindPlayerRuntime(RPlayerRuntimeReferences runtime)
    {
        playerRuntime = runtime;
        CacheDependencies();
        BindView();
    }

    private void CacheDependencies()
    {
        RPlayerRuntimeReferences runtime = playerRuntime != null ? playerRuntime : GetComponent<RPlayerRuntimeReferences>();
        PlayerStamina runtimeStamina = runtime != null ? runtime.Stamina : null;
        stamina = runtimeStamina != null ? runtimeStamina : GetComponent<PlayerStamina>();
    }

    private void ResolvePanelView()
    {
        panelView = hudCanvas != null ? hudCanvas.StaminaPanel : null;
    }

    private void BindView()
    {
        panelView?.Bind(stamina);
    }
}
