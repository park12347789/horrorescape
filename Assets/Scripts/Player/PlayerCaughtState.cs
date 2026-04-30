/*
 * File Role:
 * Stores and displays the current caught state for the player.
 *
 * Runtime Use:
 * Stops player control, asks the run modal to show failure, and exposes capture entry points for different enemies.
 *
 * Study Notes:
 * Read this when tracing what happens after an enemy successfully reaches the player.
 */

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class PlayerCaughtState : MonoBehaviour
{
    [SerializeField] private bool isCaught;
    [SerializeField] private string caughtByName;
    [SerializeField, FormerlySerializedAs("runController"), FormerlySerializedAs("rebuildRunControllerSource")]
    private MonoBehaviour runControllerSource;
    [SerializeField] private ObjectiveManager objectiveManager;

    private IRunStateController runStateController;
    private WasdPlayerController playerController;
    private PlayerInput playerInput;

    public bool IsCaught => isCaught;

    private void Awake()
    {
        Time.timeScale = 1f;
        CacheRunController();
        playerController = GetComponent<WasdPlayerController>();
        playerInput = GetComponent<PlayerInput>();
    }

    private void Update()
    {
        if (!isCaught)
        {
            return;
        }

        if (runStateController != null && runStateController.IsFailureModalShowing)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current[Key.R].wasPressedThisFrame)
        {
            RestartScene();
        }
    }

    public bool TryCapture(EnemyStateMachine sourceEnemy)
    {
        if (isCaught || HasEscaped())
        {
            return false;
        }

        isCaught = true;
        caughtByName = sourceEnemy != null ? sourceEnemy.gameObject.name : "Enemy";

        if (runStateController != null && runStateController.NotifyRunFailure(caughtByName))
        {
            Debug.Log($"Player caught by {caughtByName}.", this);
            return true;
        }

        if (playerController == null)
        {
            playerController = GetComponent<WasdPlayerController>();
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        playerInput?.DeactivateInput();
        Time.timeScale = 0f;
        Debug.Log($"Player caught by {caughtByName}.", this);
        return true;
    }

    public bool TryCapture(string sourceName)
    {
        if (isCaught || HasEscaped())
        {
            return false;
        }

        isCaught = true;
        caughtByName = string.IsNullOrWhiteSpace(sourceName) ? "Enemy" : sourceName;

        if (runStateController != null && runStateController.NotifyRunFailure(caughtByName))
        {
            Debug.Log($"Player caught by {caughtByName}.", this);
            return true;
        }

        if (playerController == null)
        {
            playerController = GetComponent<WasdPlayerController>();
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        playerInput?.DeactivateInput();
        Time.timeScale = 0f;
        Debug.Log($"Player caught by {caughtByName}.", this);
        return true;
    }

    public void BindRunController(IRunStateController controller)
    {
        runStateController = controller;
        runControllerSource = controller as MonoBehaviour;
    }

    public void BindObjectiveManager(ObjectiveManager manager)
    {
        objectiveManager = manager;
    }

    private bool HasEscaped()
    {
        if (runStateController != null)
        {
            return runStateController.IsEscaped;
        }

        return objectiveManager != null && objectiveManager.IsEscaped;
    }

    private void RestartScene()
    {
        RRunSessionController sessionController = ResolveSceneSessionController();

        if (sessionController != null)
        {
            sessionController.RetryCurrentRun();
            return;
        }

        SceneLoadUtility.ReloadScene(
            gameObject.scene,
            includeScenePath: false,
            reloadErrorMessage: $"{nameof(PlayerCaughtState)} could not reload the active scene because it has no valid name or path.",
            loadSceneErrorPrefix: nameof(PlayerCaughtState));
    }

    private RRunSessionController ResolveSceneSessionController()
    {
        return RRunSessionResolver.ResolveForContext(this);
    }

    private void OnValidate()
    {
        CacheRunController();
    }

    private void OnDestroy()
    {
        if (isCaught)
        {
            Time.timeScale = 1f;
        }
    }

    private void CacheRunController()
    {
        runStateController = runControllerSource as IRunStateController;
    }
}

