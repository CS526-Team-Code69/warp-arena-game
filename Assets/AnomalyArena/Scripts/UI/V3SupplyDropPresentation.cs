using UnityEngine;

namespace AnomalyArena
{
    /// <summary>One saved cosmetic crate, driven only by accepted weapon-choice events. It awards nothing and creates no objects.</summary>
    [DisallowMultipleComponent]
    public sealed class V3SupplyDropPresentation : MonoBehaviour
    {
        public GameManager game;
        public WorldMap world;
        public Transform dropRoot, crateRoot;
        public Renderer[] crateSurfaces;
        public SpriteRenderer worldDust, worldGlow;
        public RectTransform overlayRoot;
        public UnityEngine.UI.Image uiCrate, uiDust, uiGlow;
        public UnityEngine.UI.Button openButton;
        public UnityEngine.UI.Text openHint;
        public Canvas overlayCanvas;
        public Camera worldCamera;
        [Header("天降军需箱：只控制表现")]
        [Tooltip("头顶军需箱的水平偏移；0表示正上方。")] public float forwardDistance;
        [Tooltip("箱体落点相对玩家Position的头顶高度。")] public float headHeight = 1.8f;
        [Tooltip("下降起始高度；不使用物理碰撞。")] public float dropHeight = 10f;
        [Tooltip("下降秒数，使用不缩放时间。")] public float dropSeconds = .6f;
        [Tooltip("落地之后保持可见的秒数。")] public float holdSeconds = 2f;
        [Tooltip("保持结束后的淡出秒数。")] public float fadeSeconds = .4f;
        [Tooltip("尘环展开的秒数。")] public float dustSeconds = .45f;
        [Tooltip("尘环最大直径，世界单位。")] public float dustDiameter = 3.3f;
        [Tooltip("头顶箱体的UI投影偏移；默认0，与世界箱体重合。")] public Vector2 uiOffset;
        [Tooltip("复用已有Impact声音，仅实际落地边沿一次。")] public bool landingSound = true;
        [Tooltip("展示UI投影，使旧全屏选择页覆盖世界时仍能看见箱体。")] public bool showUiProjection = true;
        GameManager subscribedGame;
        MaterialPropertyBlock[] blocks;
        Color[] surfaceColor;
        Color dustColor, glowColor, uiDustColor, uiGlowColor;
        Vector3 anchor, dropDirection;
        float began;
        bool active, landed;
        void Awake()
        {
            int n = crateSurfaces?.Length ?? 0; blocks = new MaterialPropertyBlock[n]; surfaceColor = new Color[n];
            for (int i = 0; i < n; i++)
            {
                blocks[i] = new MaterialPropertyBlock();
                var renderer = crateSurfaces[i]; var material = renderer ? renderer.sharedMaterial : null;
                surfaceColor[i] = material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            }
            dustColor = worldDust ? worldDust.color : Color.white; glowColor = worldGlow ? worldGlow.color : Color.white;
            uiDustColor = uiDust ? uiDust.color : Color.white; uiGlowColor = uiGlow ? uiGlow.color : Color.white;
            savedCrateSprite = uiCrate ? uiCrate.sprite : null;
        }
        // 2026-11-10: in whitebox the crate's picture over the screen is the whitebox crate seen from the side (WhiteboxIcons), the saved
        // picture with the art on; checked at each tick, so the art switch shows at once.
        Sprite savedCrateSprite;
        void ShowCratePicture()
        {
            if (!uiCrate) return;
            var sprite = Art.Enabled ? savedCrateSprite : WhiteboxIcons.Look("crate");
            if (sprite && uiCrate.sprite != sprite) uiCrate.sprite = sprite;
        }
        void OnEnable()
        {
            if (!Application.isPlaying) return;
            subscribedGame = game;
            if (subscribedGame) subscribedGame.WeaponSelectionOpened += Game_WeaponSelectionOpened;
            Hide();
        }
        void OnDisable()
        {
            if (subscribedGame) subscribedGame.WeaponSelectionOpened -= Game_WeaponSelectionOpened;
            subscribedGame = null; Hide();
        }
        void Game_WeaponSelectionOpened()
        {
            if (!game || !game.player || !dropRoot || !crateRoot) return;
            var direction = Query.Flat(game.player.AimDirection);
            if (direction.sqrMagnitude < .0001f) direction = Query.Flat(game.player.transform.forward);
            if (direction.sqrMagnitude < .0001f) direction = Vector3.forward;
            dropDirection = direction.normalized;
            anchor = AbovePlayer();
            dropRoot.position = anchor;
            dropRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            crateRoot.localPosition = Vector3.up * Mathf.Max(0f, dropHeight);
            active = true; landed = false; began = Time.unscaledTime;
            if (openButton) openButton.interactable = false;
            dropRoot.gameObject.SetActive(true);
            if (overlayRoot) overlayRoot.gameObject.SetActive(showUiProjection);
            SetCrateAlpha(1f); Tick(0f);
        }
        void LateUpdate()
        {
            if (!active) return;
            // The pause menu (Esc while the build phase's crate comes down) leaves the crate where it is.
            if (game && game.RunPhase == ArenaUiState.Paused) return;
            if (!game || game.RunPhase != ArenaUiState.SupplyDrop) { Hide(); return; }
            // The build phase's player walks while the crate comes down: it stays above their head, always in reach.
            if (game.player) { anchor = AbovePlayer(); dropRoot.position = anchor; }
            Tick(Time.unscaledTime - began);
        }
        Vector3 AbovePlayer()
        {
            var at = game.player.Position + dropDirection * forwardDistance;
            at.y = game.player.Position.y + Mathf.Max(0f, headHeight);
            return at;
        }
        void Tick(float age)
        {
            float falling = Mathf.Max(.01f, dropSeconds), hold = Mathf.Max(0f, holdSeconds), fade = Mathf.Max(.01f, fadeSeconds);
            bool waiting = game && game.RunPhase == ArenaUiState.SupplyDrop;
            if (!waiting && age >= falling + hold + fade) { Hide(); return; }
            float u = Mathf.Clamp01(age / falling);
            crateRoot.localPosition = Vector3.up * Mathf.Max(0f, dropHeight) * (1f - u * u);
            float afterLand = Mathf.Max(0f, age - falling);
            float alpha = waiting ? 1f : 1f - Mathf.Clamp01((afterLand - hold) / fade);
            if (!landed && age >= falling)
            {
                landed = true;
                game.MarkSupplyLanded();
                if (V5Vfx.On && game.player) V5Vfx.I.GroundSlam(game.player.Position, 1.8f, new Color(.82f, .76f, .66f), false);
                // Opened by a click only; the button is not selected for a keyboard confirm (Enter and Space do nothing).
                if (openButton) openButton.interactable = true;
                if (landingSound) V2Audio.Instance?.PlayWorld(ArenaSoundCue.Impact, anchor);
            }
            SetCrateAlpha(alpha);
            float dust01 = landed ? Mathf.Clamp01(afterLand / Mathf.Max(.01f, dustSeconds)) : 0f;
            float dustAlpha = landed ? (1f - dust01) * alpha : 0f;
            float diameter = Mathf.Lerp(.65f, Mathf.Max(.65f, dustDiameter), dust01);
            if (worldDust) { worldDust.transform.localScale = Vector3.one * (diameter / Mathf.Max(.01f, worldDust.sprite ? worldDust.sprite.bounds.size.x : 1f)); worldDust.color = Alpha(dustColor, dustAlpha); }
            if (worldGlow) worldGlow.color = Alpha(glowColor, landed ? alpha * .8f : .18f + u * .3f);
            if (openHint)
            {
                if (ArenaText.Font) openHint.font = ArenaText.Font;
                openHint.text = landed ? ArenaText.Pair("点击箱子打开", "CLICK THE CRATE TO OPEN") : ArenaText.Pair("军需箱正在落下…", "SUPPLY INCOMING…");
            }
            if (!overlayRoot || !showUiProjection || !worldCamera || !overlayCanvas) return;
            Vector3 cratePoint = worldCamera.WorldToScreenPoint(crateRoot.position + Vector3.up * .55f);
            Vector3 groundPoint = worldCamera.WorldToScreenPoint(anchor);
            bool visible = cratePoint.z > 0f && groundPoint.z > 0f;
            overlayRoot.gameObject.SetActive(visible);
            if (!visible) return;
            Camera uiCamera = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlayCanvas.worldCamera;
            ShowCratePicture();
            if (uiCrate && RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, cratePoint, uiCamera, out var crateAt))
            {
                uiCrate.rectTransform.anchoredPosition = crateAt + uiOffset;
                if (!waiting) uiCrate.color = new Color(1f,1f,1f,alpha);
                float squash = landed ? Mathf.Clamp01(1f - afterLand / .14f) : 0f;
                uiCrate.rectTransform.localScale = new Vector3(1f + squash * .12f, 1f - squash * .1f, 1f);
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, groundPoint, uiCamera, out var groundAt))
            {
                if (uiDust) { uiDust.rectTransform.anchoredPosition = groundAt + uiOffset; uiDust.rectTransform.localScale = Vector3.one * Mathf.Lerp(.45f,1.45f,dust01); uiDust.color = Alpha(uiDustColor,dustAlpha); }
                if (uiGlow) { uiGlow.rectTransform.anchoredPosition = groundAt + uiOffset; uiGlow.color = Alpha(uiGlowColor,landed ? alpha : u * .4f); }
            }
        }
        void SetCrateAlpha(float alpha)
        {
            for (int i = 0; i < (crateSurfaces?.Length ?? 0); i++)
            {
                var renderer = crateSurfaces[i]; if (!renderer || blocks == null || i >= blocks.Length) continue;
                renderer.GetPropertyBlock(blocks[i]); var tint = surfaceColor[i]; tint.a *= alpha;
                blocks[i].SetColor("_BaseColor",tint); blocks[i].SetColor("_Color",tint); renderer.SetPropertyBlock(blocks[i]);
            }
        }
        void Hide()
        {
            active = false; landed = false;
            if (openButton) openButton.interactable = false;
            if (dropRoot) dropRoot.gameObject.SetActive(false);
            if (overlayRoot) overlayRoot.gameObject.SetActive(false);
        }
        static Color Alpha(Color color, float alpha) { color.a *= Mathf.Clamp01(alpha); return color; }
    }
}
