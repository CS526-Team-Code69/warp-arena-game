using UnityEngine;

namespace AnomalyArena
{

    public sealed class V2AttackPresentation : MonoBehaviour
    {
        public AttackActor actor;
        public V2ArtSet art;
        public SpriteRenderer illustration;
        public Renderer whitebox;
        public TrailRenderer shortTrail;
        public SpriteRenderer knifeCorpse, knifeReturnMarker;
        public Renderer knifeCorpseWhitebox;
        public float projectileWidth = .8f;
        public float daggerLength = 1.2f;
        public float groundOffset = .05f;
        int sequence = -1;
        Material savedMaterial;
        Material savedTrailMaterial;
        LineRenderer hookChain;
        bool wasV5;

        void OnDisable()
        {
            if (shortTrail) { shortTrail.emitting = false; shortTrail.Clear(); }
            if (knifeCorpse) knifeCorpse.enabled = false;
            if (knifeCorpseWhitebox) knifeCorpseWhitebox.enabled = false;
            if (knifeReturnMarker) knifeReturnMarker.enabled = false;
            if (hookChain) hookChain.enabled = false;
        }

        void LateUpdate()
        {
            if (!actor || !illustration || !art) return;
            bool fresh = sequence != actor.Sequence;
            if (fresh)
            {
                sequence = actor.Sequence;
                if (shortTrail) shortTrail.Clear();
            }
            if (hookChain && (!V5Vfx.On || actor.mode != AttackMode.Hook)) hookChain.enabled = false;
            if (V5Vfx.On && actor.weapon != null)
            {
                DrawV5(fresh);
                RenderKnifeCorpse(actor.mode == AttackMode.Knife);
                return;
            }
            RestoreMaterials();
            bool field = actor.mode is AttackMode.Field or AttackMode.Pull or AttackMode.Blast;
            bool ground = field || actor.PlantedRecall;
            bool knife = actor.mode == AttackMode.Knife;
            Sprite sprite = SelectSprite(field);
            bool illustrated = Art.Enabled && sprite;
            illustration.enabled = illustrated;
            if (whitebox) whitebox.enabled = !illustrated && !V4CombatVfx.DrawsRocketStrike(actor);
            if (shortTrail)
            {
                shortTrail.transform.position = actor.transform.position;
                shortTrail.emitting = Art.Enabled && !ground && actor.weapon != null;
                shortTrail.enabled = Art.Enabled;
            }
            if (illustrated)
            {
                if (illustration.sprite != sprite) illustration.sprite = sprite;
                var camera = GameManager.Instance ? GameManager.Instance.cam : null;
                float width = field ? actor.radius * 2f : knife || actor.weapon.form == WeaponForm.PhaseDagger ? daggerLength : projectileWidth;
                float depth = field ? width : width * sprite.bounds.size.y / Mathf.Max(.01f, sprite.bounds.size.x);
                illustration.transform.position = ground ? new Vector3(actor.transform.position.x, groundOffset, actor.transform.position.z) : actor.transform.position;
                float angle = actor.direction.sqrMagnitude > .001f ? Mathf.Atan2(actor.direction.x, actor.direction.z) * Mathf.Rad2Deg : 0f;
                bool flat = ground || knife || actor.weapon.form is WeaponForm.PhaseDagger or WeaponForm.CryoCannon;
                illustration.transform.rotation = flat ? Quaternion.Euler(90f, angle - 90f + (knife ? actor.KnifeSpinAngle : 0f), 0f)
                    : camera ? camera.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
                Scale(illustration.transform, width, depth, sprite, flat);
                illustration.color = Color.white;
            }
            RenderKnifeCorpse(knife);
        }

        void DrawV5(bool fresh)
        {
            var fx = V5Vfx.I;
            if (!savedMaterial) savedMaterial = illustration.sharedMaterial;
            if (shortTrail && !savedTrailMaterial) savedTrailMaterial = shortTrail.sharedMaterial;
            wasV5 = true;
            if (whitebox) whitebox.enabled = false;
            bool knife = actor.mode == AttackMode.Knife;
            if (actor.mode is AttackMode.Field or AttackMode.Pull)
            {
                if (shortTrail) { shortTrail.emitting = false; shortTrail.enabled = false; }
                DrawField(fx, fresh);
                return;
            }
            if (actor.mode == AttackMode.Blast)
            {

                illustration.enabled = false;
                if (shortTrail) { shortTrail.emitting = false; shortTrail.enabled = false; }
                return;
            }
            if (actor.mode == AttackMode.Hook) DrawChain(fx);
            var look = V5Vfx.LookOf(actor);
            var sprite = fx.Sprite(look.sprite);
            illustration.enabled = sprite;
            if (!sprite) return;
            illustration.sprite = sprite;
            illustration.sharedMaterial = look.additive ? fx.SpriteAdditive : savedMaterial;
            illustration.color = look.tint;
            var camera = GameManager.Instance ? GameManager.Instance.cam : null;
            illustration.transform.position = actor.PlantedRecall ? new Vector3(actor.transform.position.x, groundOffset, actor.transform.position.z) : actor.transform.position;
            float angle = actor.direction.sqrMagnitude > .001f ? Mathf.Atan2(actor.direction.x, actor.direction.z) * Mathf.Rad2Deg : 0f;
            illustration.transform.rotation = look.flat ? Quaternion.Euler(90f, angle - 90f + (knife ? actor.KnifeSpinAngle : 0f), 0f)
                : camera ? camera.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
            Scale(illustration.transform, look.length, look.width, sprite, look.flat);
            if (shortTrail)
            {
                if (fresh)
                {
                    shortTrail.sharedMaterial = fx.TrailMaterial;
                    shortTrail.startColor = look.trail;
                    shortTrail.endColor = new Color(look.trail.r, look.trail.g, look.trail.b, 0f);
                    shortTrail.startWidth = look.trailWidth;
                    shortTrail.endWidth = 0f;
                    shortTrail.time = look.trailTime;
                }
                shortTrail.transform.position = actor.transform.position;
                shortTrail.enabled = true;
                shortTrail.emitting = true;
            }
            fx.ProjectileTick(actor);
        }

        void DrawChain(V5Vfx fx)
        {
            if (!hookChain)
            {
                var go = new GameObject("V5 Hook Chain");
                go.transform.SetParent(transform, false);
                hookChain = go.AddComponent<LineRenderer>();
                hookChain.useWorldSpace = true;
                hookChain.positionCount = 2;
                hookChain.sharedMaterial = fx.TrailMaterial;
                hookChain.numCapVertices = 2;
                hookChain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hookChain.receiveShadows = false;
            }
            hookChain.enabled = actor.owner;
            if (!actor.owner) return;
            hookChain.SetPosition(0, Query.AtCastHeight(actor.owner.Position));
            hookChain.SetPosition(1, actor.transform.position);
            hookChain.startWidth = .1f;
            hookChain.endWidth = .1f;
            hookChain.startColor = hookChain.endColor = new Color(.62f, .82f, 1f, .9f);
        }

        void DrawField(V5Vfx fx, bool fresh)
        {
            ElementKind kind = actor.weapon.form switch
            {
                WeaponForm.Flamethrower or WeaponForm.ScatterCannon => ElementKind.Fire,
                WeaponForm.CryoCannon => ElementKind.Frost,
                WeaponForm.SporeLauncher => ElementKind.Poison,
                _ => actor.element
            };
            Vector3 centre = actor.transform.position;
            if (actor.delay <= 0f)
                switch (kind)
                {
                    case ElementKind.Fire: fx.FireField(centre, actor.radius); break;
                    case ElementKind.Poison: fx.PoisonField(centre, actor.radius); break;
                    case ElementKind.Frost: fx.FrostField(centre, actor.radius); if (fresh) fx.FrostMark(centre, actor.radius, Mathf.Max(1f, actor.remaining), .55f); break;
                    default: fx.BuffMotes(centre, actor.radius, V5Vfx.ElementTint(kind), 12f); break;
                }

            var edge = fx.Sprite("ring");
            illustration.enabled = edge;
            if (!edge) return;
            illustration.sprite = edge;
            illustration.sharedMaterial = fx.SpriteAdditive;
            Color tint = V5Vfx.ElementTint(kind);
            float fade = Mathf.Clamp01(actor.remaining / .4f);
            illustration.color = new Color(tint.r, tint.g, tint.b, .2f * fade);
            illustration.transform.position = new Vector3(centre.x, groundOffset + .03f, centre.z);
            illustration.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Scale(illustration.transform, actor.radius * 2f, actor.radius * 2f, edge, true);
        }

        void RestoreMaterials()
        {
            if (!wasV5) return;
            wasV5 = false;
            if (savedMaterial) illustration.sharedMaterial = savedMaterial;
            if (shortTrail && savedTrailMaterial) shortTrail.sharedMaterial = savedTrailMaterial;
        }

        void RenderKnifeCorpse(bool knife)
        {
            bool carrying = knife && actor.HasKnifeCorpse;
            bool spriteVisible = carrying && Art.Enabled && actor.KnifeCorpseSprite;
            if (knifeCorpse)
            {
                knifeCorpse.enabled = spriteVisible;
                if (spriteVisible)
                {
                    knifeCorpse.sprite = actor.KnifeCorpseSprite;
                    var camera = GameManager.Instance ? GameManager.Instance.cam : null;
                    float size = Mathf.Max(.4f, actor.KnifeCorpseRadius * 2f);
                    knifeCorpse.transform.position = actor.transform.position + Vector3.up * size * .45f;
                    knifeCorpse.transform.rotation = (camera ? camera.transform.rotation : Quaternion.identity) * Quaternion.Euler(0f, 0f, -65f);
                    Scale(knifeCorpse.transform, size, size * knifeCorpse.sprite.bounds.size.y / Mathf.Max(.01f, knifeCorpse.sprite.bounds.size.x), knifeCorpse.sprite, false);
                    knifeCorpse.color = new Color(.8f, .85f, 1f);
                }
            }
            if (knifeCorpseWhitebox)
            {
                knifeCorpseWhitebox.enabled = carrying && !spriteVisible;
                if (carrying)
                {
                    knifeCorpseWhitebox.transform.position = actor.transform.position;
                    var p = knifeCorpseWhitebox.transform.parent.lossyScale;
                    float size = Mathf.Max(.25f, actor.KnifeCorpseRadius * 2f);
                    knifeCorpseWhitebox.transform.localScale = new Vector3(size / Mathf.Max(.001f, Mathf.Abs(p.x)), size / Mathf.Max(.001f, Mathf.Abs(p.y)), size / Mathf.Max(.001f, Mathf.Abs(p.z)));
                }
            }
            if (!knifeReturnMarker) return;

            bool spinHome = knife && actor.weapon != null && actor.weapon.Has(UpgradeAxis.Twist);
            knifeReturnMarker.enabled = knife && (spinHome || actor.KnifeReturning);
            if (!knifeReturnMarker.enabled || !knifeReturnMarker.sprite) return;
            Vector3 home = actor.KnifeReturning ? actor.KnifeReturnPoint : actor.KnifeHomePoint;
            knifeReturnMarker.transform.position = new Vector3(home.x, groundOffset, home.z);
            knifeReturnMarker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Scale(knifeReturnMarker.transform, 1.15f, 1.15f, knifeReturnMarker.sprite, true);
        }

        static void Scale(Transform t, float width, float height, Sprite sprite, bool flat)
        {
            var p = t.parent ? t.parent.lossyScale : Vector3.one;
            float vertical = flat ? Mathf.Abs(p.z) : Mathf.Abs(p.y);
            t.localScale = new Vector3(width / Mathf.Max(.001f, sprite.bounds.size.x * Mathf.Abs(p.x)), height / Mathf.Max(.001f, sprite.bounds.size.y * vertical), 1f);
        }

        Sprite SelectSprite(bool field)
        {
            if (actor.weapon == null) return null;
            if (actor.mode == AttackMode.Knife) return art.phaseDagger;
            return actor.weapon.form switch
            {
                WeaponForm.PhaseDagger => art.phaseDagger,
                WeaponForm.PlasmaLauncher => art.plasmaBlob,
                WeaponForm.ScatterCannon => field ? art.flameField : null,                                                                    
                WeaponForm.Flamethrower => field ? art.flameField : art.fireball,
                WeaponForm.CryoCannon => field ? art.blizzardField : art.frostShard,
                WeaponForm.SporeLauncher => field ? art.poisonField : art.sporePod,
                _ => null
            };
        }
    }
}
