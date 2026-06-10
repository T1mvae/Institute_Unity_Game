using System;
using UnityEngine;

/// <summary>
/// Central game state manager. This is a NEW layer on top of LevelController —
/// it does NOT duplicate LevelController's fields. It manages high-level game
/// states and delegates pause/resume to LevelController for backwards compatibility.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ─── Game States ────────────────────────────────────────────────────
    public enum GameState
    {
        Playing,
        Paused,
        EventActive,
        GameOver
    }

    // ─── Events ─────────────────────────────────────────────────────────
    /// <summary>
    /// Fired whenever the game state changes. Listeners receive the new state.
    /// </summary>
    public event Action<GameState> OnGameStateChanged;

    // ─── State ──────────────────────────────────────────────────────────
    private GameState currentState = GameState.Playing;

    /// <summary>Current high-level game state.</summary>
    public GameState CurrentState
    {
        get => currentState;
        private set
        {
            if (currentState == value) return;

            GameState previous = currentState;
            currentState = value;

            Debug.Log($"[GameManager] State changed: {previous} → {currentState}");
            OnGameStateChanged?.Invoke(currentState);
        }
    }

    /// <summary>True when the game is paused (either explicitly or due to game-over).</summary>
    public bool IsPaused => currentState == GameState.Paused || currentState == GameState.GameOver;

    /// <summary>True when the game has ended (win or lose).</summary>
    public bool IsGameOver => currentState == GameState.GameOver;

    // Stores whether the game was won (true) or lost (false). Only valid when IsGameOver.
    private bool didWin;

    /// <summary>Whether the player won. Only meaningful when IsGameOver is true.</summary>
    public bool DidWin => didWin;

    // ─── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Singleton setup — no DontDestroyOnLoad; lives with the gameplay scene.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Register with the ServiceLocator so other systems can find us.
        ServiceLocator.Register<GameManager>(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister<GameManager>();
            Instance = null;
        }
    }

    // ─── Public API ─────────────────────────────────────────────────────

    /// <summary>
    /// Pause the game. Has no effect if already paused or game is over.
    /// </summary>
    public void PauseGame()
    {
        if (currentState == GameState.GameOver) return;

        CurrentState = GameState.Paused;
        ApplyPauseToLegacy(true);
    }

    /// <summary>
    /// Resume the game. Has no effect if the game is over.
    /// </summary>
    public void ResumeGame()
    {
        if (currentState == GameState.GameOver) return;

        // If an event is active, return to EventActive rather than Playing.
        CurrentState = EventManager.isEventActive ? GameState.EventActive : GameState.Playing;
        ApplyPauseToLegacy(false);
    }

    /// <summary>
    /// Toggle between paused and playing/event-active.
    /// </summary>
    public void TogglePause()
    {
        if (currentState == GameState.GameOver) return;

        if (currentState == GameState.Paused)
            ResumeGame();
        else
            PauseGame();
    }

    /// <summary>
    /// Transition to the EventActive state (e.g. when an event panel opens).
    /// </summary>
    public void SetEventActive(bool active)
    {
        if (currentState == GameState.GameOver) return;

        if (active)
        {
            CurrentState = GameState.EventActive;
        }
        else
        {
            // Revert to playing (or stay paused if the player had paused before the event).
            CurrentState = GameState.Playing;
        }
    }

    /// <summary>
    /// End the game.
    /// </summary>
    /// <param name="isWin">True for victory, false for defeat.</param>
    public void SetGameOver(bool isWin)
    {
        didWin = isWin;
        CurrentState = GameState.GameOver;
        ApplyPauseToLegacy(true);

        Debug.Log($"[GameManager] Game over — {(isWin ? "Victory" : "Defeat")}");
    }

    // ─── Backwards Compatibility ────────────────────────────────────────

    /// <summary>
    /// Delegates pause state to LevelController so the legacy timescale/UI
    /// logic continues to work.
    /// </summary>
    private void ApplyPauseToLegacy(bool pause)
    {
        if (LevelController.Instance != null)
        {
            LevelController.Instance.SetPauseFromUI(pause);
        }
    }
}
