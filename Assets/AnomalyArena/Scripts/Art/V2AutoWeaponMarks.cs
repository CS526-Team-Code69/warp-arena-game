using UnityEngine;

namespace AnomalyArena
{

    public sealed class V2AutoWeaponMarks : MonoBehaviour
    {
        public RunWeapons weapons;
        [Tooltip("Ring of the Deflect force field round the player.")] public Color fieldColor = new(.55f, .9f, 1f, .55f);
        [Tooltip("Energy Shield ring round the player while it is up.")] public Color shieldColor = new(.6f, 1f, .95f, .9f);
        [Tooltip("Ring that flashes after the Energy Shield broke, while the next enemy hit would cost Energy.")] public Color overloadColor = new(1f, .45f, .75f, .9f);
        [Tooltip("Whirl tractor beam from the drone to the dragged enemy.")] public Color beamColor = new(.55f, .95f, 1f, .85f);
        [Tooltip("The player's afterimage where the Whirl will slam the enemy.")] public Color afterimageColor = new(.55f, .85f, 1f, .4f);
        [Tooltip("Drone body while it flies (the whitebox disc; the illustrated drone is drawn in its own colours).")] public Color droneColor = new(.7f, .95f, 1f, .95f);
        [Tooltip("Drone lying on the ground to cool down, and the ring where the player steps on it.")] public Color droneDownColor = new(.55f, .55f, .6f, .9f);
        [Min(.01f), Tooltip("Width of the rings and the beam, cells.")] public float lineWidth = .08f;
        [Min(.1f), Tooltip("Width of the illustrated drone, cells.")] public float droneWidth = 1f;
        [Min(.01f), Tooltip("Seconds between two smoke puffs of a drone cooling down.")] public float smokeInterval = .3f;
        const int Segments = 48;
        Material material;
        int sortingLayer, sortingOrder;
        float nextSearch;
        V2ArtSet art;
        V2CharacterIllustration look;
        Combatant holder;
        LineRenderer fieldRing, shieldRing, beam, ghostRing, ghostFill, droneDisc, droneRim, dropRing;
        MeshRenderer fieldDisc, shieldDisc;
        SpriteRenderer droneSprite, ghostSprite;
        Vector3 droneShown;
        bool droneShownSet;
        float smokeAt;

        void OnDisable()
        {
            foreach (var line in new[] { fieldRing, shieldRing, beam, ghostRing, ghostFill, droneDisc, droneRim, dropRing }) if (line) line.enabled = false;
            if (droneSprite) droneSprite.enabled = false;
            if (ghostSprite) ghostSprite.enabled = false;
            if (fieldDisc) fieldDisc.enabled = false;
            if (shieldDisc) shieldDisc.enabled = false;
            droneShownSet = false;
        }

        void LateUpdate()
        {
            if (!weapons) weapons = GetComponent<RunWeapons>();
            if (!holder && weapons) holder = weapons.GetComponent<Combatant>();
            var player = holder;
            bool show = player && player.IsAlive && player.gameObject.activeInHierarchy;
            if (show && !material && Time.unscaledTime >= nextSearch) FindLook();
            if (!material) show = false;
            RunWeapon drone = null, deflect = null, drain = null;
            if (show)
                foreach (var w in weapons.Auto)
                {
                    if (w == null) continue;
                    if (w.form == WeaponForm.SentryDrone) drone = w;
                    else if (w.form == WeaponForm.ShieldRing) { if (w.variant == 0) deflect = w; else drain = w; }
                }
            DrawField(deflect, player);
            DrawShield(drain, player);
            DrawDrone(drone, player);
            DrawWhirl(drone, player);
        }

        void FindLook()
        {
            nextSearch = Time.unscaledTime + 1f;
            look = GetComponent<V2CharacterIllustration>();
            var telegraph = FindAnyObjectByType<V2EnemyTelegraphPresentation>(FindObjectsInactive.Include);
            SpriteRenderer source = telegraph && telegraph.illustration ? telegraph.illustration : look ? look.illustration : null;
            if (!source) return;
            material = source.sharedMaterial;
            sortingLayer = source.sortingLayerID;
            sortingOrder = source.sortingOrder;
            art = telegraph && telegraph.art ? telegraph.art : look ? look.art : null;
        }

        void DrawField(RunWeapon deflect, Combatant player)
        {
            if (V5Vfx.On)
            {

                if (fieldRing) fieldRing.enabled = false;
                bool on = deflect != null;
                if (on && !fieldDisc) fieldDisc = V5Vfx.I.MakeFlatDisc("DeflectField", "hex", transform);
                if (fieldDisc)
                {
                    fieldDisc.enabled = on;
                    if (on) V5Vfx.PlaceDisc(fieldDisc, player.Position, weapons.FieldRadius(deflect), .07f, Time.time * 8f, new Color(.42f, .85f, 1f, .3f + .06f * Mathf.Sin(Time.time * 3f)));
                }
                return;
            }
            if (fieldDisc) fieldDisc.enabled = false;
            if (!Line(ref fieldRing, "DeflectField", deflect != null, true)) return;
            Circle(fieldRing, player.Position, weapons.FieldRadius(deflect), .06f);
            fieldRing.widthMultiplier = lineWidth;
            Paint(fieldRing, fieldColor, .85f + .15f * Mathf.Sin(Time.time * 4f));
        }

        void DrawShield(RunWeapon drain, Combatant player)
        {
            bool overload = drain != null && !drain.drainShieldUp && drain.drainOverloadLeft > 0f && !drain.drainOverloadPaid;
            if (V5Vfx.On)
            {
                if (shieldRing) shieldRing.enabled = false;
                bool on = drain != null && (drain.drainShieldUp || overload);
                if (on && !shieldDisc) shieldDisc = V5Vfx.I.MakeFlatDisc("EnergyShield", "hex", transform);
                if (shieldDisc)
                {
                    shieldDisc.enabled = on;
                    if (on) V5Vfx.PlaceDisc(shieldDisc, player.Position, player.radius + 1.05f, .1f, -Time.time * 22f, overload ? new Color(1f, .4f, .7f, Mathf.Repeat(Time.time, .2f) > .1f ? .2f : .8f) : new Color(.5f, 1f, .95f, .62f));
                }
                return;
            }
            if (shieldDisc) shieldDisc.enabled = false;
            if (!Line(ref shieldRing, "EnergyShield", drain != null && (drain.drainShieldUp || overload), true)) return;
            Circle(shieldRing, player.Position, player.radius + .45f, .5f);
            shieldRing.widthMultiplier = lineWidth * (overload ? 1.5f : 2.5f);
            Paint(shieldRing, overload ? overloadColor : shieldColor, overload && Mathf.Repeat(Time.time, .2f) > .1f ? .25f : 1f);
        }

        void DrawDrone(RunWeapon drone, Combatant player)
        {
            bool on = drone != null, down = on && weapons.DroneDown(drone);
            if (on)
            {
                Vector3 target = down ? new Vector3(drone.dronePoint.x, .3f, drone.dronePoint.z) : weapons.DroneHover();
                droneShown = droneShownSet ? Vector3.Lerp(droneShown, target, 1f - Mathf.Exp(-12f * Time.deltaTime)) : target;
                droneShownSet = true;
            }
            else droneShownSet = false;
            Sprite sprite = on && Art.Enabled && art && art.weaponSprites != null && (int)WeaponForm.SentryDrone < art.weaponSprites.Length ? art.weaponSprites[(int)WeaponForm.SentryDrone] : null;
            if (ShowSprite(ref droneSprite, "Drone", sprite))
            {
                droneSprite.sprite = sprite;
                Billboard(droneSprite, droneShown, droneWidth, droneWidth * sprite.bounds.size.y / Mathf.Max(.01f, sprite.bounds.size.x));
                droneSprite.color = down ? droneDownColor : Color.white;
            }
            Color body = down ? droneDownColor : droneColor;
            if (Line(ref droneDisc, "DroneDisc", on && !sprite, true)) { Disc(droneDisc, droneShown, .3f); Paint(droneDisc, body, 1f); }
            if (Line(ref droneRim, "DroneRim", on && !sprite, true)) { Circle(droneRim, droneShown, .45f, droneShown.y); droneRim.widthMultiplier = lineWidth; Paint(droneRim, body, 1f); }
            if (Line(ref dropRing, "DroneStepRing", down, true))
            {
                Circle(dropRing, drone.dronePoint, player.radius + weapons.upgrades.droneRadius, .06f);
                dropRing.widthMultiplier = lineWidth;
                Paint(dropRing, droneDownColor, .6f + .4f * Mathf.Sin(Time.time * 6f));
            }
            if (on && !down && V5Vfx.On) V5Vfx.I.DroneGlow(droneShown);
            if (!down || Time.time < smokeAt || !FxPool.Active) return;
            smokeAt = Time.time + smokeInterval;
            if (V5Vfx.On) { V5Vfx.I.CoolingSmoke(new Vector3(drone.dronePoint.x, .5f, drone.dronePoint.z)); return; }
            FxPool.Active.Emit(new Vector3(drone.dronePoint.x, .5f, drone.dronePoint.z), new Color(.75f, .75f, .8f, .55f), .7f, .9f, Vector3.up * .9f);
        }

        void DrawWhirl(RunWeapon drone, Combatant player)
        {
            var body = drone != null ? drone.whirled : null;
            bool on = body && body.IsAlive && body.gameObject.activeInHierarchy;
            if (Line(ref beam, "WhirlBeam", on, false))
            {
                beam.SetPosition(0, droneShownSet ? droneShown : weapons.DroneHover());
                beam.SetPosition(1, body.Position + Vector3.up * .5f);
                beam.widthMultiplier = lineWidth * 1.5f;
                Paint(beam, beamColor, .75f + .25f * Mathf.Sin(Time.time * 30f));
                if (V5Vfx.On)
                {
                    beam.sharedMaterial = V5Vfx.I.TrailMaterial;
                    beam.widthMultiplier = lineWidth * 2.6f;
                    V5Vfx.I.TractorMotes(droneShownSet ? droneShown : weapons.DroneHover(), body.Position + Vector3.up * .5f);
                }
            }
            Vector3 spot = on ? drone.whirlTo : Vector3.zero;
            Sprite sprite = on && Art.Enabled && look && look.illustration ? look.illustration.sprite : null;
            if (ShowSprite(ref ghostSprite, "Afterimage", sprite))
            {
                ghostSprite.sprite = sprite;
                ghostSprite.flipX = look.illustration.flipX;
                Billboard(ghostSprite, spot + Vector3.up * look.height * .5f, look.height * sprite.bounds.size.x / Mathf.Max(.01f, sprite.bounds.size.y), look.height);
                ghostSprite.color = afterimageColor;
            }
            if (Line(ref ghostFill, "AfterimageFill", on && !sprite, true)) { Disc(ghostFill, new Vector3(spot.x, .05f, spot.z), player.radius); Paint(ghostFill, afterimageColor, 1f); }
            if (Line(ref ghostRing, "AfterimageRing", on, true)) { Circle(ghostRing, spot, player.radius + .1f, .06f); ghostRing.widthMultiplier = lineWidth; Paint(ghostRing, afterimageColor, 2f); }
        }

        bool Line(ref LineRenderer line, string name, bool on, bool flat)
        {
            if (!on) { if (line) line.enabled = false; return false; }
            if (!line)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.sharedMaterial = material;
                line.sortingLayerID = sortingLayer;
                line.sortingOrder = sortingOrder;
                line.alignment = flat ? LineAlignment.TransformZ : LineAlignment.View;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.numCapVertices = 0;
                line.loop = flat;
                line.positionCount = flat ? Segments : 2;
            }

            if (flat) line.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            line.enabled = true;
            return true;
        }

        bool ShowSprite(ref SpriteRenderer renderer, string name, Sprite sprite)
        {
            if (!sprite) { if (renderer) renderer.enabled = false; return false; }
            if (!renderer)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                renderer = go.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingLayerID = sortingLayer;
                renderer.sortingOrder = sortingOrder + 1;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            renderer.enabled = true;
            return true;
        }

        static void Circle(LineRenderer line, Vector3 centre, float radius, float height)
        {
            radius = Mathf.Max(.01f, radius);
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                line.SetPosition(i, new Vector3(centre.x + Mathf.Cos(a) * radius, height, centre.z + Mathf.Sin(a) * radius));
            }
        }

        static void Disc(LineRenderer line, Vector3 centre, float radius)
        {
            Circle(line, centre, radius * .5f, centre.y);
            line.widthMultiplier = Mathf.Max(.01f, radius);
        }

        static void Paint(LineRenderer line, Color color, float alpha)
        {
            color.a = Mathf.Clamp01(color.a * alpha);
            line.startColor = line.endColor = color;
        }

        void Billboard(SpriteRenderer sprite, Vector3 at, float width, float height)
        {
            var camera = GameManager.Instance ? GameManager.Instance.cam : null;
            var t = sprite.transform;
            t.position = at - (camera ? camera.transform.forward * .025f : Vector3.zero);
            t.rotation = camera ? camera.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
            var parent = t.parent ? t.parent.lossyScale : Vector3.one;
            var size = sprite.sprite.bounds.size;
            t.localScale = new Vector3(width / Mathf.Max(.001f, size.x * Mathf.Abs(parent.x)), height / Mathf.Max(.001f, size.y * Mathf.Abs(parent.y)), 1f);
        }
    }
}
