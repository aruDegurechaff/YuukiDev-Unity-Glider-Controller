using UnityEngine;
using YuukiDev.OtherScripts;

public partial class ScoreManager
{
    #region Scoring
    /*
     * Scoring Methods Rewritten
     * by: YuukiDev
     *
     * Dynamic scoring that considers speed and graze proximity.
     */
    public void FinalScoring()
    {
        float multiplier = 1f;
        if (grazeChecker != null)
        {
            multiplier =
                grazeChecker.IsClose ? 3.75f :
                grazeChecker.IsMid ? 2.5f :
                grazeChecker.IsFar ? 1.75f :
                1f;
        }

        BaseScoring(multiplier);
    }

    public float BaseScoring(float multiplier)
    {
        float currentSpeed = movementTracker != null ? movementTracker.CurrentSpeed : 0f;

        float basePoint =
            currentSpeed >= 75f ? 25f :
            currentSpeed >= 60f ? 20f :
            currentSpeed >= 45f ? 15f :
            currentSpeed >= 30f ? 12f :
            10f;

        float pointsPerSecond = (currentSpeed / 20f) * basePoint;

        float deltaScore = pointsPerSecond * multiplier * activePowerUpMultiplier * Time.deltaTime;
        distanceScore += deltaScore;
        return distanceScore;
    }

    public void AddScore(float amount)
    {
        if (amount <= 0f || isGameOver)
            return;

        distanceScore += amount;
    }

    public void ApplyLanternSparkBoost(float multiplier, float duration)
    {
        activePowerUpMultiplier = Mathf.Max(activePowerUpMultiplier, Mathf.Max(1f, multiplier));
        float safeDuration = Mathf.Max(0.1f, duration);
        powerUpTimer = Mathf.Max(powerUpTimer, safeDuration);
        lanternSparksDurationTotal = Mathf.Max(lanternSparksDurationTotal, safeDuration);
    }

    public float LanternSparksRemaining => Mathf.Max(0f, powerUpTimer);
    public float LanternSparksNormalized => lanternSparksDurationTotal > 0.001f
        ? Mathf.Clamp01(powerUpTimer / lanternSparksDurationTotal)
        : 0f;

    private void OnCoinCollected(int value)
    {
        AddScore(value);
    }

    private void UpdatePowerUpTimer()
    {
        if (powerUpTimer <= 0f)
            return;

        powerUpTimer -= Time.deltaTime;
        if (powerUpTimer <= 0f)
        {
            powerUpTimer = 0f;
            lanternSparksDurationTotal = 0f;
            activePowerUpMultiplier = 1f;
        }
    }

    private void ResetRunScoreState()
    {
        distanceScore = 0f;
        activePowerUpMultiplier = 1f;
        powerUpTimer = 0f;
        lanternSparksDurationTotal = 0f;
        currentReviveScoreCost = Mathf.Max(1, baseReviveScoreCost);

        if (distanceText != null)
            UpdateScoreText(true);

        UpdateReviveCostUI();
    }

    private void UpdateHighScore()
    {
        int currentScore = Mathf.FloorToInt(distanceScore);
        if (currentScore > cachedHighScore)
        {
            cachedHighScore = currentScore;
            PlayerPrefs.SetInt("HighScore", currentScore);
            if (HighScoreText != null)
                HighScoreText.SetText(HighScoreTextFormat, currentScore);
        }
    }
    #endregion
}
