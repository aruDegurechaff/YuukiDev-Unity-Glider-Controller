using System.Collections;
using UnityEngine;
using YuukiDev.Controller;
using YuukiDev.OtherScripts;

public partial class ScoreManager
{
    #region Game Over
    /*
     * Game over and revive flow
     * by: YuukiDev
     *
     * Handles UI prompts, revive cost, and restart logic.
     */
    public void HandleGameOver(PlayerController player)
    {
        if (isGameOver)
            return;

        isGameOver = true;
        downedPlayer = player;
        UpdateHighScore();

        if (reviveMessageRoutine != null)
        {
            StopCoroutine(reviveMessageRoutine);
            reviveMessageRoutine = null;
        }

        UpdateGameOverUI();
        SetGameplayCursor(false);
    }

    private void OnPlayerGameOver(PlayerController player)
    {
        HandleGameOver(player);
    }

    public void OnReviveButtonPressed()
    {
        if (!isGameOver || !enableRevive)
            return;

        PlayerController player = ResolveDownedPlayer();
        if (player == null || !player.IsGameOver)
            return;

        if (!CanAffordRevive())
        {
            UpdateGameOverUI();
            return;
        }

        int spentScore = SpendReviveCost();
        if (!player.ReviveFromGameOver())
            return;

        isGameOver = false;
        HideGameOverUI();
        SetGameplayCursor(true);

        if (distanceText != null)
            UpdateScoreText();

        ShowTemporaryReviveMessage("Revived! -" + spentScore);
    }

    public void OnPlayAgainButtonPressed()
    {
        PlayerController player = ResolveDownedPlayer();
        if (player == null)
            return;

        if (!player.RestartFromGameOver(respawnAnchor))
            return;

        ClearScenePickups();
        ResetRunScoreState();
        isGameOver = false;
        HideGameOverUI();
        SetGameplayCursor(true);
    }

    private void ShowTemporaryReviveMessage(string message)
    {
        if (gameOverText == null)
            return;

        if (reviveMessageRoutine != null)
            StopCoroutine(reviveMessageRoutine);

        reviveMessageRoutine = StartCoroutine(ShowTemporaryReviveMessageRoutine(message));
    }

    private IEnumerator ShowTemporaryReviveMessageRoutine(string message)
    {
        gameOverText.gameObject.SetActive(true);
        gameOverText.text = message;

        float duration = Mathf.Max(0.05f, reviveMessageDuration);
        yield return new WaitForSecondsRealtime(duration);

        if (!isGameOver && gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        reviveMessageRoutine = null;
    }

    private void UpdateGameOverUI()
    {
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
            gameOverText.text = enableRevive
                ? "Game Over\nNeed " + currentReviveScoreCost + " score to revive"
                : "Game Over";
        }

        if (reviveButton != null)
        {
            reviveButton.gameObject.SetActive(enableRevive);
            reviveButton.interactable = CanAffordRevive();
        }

        if (playAgainButton != null)
            playAgainButton.gameObject.SetActive(true);
    }

    private void HideGameOverUI()
    {
        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);
        if (reviveButton != null)
            reviveButton.gameObject.SetActive(false);
        if (playAgainButton != null)
            playAgainButton.gameObject.SetActive(false);
    }

    private bool CanAffordRevive()
    {
        return Mathf.FloorToInt(distanceScore) >= currentReviveScoreCost;
    }

    private int SpendReviveCost()
    {
        int spentScore = currentReviveScoreCost;
        distanceScore = Mathf.Max(0f, distanceScore - spentScore);
        currentReviveScoreCost += Mathf.Max(1, baseReviveScoreCost);
        UpdateReviveCostUI();
        return spentScore;
    }

    private void ClearScenePickups()
    {
        PowerUp[] powerUps = FindObjectsByType<PowerUp>(FindObjectsSortMode.None);
        for (int i = 0; i < powerUps.Length; i++)
        {
            if (powerUps[i] != null)
                Destroy(powerUps[i].gameObject);
        }

        CoinCollectible[] coins = FindObjectsByType<CoinCollectible>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            if (coins[i] != null)
                Destroy(coins[i].gameObject);
        }
    }
    #endregion
}
