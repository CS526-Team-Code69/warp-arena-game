using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{

    [DefaultExecutionOrder(1000)]
    public class V2CharacterIllustration : MonoBehaviour
    {
        public V2ArtSet art;
        public SpriteRenderer illustration;
        public Renderer[] whiteboxRenderers;
        public SpriteRenderer hitImpact;
        [Tooltip("Billboard height in world units; tune in the prefab Inspector.")]
        public float height = 1.8f;
        [Header("Candidate presentation tuning")]
        [Range(1f, 16f)] public float pixelBlock = 6f;
        public float walkBob = .065f, walkCyclesPerMetre = .7f;
        public float walkTilt = 3f, hitSquash = .24f, hitRecoil = .15f;
        [Header("Elite outline (owner M116)")]
        [Tooltip("Size of the white silhouette behind an elite's illustration, as a share of the illustration.")]
        public float eliteOutlineScale = 1.12f;
        [Tooltip("Size of the white back-face shell round an elite's whitebox body, as a share of the body.")]
        public float whiteboxOutlineScale = 1.15f;
        Combatant body;
        Enemy enemy;
        Renderer facingMark;
        V2ModelPresentation model;
        Rigidbody motion;
        MaterialPropertyBlock block;
        Sprite sampledSprite;
        Vector4 spriteUv;
        float walkPhase;
        bool started, illustrated, modelVisible;
        SpriteRenderer eliteOutline;
        MeshRenderer eliteShell;
        static Material shellMaterial;

        public SpriteRenderer EliteOutline => eliteOutline;

        public Renderer EliteShell => eliteShell;

        void Awake()
        {
            body = GetComponent<Combatant>(); enemy = body as Enemy;
            model = GetComponent<V2ModelPresentation>();
            motion = GetComponent<Rigidbody>(); block = new MaterialPropertyBlock();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Facing") { facingMark = t.GetComponent<Renderer>(); break; }
        }
        void OnEnable() { started = false; walkPhase = 0f; }
        void OnDisable() { if (hitImpact) hitImpact.enabled = false; ShowIllustrationOutline(false); ShowWhiteboxOutline(false); }

        void LateUpdate()
        {

            if (facingMark && facingMark.enabled == Art.Enabled) facingMark.enabled = !Art.Enabled;
            if (!body || !illustration) return;
            if (!started) { ApplyLook(); started = true; }
            if (illustrated != Art.Enabled || modelVisible != (model && model.UsingModel)) ApplyVisibility();
            bool on = Art.Enabled && illustration.sprite && !modelVisible;
            illustration.enabled = on;
            bool elite = enemy && enemy.ShowsEliteOutline;
            ShowWhiteboxOutline(elite && !on && !modelVisible);
            if (!on) { if (hitImpact) hitImpact.enabled = false; ShowIllustrationOutline(false); return; }
            var gm = GameManager.Instance;
            var camera = gm ? gm.cam : null;
            float speed = motion ? Query.Flat(motion.linearVelocity).magnitude : 0f;
            float dt = gm ? body.Team == Team.Player ? gm.PlayerDeltaTime : gm.WorldDeltaTime : Time.deltaTime;
            if (body.IsAlive && body.State == CharacterState.Normal && !body.ActionsBlocked)
                walkPhase += speed * dt * walkCyclesPerMetre * Mathf.PI * 2f;
            float moving = Mathf.Clamp01(speed / Mathf.Max(.1f, body.moveSpeed));
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * walkBob * moving;
            float squash = body.HitSquash01 * hitSquash;
            float tall = height * (enemy ? enemy.Swell : 1f);                                                  
            var t = illustration.transform;
            t.position = body.Position + Vector3.up * (tall * .5f + bob) - body.LastHitDirection * (body.HitSquash01 * hitRecoil);
            t.rotation = (camera ? camera.transform.rotation : Quaternion.identity) * Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase) * walkTilt * moving);
            SetWorldScale(t, tall * illustration.sprite.bounds.size.x / Mathf.Max(.01f, illustration.sprite.bounds.size.y) * (1f + squash), tall * (1f - squash), illustration.sprite);
            if (camera && Mathf.Abs(Vector3.Dot(body.AimDirection, camera.transform.right)) > .1f)
                illustration.flipX = Vector3.Dot(body.AimDirection, camera.transform.right) < 0f;
            var status = ElementStatus.For(body);
            Color tint = enemy && enemy.definition && enemy.definition.kind == EnemyKind.Hollow ? V2Hud.ElementColors[6] : Color.white;
            if (status && status.Frozen) tint = V2Hud.ElementColors[2];
            if (enemy && enemy.WindupTinted) tint = new Color(1f, .87f, .45f);
            if (enemy && enemy.Cloaked)
            {
                float distance = gm && gm.player ? Vector3.Distance(body.Position, gm.player.Position) : 16f;
                tint = new Color(.5f, .75f, 1f, Mathf.Lerp(.12f, .38f, 1f - Mathf.Clamp01(distance / 16f)));
            }
            illustration.color = tint;
            if (sampledSprite != illustration.sprite)
            {
                sampledSprite = illustration.sprite;
                var r = sampledSprite.rect; var tex = sampledSprite.texture;
                spriteUv = new Vector4(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            }
            block ??= new MaterialPropertyBlock();
            illustration.GetPropertyBlock(block);
            block.SetFloat("_HitFlash", body.HitFlash01);
            block.SetFloat("_PixelBlock", pixelBlock);
            block.SetVector("_SpriteUVRect", spriteUv);
            illustration.SetPropertyBlock(block);
            ShowIllustrationOutline(elite);
            if (!hitImpact) return;
            hitImpact.enabled = body.HitFlash01 > 0f && body.IsAlive;
            if (!hitImpact.enabled || !hitImpact.sprite) return;
            hitImpact.transform.position = body.Position + Vector3.up * height * .5f + (camera ? -camera.transform.forward * .05f : Vector3.zero);
            hitImpact.transform.rotation = camera ? camera.transform.rotation : Quaternion.identity;
            float size = Mathf.Max(.5f, body.radius * 2.4f) * (1.25f - body.HitFlash01 * .25f);
            SetWorldScale(hitImpact.transform, size, size, hitImpact.sprite);
            hitImpact.color = new Color(1f, .95f, .75f, body.HitFlash01);
        }

        void ShowIllustrationOutline(bool show)
        {
            if (!show) { if (eliteOutline) eliteOutline.enabled = false; return; }
            if (!eliteOutline)
            {
                var go = new GameObject("EliteOutline");
                go.transform.SetParent(illustration.transform, false);
                eliteOutline = go.AddComponent<SpriteRenderer>();
                eliteOutline.sharedMaterial = illustration.sharedMaterial;
                eliteOutline.shadowCastingMode = ShadowCastingMode.Off; eliteOutline.receiveShadows = false;
            }
            var t = eliteOutline.transform;
            t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one * eliteOutlineScale;
            eliteOutline.sprite = illustration.sprite; eliteOutline.flipX = illustration.flipX; eliteOutline.color = Color.white;
            eliteOutline.sortingLayerID = illustration.sortingLayerID; eliteOutline.sortingOrder = illustration.sortingOrder - 1;
            block.SetFloat("_HitFlash", 1f);                                                                    
            eliteOutline.SetPropertyBlock(block);
            eliteOutline.enabled = true;
        }

        void ShowWhiteboxOutline(bool show)
        {
            var source = enemy ? enemy.bodyRenderer : null;
            if (!show || !source || !source.enabled) { if (eliteShell) eliteShell.enabled = false; return; }
            if (!eliteShell)
            {
                var mesh = source.GetComponent<MeshFilter>();
                if (!mesh || !mesh.sharedMesh) return;
                var go = new GameObject("EliteShell");
                go.transform.SetParent(source.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                eliteShell = go.AddComponent<MeshRenderer>();
                eliteShell.shadowCastingMode = ShadowCastingMode.Off; eliteShell.receiveShadows = false;
            }
            if (!shellMaterial) shellMaterial = WhiteShellMaterial();
            if (!shellMaterial) { eliteShell.enabled = false; return; }
            eliteShell.sharedMaterial = shellMaterial;
            eliteShell.transform.localScale = Vector3.one * whiteboxOutlineScale;
            eliteShell.enabled = true;
        }

        static Material WhiteShellMaterial()
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var gm = GameManager.Instance;
            var m = unlit ? new Material(unlit) : gm && gm.litMaterial ? new Material(gm.litMaterial) : null;
            if (!m) return null;
            m.name = "EliteShell (white, back faces)";
            if (unlit)
            {
                m.SetFloat("_Surface", 1f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_ZWrite", 0f); m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cull", (float)CullMode.Front);
            return m;
        }

        static void SetWorldScale(Transform t, float width, float height, Sprite sprite)
        {
            var p = t.parent ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(width / Mathf.Max(.001f, sprite.bounds.size.x * Mathf.Abs(p.x)), height / Mathf.Max(.001f, sprite.bounds.size.y * Mathf.Abs(p.y)), 1f);
        }

        void ApplyLook()
        {
            if (enemy && enemy.definition && art)
                foreach (var entry in art.enemyImages)
                    if (string.Equals(entry.id, enemy.definition.kind.ToString(), System.StringComparison.OrdinalIgnoreCase))
                    { illustration.sprite = entry.sprite; break; }
            sampledSprite = null; ApplyVisibility();
            illustration.shadowCastingMode = ShadowCastingMode.Off; illustration.receiveShadows = false;
        }
        void ApplyVisibility()
        {
            illustrated = Art.Enabled;
            modelVisible = model && model.UsingModel;
            bool on = illustrated && illustration.sprite && !modelVisible;
            if (whiteboxRenderers != null) foreach (var renderer in whiteboxRenderers) if (renderer) renderer.enabled = !on && !modelVisible;
            illustration.enabled = on;
        }
    }
}
