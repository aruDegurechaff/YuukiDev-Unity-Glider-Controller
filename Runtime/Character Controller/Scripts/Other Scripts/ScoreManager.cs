using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using YuukiDev.OtherScripts;
using YuukiDev.Controller;

public partial class ScoreManager : MonoBehaviour
{
    #region Variables
    public static ScoreManager Instance;
    private const string ScoreTextFormat = "Score: {0}";
    private const string HighScoreTextFormat = "High Score: {0}";
    private const string ReviveCostTextFormat = "Revive Cost: {0}";

    [Header("UI")]
    public TMP_Text distanceText;
    public TMP_Text HighScoreText;

    [Header("Score Settings")]
    public float distanceScore = 0f;

    [Header("Power Up Score")]
    [SerializeField] private float activePowerUpMultiplier = 1f;
    [SerializeField] private float powerUpTimer = 0f;
    private float lanternSparksDurationTotal = 0f;

    [Header("Game Over UI")]
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private Button reviveButton;
    [SerializeField] private Button playAgainButton;

    [Header("Revive Settings")]
    [SerializeField] private bool enableRevive = true;
    [SerializeField] private int baseReviveScoreCost = 500;
    [SerializeField] private TMP_Text reviveCostText;
    [SerializeField] private float reviveMessageDuration = 1.2f;

    [Header("Respawn")]
    [SerializeField] private Transform respawnAnchor;
    [SerializeField] private bool spawnPlayerAtAnchorOnStart = true;
    [SerializeField] private PlayerController playerPrefab;

    [Header("Runtime Optimization")]
    [SerializeField] private float dependencyResolveInterval = 0.75f;

    private MovementTracker movementTracker;
    private ProximityChecker grazeChecker;
    private bool isGameOver = false;
    private int currentReviveScoreCost = 0;
    private Coroutine reviveMessageRoutine;
    private PlayerController downedPlayer;
    private CursorManager cursorManager;
    private int cachedHighScore = 0;
    private int lastDisplayedScore = int.MinValue;
    private float nextDependencyResolveTime = 0f;
    private bool warnedMissingMovementTracker = false;
    private bool warnedMissingGrazeChecker = false;
    #endregion

    #region Unity Events
    /*
     * Core lifecycle
     * by: YuukiDev
     *
     * Keeps event hooks centralized and ensures
     * score systems initialize safely on load.
     */
    private void OnEnable()
    {
        PowerUp.LanternSparksCollected += ApplyLanternSparkBoost;
        PowerUp.CoinCollected += OnCoinCollected;
        PlayerController.GameOverTriggered += OnPlayerGameOver;
        HookButtons();
    }

    private void OnDisable()
    {
        PowerUp.LanternSparksCollected -= ApplyLanternSparkBoost;
        PowerUp.CoinCollected -= OnCoinCollected;
        PlayerController.GameOverTriggered -= OnPlayerGameOver;
        UnhookButtons();

        if (reviveMessageRoutine != null)
        {
            StopCoroutine(reviveMessageRoutine);
            reviveMessageRoutine = null;
        }
    }

    private void Awake()
    {
        Instance = this;
        ResolveCursorManager();
        InitializePlayerAtStart();

        ResolveDependencies();

        cachedHighScore = PlayerPrefs.GetInt("HighScore", 0);
        if (HighScoreText != null)
            HighScoreText.SetText(HighScoreTextFormat, cachedHighScore);

        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        if (reviveButton != null)
            reviveButton.gameObject.SetActive(false);
        if (playAgainButton != null)
            playAgainButton.gameObject.SetActive(false);

        currentReviveScoreCost = Mathf.Max(1, baseReviveScoreCost);
        UpdateReviveCostUI();
        SetGameplayCursor(true);

        // If game over froze time in a previous run, ensure scoring can progress.
        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;
    }

    private void LateUpdate()
    {
        if (isGameOver)
            return;

        if ((movementTracker == null || grazeChecker == null) && Time.unscaledTime >= nextDependencyResolveTime)
        {
            nextDependencyResolveTime = Time.unscaledTime + Mathf.Max(0.1f, dependencyResolveInterval);
            ResolveDependencies();
        }

        UpdatePowerUpTimer();
        FinalScoring();
        UpdateScoreText();

        UpdateHighScore();
    }
    #endregion
}
