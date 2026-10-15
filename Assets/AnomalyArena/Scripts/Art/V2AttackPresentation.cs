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

        void OnDisable()
        {
            if (shortTrail) { shortTrail.emitting = false; shortTrail.Clear(); }
            if (knifeCorpse) knifeCorpse.enabled = false;
            if (knifeCorpseWhitebox) knifeCorpseWhitebox.enabled = false;
            if (knifeReturnMarker) knifeReturnMarker.enabled = false;
        }

        void LateUpdate()
        {
            if (!actor || !illustration || !art) return;
            if (sequence != actor.Sequence)
            {
                sequence = actor.Sequence;
                if (shortTrail) shortTrail.Clear();
            }
            bool field = actor.mode is AttackMode.Field or AttackMode.Pull or AttackMode.Blast;
            bool ground = field || actor.PlantedRecall;
            bool knife = actor.mode == AttackMode.Knife;
            Sprite sprite = SelectSprite(field);
            bool illustrated = Art.Enabled && sprite;
            illustration.enabled = illustrated;
            if (whitebox) whitebox.enabled = !illustrated;
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
            knifeReturnMarker.enabled = carrying && actor.KnifeReturning;
            if (!knifeReturnMarker.enabled || !knifeReturnMarker.sprite) return;
            knifeReturnMarker.transform.position = new Vector3(actor.KnifeReturnPoint.x, groundOffset, actor.KnifeReturnPoint.z);
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
                WeaponForm.Flamethrower => field ? art.flameField : art.fireball,
                WeaponForm.CryoCannon => field ? art.blizzardField : art.frostShard,
                WeaponForm.SporeLauncher => field ? art.poisonField : art.sporePod,
                _ => null
            };
        }
    }
}
