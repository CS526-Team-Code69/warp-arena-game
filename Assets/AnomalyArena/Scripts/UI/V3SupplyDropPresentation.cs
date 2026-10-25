using UnityEngine;

namespace AnomalyArena
{

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
        [Header("Supply-drop presentation.")]
        [Tooltip("Horizontal offset of the overhead crate; zero is directly above the player.")] public float forwardDistance;
        [Tooltip("Crate landing height above the player's position.")] public float headHeight = 1.8f;
        [Tooltip("Starting drop height; no physics collision is used.")] public float dropHeight = 10f;
        [Tooltip("Descent duration in unscaled seconds.")] public float dropSeconds = .6f;
        [Tooltip("Seconds to remain visible after landing.")] public float holdSeconds = 2f;
        [Tooltip("Pooled impact-light size multiplier.")] public float fadeSeconds = .4f;
        [Tooltip("Vertical menu item offset when expanded.")] public float dustSeconds = .45f;
        [Tooltip("Impact ring diameter in world units.")] public float dustDiameter = 3.3f;
        [Tooltip("UI projection offset of the overhead crate; zero aligns with the world crate.")] public Vector2 uiOffset;
        [Tooltip("Reuses Impact audio once at the landing transition.")] public bool landingSound = true;
        [Tooltip("Show a UI projection so the crate remains visible under full-screen selection screens.")] public bool showUiProjection = true;
        GameManager subscribedGame;
        MaterialPropertyBlock[] blocks;
        Color[] surfaceColor;
        Color dustColor, glowColor, uiDustColor, uiGlowColor;
        Vector3 anchor;
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
            anchor = game.player.Position + direction.normalized * forwardDistance;
            anchor.y = game.player.Position.y + Mathf.Max(0f, headHeight);
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
            if (!game || game.RunPhase != ArenaUiState.SupplyDrop) { Hide(); return; }
            Tick(Time.unscaledTime - began);
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
                openHint.text = landed ? ArenaText.Pair("CLICK THE CRATE TO OPEN","CLICK THE CRATE TO OPEN") : ArenaText.Pair("SUPPLY INCOMING…","SUPPLY INCOMING…");
            }
            if (!overlayRoot || !showUiProjection || !worldCamera || !overlayCanvas) return;
            Vector3 cratePoint = worldCamera.WorldToScreenPoint(crateRoot.position + Vector3.up * .55f);
            Vector3 groundPoint = worldCamera.WorldToScreenPoint(anchor);
            bool visible = cratePoint.z > 0f && groundPoint.z > 0f;
            overlayRoot.gameObject.SetActive(visible);
            if (!visible) return;
            Camera uiCamera = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlayCanvas.worldCamera;
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
