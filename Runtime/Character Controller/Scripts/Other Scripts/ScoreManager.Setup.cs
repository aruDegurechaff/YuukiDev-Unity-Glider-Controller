using UnityEngine;
using TMPro;
using YuukiDev.OtherScripts;
using YuukiDev.Controller;

public partial class ScoreManager
{
    #region Setup & UI
    /*
     * Setup and dependency wiring
     * by: YuukiDev
     *
     * Keeps references resolved and UI synced with runtime state.
     */
    private void ResolveDependencies()
    {
        if (movementTracker == null)
            movementTracker = GetComponent<MovementTracker>();
        if (grazeChecker == null)
            grazeChecker = GetComponent<ProximityChecker>();

        if (movementTracker == null)
            movementTracker = FindAnyObjectByType<MovementTracker>();
        if (grazeChecker == null)
            grazeChecker = FindAnyObjectByType<ProximityChecker>();

        if (movementTracker == null && !warnedMissingMovementTracker)
        {
            Debug.LogWarning("ScoreManager: MovementTracker not found. Distance score gain will be 0 until it is available.");
            warnedMissingMovementTracker = true;
        }

        if (grazeChecker == null && !warnedMissingGrazeChecker)
        {
            Debug.LogWarning("ScoreManager: ProximityChecker not found. Graze multiplier defaults to 1x.");
            warnedMissingGrazeChecker = true;
        }

        if (movementTracker != null)
            warnedMissingMovementTracker = false;
        if (grazeChecker != null)
            warnedMissingGrazeChecker = false;
    }

    private void InitializePlayerAtStart()
    {
        if (!spawnPlayerAtAnchorOnStart || respawnAnchor == null)
            return;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null && playerPrefab != null)
            player = Instantiate(playerPrefab, respawnAnchor.position, respawnAnchor.rotation);

        if (player == null)
            return;

        player.transform.SetPositionAndRotation(respawnAnchor.position, respawnAnchor.rotation);
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb == null)
            playerRb = player.GetComponentInChildren<Rigidbody>();

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        downedPlayer = player;
        AssignPlayerToSpawners(player.transform);
    }

    private PlayerController ResolveDownedPlayer()
    {
        if (downedPlayer != null)
            return downedPlayer;

        downedPlayer = FindAnyObjectByType<PlayerController>();
        if (downedPlayer != null)
            AssignPlayerToSpawners(downedPlayer.transform);
        return downedPlayer;
    }

    private void AssignPlayerToSpawners(Transform target)
    {
        if (target == null)
            return;

        CoinsSpawnerManager[] coinSpawners = FindObjectsByType<CoinsSpawnerManager>(FindObjectsSortMode.None);
        for (int i = 0; i < coinSpawners.Length; i++)
        {
            if (coinSpawners[i] != null)
                coinSpawners[i].SetPlayerTarget(target);
        }

        PowerUpsSpawnerManager[] powerSpawners = FindObjectsByType<PowerUpsSpawnerManager>(FindObjectsSortMode.None);
        for (int i = 0; i < powerSpawners.Length; i++)
        {
            if (powerSpawners[i] != null)
                powerSpawners[i].SetPlayerTarget(target);
        }
    }

    private void HookButtons()
    {
        if (reviveButton != null)
            reviveButton.onClick.AddListener(OnReviveButtonPressed);
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(OnPlayAgainButtonPressed);
    }

    private void UnhookButtons()
    {
        if (reviveButton != null)
            reviveButton.onClick.RemoveListener(OnReviveButtonPressed);
        if (playAgainButton != null)
            playAgainButton.onClick.RemoveListener(OnPlayAgainButtonPressed);
    }

    private void ResolveCursorManager()
    {
        if (cursorManager == null)
            cursorManager = FindAnyObjectByType<CursorManager>();
    }

    private void SetGameplayCursor(bool isGameplayMode)
    {
        ResolveCursorManager();
        if (cursorManager != null)
        {
            cursorManager.SetCursorState(isGameplayMode);
            return;
        }

        Cursor.lockState = isGameplayMode ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isGameplayMode;
    }

    private void UpdateScoreText(bool force = false)
    {
        if (distanceText == null)
            return;

        int currentScore = Mathf.FloorToInt(distanceScore);
        if (!force && currentScore == lastDisplayedScore)
            return;

        lastDisplayedScore = currentScore;
        distanceText.SetText(ScoreTextFormat, currentScore);
    }

    private void UpdateReviveCostUI()
    {
        if (reviveCostText == null)
            return;

        if (!enableRevive)
        {
            reviveCostText.gameObject.SetActive(false);
            return;
        }

        reviveCostText.gameObject.SetActive(true);
        reviveCostText.SetText(ReviveCostTextFormat, currentReviveScoreCost);
    }
    #endregion
}
