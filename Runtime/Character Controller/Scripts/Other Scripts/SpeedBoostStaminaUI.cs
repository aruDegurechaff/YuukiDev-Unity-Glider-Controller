using UnityEngine;
using UnityEngine.UI;
using YuukiDev.Controller;

namespace YuukiDev.OtherScripts
{
    [DisallowMultipleComponent]
    public class SpeedBoostStaminaUI : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private PlayerController player;

        [Header("Local Space Layout")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 2f, 0f);
        [SerializeField] private float worldDiameter = 0.55f;
        [SerializeField] private int ringTextureSize = 256;
        [SerializeField, Range(0.05f, 0.95f)] private float ringInnerRadius = 0.72f;
        [SerializeField] private int sortingOrder = 20;

        [Header("Facing")]
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private float billboardSmooth = 14f;

        [Header("Visibility")]
        [SerializeField] private bool hideWhenFullAndIdle = true;
        [SerializeField] private float fullHideThreshold = 0.995f;
        [SerializeField] private float fadeSpeed = 6f;

        [Header("Text")]
        [SerializeField] private bool showLabel = true;
        [SerializeField] private string labelText = "Stamina";
        [SerializeField] private int fontSize = 22;

        [Header("Colors")]
        [SerializeField] private Color frameColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color backgroundColor = new Color(0.06f, 0.09f, 0.14f, 0.85f);
        [SerializeField] private Color fillLowColor = new Color(0.97f, 0.30f, 0.25f, 1f);
        [SerializeField] private Color fillHighColor = new Color(0.32f, 0.95f, 0.78f, 1f);
        [SerializeField] private Color textColor = Color.white;

        private static Sprite cachedRingSprite;
        private static int cachedRingSize = -1;
        private static float cachedInnerRadius = -1f;

        private Transform uiRoot;
        private Canvas uiCanvas;
        private RectTransform rootRect;
        private Image frameImage;
        private Image backgroundImage;
        private Image fillImage;
        private Text label;
        private CanvasGroup canvasGroup;
        private Camera targetCamera;
        private bool ownsCanvas;

        private void Awake()
        {
            ResolvePlayer();
            BuildRuntimeUI();
        }

        private void OnValidate()
        {
            worldDiameter = Mathf.Clamp(worldDiameter, 0.08f, 3.5f);
            ringTextureSize = Mathf.Clamp(ringTextureSize, 64, 1024);
            ringInnerRadius = Mathf.Clamp(ringInnerRadius, 0.05f, 0.95f);
            fontSize = Mathf.Clamp(fontSize, 10, 36);
            sortingOrder = Mathf.Clamp(sortingOrder, 0, 5000);
            billboardSmooth = Mathf.Clamp(billboardSmooth, 1f, 40f);
            fullHideThreshold = Mathf.Clamp01(fullHideThreshold);
            fadeSpeed = Mathf.Clamp(fadeSpeed, 0.1f, 40f);
        }

        private void Update()
        {
            if (player == null)
                ResolvePlayer();

            if (player == null || fillImage == null || uiRoot == null)
                return;

            uiRoot.localPosition = localOffset;

            float stamina01 = Mathf.Clamp01(player.BoostNormalized);
            fillImage.fillAmount = stamina01;
            fillImage.color = Color.Lerp(fillLowColor, fillHighColor, stamina01);

            if (label != null)
                label.text = $"{labelText}: {Mathf.RoundToInt(stamina01 * 100f)}%";

            UpdateVisibility(stamina01);

            if (!billboardToCamera)
                return;

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null)
                return;

            Vector3 toCamera = uiRoot.position - targetCamera.transform.position;
            if (toCamera.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            float t = 1f - Mathf.Exp(-billboardSmooth * Time.deltaTime);
            uiRoot.rotation = Quaternion.Slerp(uiRoot.rotation, targetRotation, t);
        }

        private void OnDestroy()
        {
            if (ownsCanvas && uiCanvas != null)
                Destroy(uiCanvas.gameObject);
        }

        private void ResolvePlayer()
        {
            if (player != null)
                return;

            player = GetComponent<PlayerController>();
            if (player == null)
                player = FindAnyObjectByType<PlayerController>();
        }

        private void BuildRuntimeUI()
        {
            if (fillImage != null)
                return;

            targetCamera = Camera.main;
            Transform parent = player != null ? player.transform : transform;
            Sprite ringSprite = GetOrCreateRingSprite();

            GameObject canvasObject = new GameObject("SpeedBoostStaminaUI_Local", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiCanvas = canvasObject.GetComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.WorldSpace;
            uiCanvas.sortingOrder = sortingOrder;
            uiCanvas.worldCamera = targetCamera;
            ownsCanvas = true;
            uiRoot = canvasObject.transform;
            uiRoot.SetParent(parent, false);
            uiRoot.localPosition = localOffset;
            uiRoot.localRotation = Quaternion.identity;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 12f;

            rootRect = CreateUIRect("Root", canvasObject.transform);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(100f, 100f);
            float scale = worldDiameter / 100f;
            rootRect.localScale = new Vector3(scale, scale, scale);

            frameImage = rootRect.gameObject.AddComponent<Image>();
            frameImage.sprite = ringSprite;
            frameImage.color = frameColor;

            RectTransform backgroundRect = CreateUIRect("Background", rootRect);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(5f, 5f);
            backgroundRect.offsetMax = new Vector2(-5f, -5f);

            backgroundImage = backgroundRect.gameObject.AddComponent<Image>();
            backgroundImage.sprite = ringSprite;
            backgroundImage.color = backgroundColor;

            RectTransform fillRect = CreateUIRect("Fill", backgroundRect);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.localRotation = Quaternion.Euler(0f, 0f, -90f);

            fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = ringSprite;
            fillImage.color = fillHighColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = false;
            fillImage.fillAmount = 1f;

            canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 1f;

            if (!showLabel)
                return;

            RectTransform labelRect = CreateUIRect("Label", rootRect);
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -12f);
            labelRect.sizeDelta = new Vector2(220f, 36f);

            label = labelRect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = textColor;
            label.text = labelText;
        }

        private Sprite GetOrCreateRingSprite()
        {
            if (cachedRingSprite != null && cachedRingSize == ringTextureSize && Mathf.Approximately(cachedInnerRadius, ringInnerRadius))
                return cachedRingSprite;

            int size = Mathf.Max(64, ringTextureSize);
            float inner = Mathf.Clamp(ringInnerRadius, 0.05f, 0.95f);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "SpeedBoostRingSprite";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float outerRadius = center;
            float innerRadius = outerRadius * inner;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float alpha = distance <= outerRadius && distance >= innerRadius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            cachedRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            cachedRingSize = size;
            cachedInnerRadius = inner;
            return cachedRingSprite;
        }

        private static RectTransform CreateUIRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private void UpdateVisibility(float stamina01)
        {
            if (canvasGroup == null)
                return;

            bool isBoosting = player != null &&
                              player.CurrentGlideMode == PlayerController.GlideMode.SpeedingUp;

            bool shouldHide = hideWhenFullAndIdle && stamina01 >= fullHideThreshold && !isBoosting;
            float targetAlpha = shouldHide ? 0f : 1f;
            float step = fadeSpeed * Time.deltaTime;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, step);
        }
    }
}
