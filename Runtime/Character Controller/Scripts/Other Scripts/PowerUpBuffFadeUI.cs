using UnityEngine;
using UnityEngine.UI;
using YuukiDev.Controller;

namespace YuukiDev.OtherScripts
{
    [DisallowMultipleComponent]
    public class PowerUpBuffFadeUI : MonoBehaviour
    {
        private enum ActiveBuff
        {
            None,
            WindOrb,
            PhantomGrace,
            LanternSparks,
            FeatherSlip
        }

        [Header("Source")]
        [SerializeField] private PlayerController player;

        [Header("Layout")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.55f, 0f);
        [SerializeField] private float worldSize = 0.32f;
        [SerializeField] private int sortingOrder = 21;

        [Header("Facing")]
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private float billboardSmooth = 14f;

        [Header("Visibility")]
        [SerializeField] private bool showWindOrbBuff = true;
        [SerializeField] private float fadeSpeed = 6f;

        [Header("Icons")]
        [SerializeField] private Sprite windOrbSprite;
        [SerializeField] private Sprite phantomGraceSprite;
        [SerializeField] private Sprite lanternSparksSprite;
        [SerializeField] private Sprite featherSlipSprite;

        [Header("Colors")]
        [SerializeField] private Color windOrbColor = new Color(0.3f, 0.85f, 1f, 1f);
        [SerializeField] private Color phantomGraceColor = new Color(0.52f, 1f, 1f, 1f);
        [SerializeField] private Color lanternSparksColor = new Color(1f, 0.74f, 0.24f, 1f);
        [SerializeField] private Color featherSlipColor = new Color(0.62f, 1f, 0.82f, 1f);

        private static Sprite fallbackSprite;
        private static int fallbackSpriteSize = -1;

        private Transform uiRoot;
        private Canvas uiCanvas;
        private CanvasGroup canvasGroup;
        private Image buffImage;
        private Camera targetCamera;
        private ActiveBuff preferredBuff = ActiveBuff.None;
        private float currentAlpha;

        private void Awake()
        {
            ResolvePlayer();
            BuildRuntimeUI();
        }

        private void OnEnable()
        {
            PowerUp.PowerUpCollected += OnPowerUpCollected;
        }

        private void OnDisable()
        {
            PowerUp.PowerUpCollected -= OnPowerUpCollected;
        }

        private void OnValidate()
        {
            worldSize = Mathf.Clamp(worldSize, 0.06f, 2f);
            sortingOrder = Mathf.Clamp(sortingOrder, 0, 5000);
            billboardSmooth = Mathf.Clamp(billboardSmooth, 1f, 40f);
            fadeSpeed = Mathf.Clamp(fadeSpeed, 0.1f, 40f);
        }

        private void Update()
        {
            ResolvePlayer();
            if (player == null || uiRoot == null || buffImage == null || canvasGroup == null)
                return;

            uiRoot.localPosition = localOffset;

            ActiveBuff activeBuff = ResolveDisplayedBuff(out float remaining01);
            bool hasActiveBuff = activeBuff != ActiveBuff.None && remaining01 > 0f;

            if (hasActiveBuff)
            {
                if (buffImage.sprite == null)
                    buffImage.sprite = GetFallbackSprite();

                buffImage.sprite = GetSpriteForBuff(activeBuff) ?? GetFallbackSprite();
                Color tint = GetColorForBuff(activeBuff);
                tint.a = 1f;
                buffImage.color = tint;
                buffImage.enabled = true;
            }

            float targetAlpha = hasActiveBuff ? Mathf.Clamp01(remaining01) : 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            canvasGroup.alpha = currentAlpha;

            if (currentAlpha <= 0.001f && !hasActiveBuff)
                buffImage.enabled = false;

            UpdateBillboard();
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
            if (buffImage != null)
                return;

            Transform parent = player != null ? player.transform : transform;
            targetCamera = Camera.main;

            GameObject canvasObject = new GameObject("PowerUpBuffFadeUI_Local", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiCanvas = canvasObject.GetComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.WorldSpace;
            uiCanvas.sortingOrder = sortingOrder;
            uiCanvas.worldCamera = targetCamera;

            uiRoot = canvasObject.transform;
            uiRoot.SetParent(parent, false);
            uiRoot.localPosition = localOffset;
            uiRoot.localRotation = Quaternion.identity;

            RectTransform rootRect = CreateUIRect("Root", uiRoot);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(100f, 100f);
            float scale = worldSize / 100f;
            rootRect.localScale = new Vector3(scale, scale, scale);

            buffImage = rootRect.gameObject.AddComponent<Image>();
            buffImage.sprite = GetFallbackSprite();
            buffImage.color = Color.white;
            buffImage.preserveAspect = true;
            buffImage.raycastTarget = false;

            canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
        }

        private ActiveBuff ResolveDisplayedBuff(out float remaining01)
        {
            remaining01 = 0f;
            ActiveBuff preferred = preferredBuff;
            if (preferred != ActiveBuff.None && TryGetBuffRemaining01(preferred, out float preferredRemaining))
            {
                remaining01 = preferredRemaining;
                return preferred;
            }

            if (TryGetBuffRemaining01(ActiveBuff.PhantomGrace, out float phantomRemaining))
            {
                remaining01 = phantomRemaining;
                return ActiveBuff.PhantomGrace;
            }

            if (TryGetBuffRemaining01(ActiveBuff.LanternSparks, out float lanternRemaining))
            {
                remaining01 = lanternRemaining;
                return ActiveBuff.LanternSparks;
            }

            if (TryGetBuffRemaining01(ActiveBuff.FeatherSlip, out float featherRemaining))
            {
                remaining01 = featherRemaining;
                return ActiveBuff.FeatherSlip;
            }

            if (showWindOrbBuff && TryGetBuffRemaining01(ActiveBuff.WindOrb, out float windRemaining))
            {
                remaining01 = windRemaining;
                return ActiveBuff.WindOrb;
            }

            preferredBuff = ActiveBuff.None;
            return ActiveBuff.None;
        }

        private bool TryGetBuffRemaining01(ActiveBuff buff, out float remaining01)
        {
            remaining01 = 0f;
            if (player == null)
                return false;

            switch (buff)
            {
                case ActiveBuff.WindOrb:
                    remaining01 = player.WindOrbNormalized;
                    return player.WindOrbRemaining > 0f && remaining01 > 0f;
                case ActiveBuff.PhantomGrace:
                    remaining01 = player.PhantomGraceNormalized;
                    return player.PhantomGraceRemaining > 0f && remaining01 > 0f;
                case ActiveBuff.LanternSparks:
                    remaining01 = player.LanternSparksNormalized;
                    return player.LanternSparksRemaining > 0f && remaining01 > 0f;
                case ActiveBuff.FeatherSlip:
                    remaining01 = player.FeatherSlipNormalized;
                    return player.FeatherSlipRemaining > 0f && remaining01 > 0f;
                default:
                    return false;
            }
        }

        private void OnPowerUpCollected(PowerUp.PowerUpType type, PlayerController collector)
        {
            if (collector == null)
                return;

            if (player != null && collector != player)
                return;

            if (player == null)
                player = collector;

            switch (type)
            {
                case PowerUp.PowerUpType.WindOrb:
                    preferredBuff = ActiveBuff.WindOrb;
                    break;
                case PowerUp.PowerUpType.GraceToken:
                    preferredBuff = ActiveBuff.PhantomGrace;
                    break;
                case PowerUp.PowerUpType.LanternSparks:
                    preferredBuff = ActiveBuff.LanternSparks;
                    break;
                case PowerUp.PowerUpType.FeatherSlip:
                    preferredBuff = ActiveBuff.FeatherSlip;
                    break;
            }
        }

        private void UpdateBillboard()
        {
            if (!billboardToCamera || uiRoot == null)
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

        private Sprite GetSpriteForBuff(ActiveBuff buff)
        {
            switch (buff)
            {
                case ActiveBuff.WindOrb:
                    return windOrbSprite;
                case ActiveBuff.PhantomGrace:
                    return phantomGraceSprite;
                case ActiveBuff.LanternSparks:
                    return lanternSparksSprite;
                case ActiveBuff.FeatherSlip:
                    return featherSlipSprite;
                default:
                    return null;
            }
        }

        private Color GetColorForBuff(ActiveBuff buff)
        {
            switch (buff)
            {
                case ActiveBuff.WindOrb:
                    return windOrbColor;
                case ActiveBuff.PhantomGrace:
                    return phantomGraceColor;
                case ActiveBuff.LanternSparks:
                    return lanternSparksColor;
                case ActiveBuff.FeatherSlip:
                    return featherSlipColor;
                default:
                    return Color.white;
            }
        }

        private static RectTransform CreateUIRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Sprite GetFallbackSprite()
        {
            const int size = 64;
            if (fallbackSprite != null && fallbackSpriteSize == size)
                return fallbackSprite;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "PowerUpBuffFallbackSprite";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float radius = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float alpha = distance <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            fallbackSpriteSize = size;
            return fallbackSprite;
        }
    }
}
