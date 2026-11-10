using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The picture of the hand weapon in use: one saved sprite per weapon form (both variants of a form look the same).
    /// With the player's 3D model showing, the picture is held in the model's right hand:
    /// its grip sits on the hand bone, it points along the aim and it turns about that line to face the camera, kept
    /// upright (flipped while the aim points to the left of the screen). The right arm is bent so the hand holds it out in front of the body.
    /// Presentation only: the aim, shots, hits and muzzle flashes stay where gameplay puts them. Without the model (art off, or a model with
    /// no right hand) the picture keeps its saved place on the Hand object, as before.
    /// With the art off each hand weapon is its own look from the whitebox table (2026-11-10, Whitebox.Build: the Pistol a short grey bar,
    /// the rocket launcher an olive tube with a nose cone, and so on), held out along the aim in front of the body with its front end in the
    /// weapon's element colour (ShowFormLook). The retired forms, never handed out, keep the saved long thin box (PlaceWhitebox).
    /// The Pistol (since 2026-11-06) is drawn shorter and held by its grip; its picture comes from the V2 art set when the scene has none.
    /// </summary>
    [DefaultExecutionOrder(1150)] // after V2ModelPresentation (1100) has posed the model and turned its chest toward the aim
    public class V2WeaponPresentation : MonoBehaviour
    {
        public RunWeapons weapons;
        public SpriteRenderer illustration;
        public Renderer whitebox;
        [Tooltip("WeaponForm enum order; one image for both variants.")]
        public Sprite[] formSprites = new Sprite[17];
        public float length = 2.2f;
        [Header("Held in the model's right hand (2026-11-06)")]
        [Tooltip("Hold the picture in the right hand of the player's 3D model. Off, or without a model: the picture stays on the Hand object as before.")]
        public bool holdInHand = true;
        [Tooltip("Where the hand holds the picture, as a share of its width and height from its bottom left corner (the pictures point right).")]
        public Vector2 grip = new Vector2(.28f, .4f);
        [Tooltip("Per-form grip in WeaponForm order, over the built-in one; a missing or (0, 0) entry uses the built-in grip: the knife and the " +
                 "phase dagger by the handle, every gun by grip, which falls on its trigger grip.")]
        public Vector2[] formGrips = new Vector2[0];
        // Built in, not serialized, so a scene saved before they existed still holds the blades by the handle (checked on every picture).
        static readonly Vector2[] HandleGrips = { default, new Vector2(.11f, .5f), default, default, default, new Vector2(.15f, .5f) };
        [Range(0f, 1f), Tooltip("How far the right arm is bent to hold the weapon out in front: 0 leaves the arm to its animation (the weapon then " +
                                "follows the swinging hand).")]
        public float armHold = 1f;
        [Tooltip("Where the right hand is held, in arm lengths from the shoulder: x forward along the aim, y down, z out to the right.")]
        public Vector3 holdReach = new Vector3(.62f, .5f, .12f);
        [Min(.01f), Tooltip("Seconds the arm takes to come up into the hold, or to drop back to its animation (knockback, falls, the slide).")]
        public float holdBlendSeconds = .15f;
        [Range(-30f, 30f), Tooltip("Tilt of the weapon's front end below the horizontal, in degrees.")]
        public float pitchDown = 3f;

        int lastForm = -1;
        bool lastV5;
        const float V5HandShare = .62f;
        // The Pistol (since 2026-11-06) fills its icon like every other weapon, so in the hand it is drawn shorter: this share of their length.
        const float PistolLengthShare = .8f;
        // Its hand sits on the top of its grip, measured on weapon-pistol.png (the grip runs from x .16 to .37, below y .34).
        static readonly Vector2 PistolGrip = new Vector2(.28f, .32f);
        V2ArtSet artSet;
        bool artSearched;

        V2ModelPresentation model;
        Combatant body;
        Animator boneSource;
        AnimatedBone upperArm, foreArm;
        Transform hand;
        Vector3 savedPosition;
        Quaternion savedRotation;
        bool saved, holding;
        float hold;
        // The whitebox box: its saved place on the Hand object, and its height above the body's feet as saved (about chest height).
        const float WhiteboxInset = .1f; // how far its back end sits inside the front of the body box, as in the saved scene
        Vector3 savedBoxPosition;
        Quaternion savedBoxRotation;
        float boxHeight;
        bool boxSaved, boxPlaced;
        // The whitebox looks of the forms used so far (built on first use and kept), the one shown, and the element painted on it.
        const float DefaultHoldHeight = 1.05f; // without the saved box: about chest height, where the saved box was
        readonly System.Collections.Generic.Dictionary<WeaponForm, WhiteboxLook> formLooks = new();
        WhiteboxLook shownLook;
        ElementKind paintedElement;

        void LateUpdate()
        {
            var active = weapons ? weapons.Active : null;
            int form = active != null ? (int)active.form : -1;
            Sprite picture = form >= 0 ? SpriteOf(form) : null;
            bool valid = picture;
            bool daggerAway = active != null && active.form == WeaponForm.PhaseDagger && active.variant == 1
                && (active.inFlight || active.recallPlanted);
            if (!body) body = GetComponent<Combatant>();
            bool inModel = HeldByModel();
            bool dead = inModel && body && body.State == CharacterState.Dead; // a corpse drops its weapon: the picture goes
            if (illustration) illustration.enabled = valid && !daggerAway && Art.Enabled && !dead;
            // Whitebox: the weapon shows while a hand weapon is out, and a corpse drops it as well. A form with a look in the table shows
            // that look; a retired one the saved box.
            bool boxOut = form >= 0 && !daggerAway && !Art.Enabled && !(body && body.State == CharacterState.Dead);
            bool ownLook = boxOut && active.form is not (WeaponForm.OrbitBlades or WeaponForm.SentryDrone or WeaponForm.ShieldRing)
                && LookIdFor(active.form) != null;
            if (whitebox)
            {
                whitebox.enabled = boxOut && !ownLook;
                PlaceWhitebox(boxOut && !ownLook);
            }
            ShowFormLook(ownLook ? active : null);
            bool v5 = V5Vfx.On;
            if (valid && illustration && (form != lastForm || v5 != lastV5))
            {
                lastForm = form; lastV5 = v5; illustration.sprite = picture;
                // The V5 art keeps the weapon smaller than the body it is held by.
                if (illustration.sprite)
                    illustration.transform.localScale = Vector3.one * ((v5 ? length * V5HandShare : length) * LengthShare(form) / Mathf.Max(.01f, illustration.sprite.bounds.size.x));
            }
            if (illustration) Place(inModel && valid && !daggerAway && !dead, form);
        }

        /// <summary>Whether the player's 3D model is showing and has a right hand to hold the weapon.</summary>
        bool HeldByModel()
        {
            if (!holdInHand || !illustration) return false;
            if (!model)
            {
                model = GetComponent<V2ModelPresentation>();
                body = GetComponent<Combatant>();
            }
            if (!model || !model.UsingModel || !model.animator || !body) return false;
            if (boneSource != model.animator) FindArm(model.animator);
            return hand;
        }

        void FindArm(Animator animator)
        {
            boneSource = animator;
            Transform upper = null, fore = null;
            hand = null;
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            {
                string boneName = bone.name;
                int colon = boneName.LastIndexOf(':'); // "mixamorig:RightHand"
                if (colon >= 0) boneName = boneName.Substring(colon + 1);
                if (!upper && Named(boneName, "RightArm")) upper = bone;
                else if (!fore && Named(boneName, "RightForeArm")) fore = bone;
                else if (!hand && Named(boneName, "RightHand")) hand = bone;
            }
            // Only a plain shoulder - elbow - wrist chain is bent; any other rig still holds the picture in its hand.
            bool chain = hand && fore && upper && hand.parent == fore && fore.parent == upper;
            upperArm = new AnimatedBone(chain ? upper : null);
            foreArm = new AnimatedBone(chain ? fore : null);
            if (!hand) Debug.LogWarning($"{name}: the player model has no RightHand bone; the weapon picture stays on the Hand object.", this);
        }

        static bool Named(string name, string wanted) => string.Equals(name, wanted, System.StringComparison.OrdinalIgnoreCase);

        void Place(bool inHand, int form)
        {
            var picture = illustration.transform;
            if (!saved) { savedPosition = picture.localPosition; savedRotation = picture.localRotation; saved = true; }
            if (!inHand)
            {
                if (holding)
                {
                    holding = false;
                    picture.localPosition = savedPosition; picture.localRotation = savedRotation; illustration.flipY = false;
                }
                hold = 0f;
                return;
            }
            holding = true;
            Vector3 aim = Query.Flat(body.AimDirection);
            if (aim.sqrMagnitude < 1e-6f) aim = Query.Flat(transform.forward);
            if (aim.sqrMagnitude < 1e-6f) aim = Vector3.forward;
            aim.Normalize();

            // The arm comes up while the body is free to act and drops back to its animation for knockback, the slide and falls.
            float want = body.State == CharacterState.Normal ? armHold : 0f;
            hold = Mathf.MoveTowards(hold, want, Time.deltaTime / holdBlendSeconds);
            if (upperArm.bone)
            {
                upperArm.Begin(); foreArm.Begin();
                if (hold > 0f) Reach(aim, hold);
                upperArm.End(); foreArm.End();
            }

            float pitch = pitchDown * Mathf.Deg2Rad;
            Vector3 along = aim * Mathf.Cos(pitch) - Vector3.up * Mathf.Sin(pitch);
            var cam = GameManager.Instance && GameManager.Instance.cam ? GameManager.Instance.cam : Camera.main;
            Vector3 view = cam ? cam.transform.forward : Vector3.down;
            // Turned about its own length to face the camera as fully as it can (the picture is flat), its top kept toward the top of the screen.
            Vector3 facing = view - Vector3.Dot(view, along) * along;
            if (facing.sqrMagnitude < 1e-6f) facing = Vector3.down - Vector3.Dot(Vector3.down, along) * along;
            facing.Normalize();
            Vector3 top = Vector3.Cross(facing, along);
            bool flip = cam && Vector3.Dot(top, cam.transform.up) < 0f;
            illustration.flipY = flip;
            var rotation = Quaternion.LookRotation(facing, top);
            Vector3 gripPoint = Vector3.zero;
            if (illustration.sprite)
            {
                var bounds = illustration.sprite.bounds;
                Vector2 share = GripOf(form);
                gripPoint = new Vector3(bounds.min.x + share.x * bounds.size.x, bounds.min.y + share.y * bounds.size.y, 0f);
                if (flip) gripPoint.y = -gripPoint.y; // flipY mirrors the picture about its pivot
            }
            picture.SetPositionAndRotation(hand.position - rotation * (gripPoint * picture.lossyScale.x), rotation);
        }

        /// <summary>
        /// The whitebox weapon (whitebox weapons are boxes too): the saved long thin box, its length along the aim, from
        /// just inside the front of the body box outward, centred on the body as the shots are (RunWeapons.Muzzle), at the height it was
        /// saved at. It follows the aim every frame, also while the body itself cannot turn (the slide, a hook). Hidden, it goes back to its
        /// saved place on the Hand object.
        /// </summary>
        void PlaceWhitebox(bool shown)
        {
            var box = whitebox.transform;
            SaveBox();
            if (!shown || !body)
            {
                if (boxPlaced) { boxPlaced = false; box.localPosition = savedBoxPosition; box.localRotation = savedBoxRotation; }
                return;
            }
            boxPlaced = true;
            Vector3 aim = Query.Flat(body.AimDirection);
            if (aim.sqrMagnitude < 1e-6f) aim = Query.Flat(transform.forward);
            if (aim.sqrMagnitude < 1e-6f) aim = Vector3.forward;
            aim.Normalize();
            float reach = body.radius + box.lossyScale.z * .5f - WhiteboxInset;
            box.SetPositionAndRotation(transform.position + Vector3.up * boxHeight + aim * reach, Quaternion.LookRotation(aim, Vector3.up));
        }

        void SaveBox()
        {
            if (boxSaved || !whitebox) return;
            var box = whitebox.transform;
            boxSaved = true;
            savedBoxPosition = box.localPosition; savedBoxRotation = box.localRotation;
            boxHeight = transform.InverseTransformPoint(box.position).y;
        }

        /// <summary>The whitebox look of a form (the table's id), or null for a retired form, which keeps the saved box. The auto weapons'
        /// looks are drawn by V2OrbitPresentation and V2AutoWeaponMarks.</summary>
        public static string LookIdFor(WeaponForm form) => form switch
        {
            WeaponForm.Pistol => "pistol",
            WeaponForm.Rifle => "rifle",
            WeaponForm.Knife => "knife",
            WeaponForm.RocketLauncher => "rocket",
            WeaponForm.ScatterCannon => "scatter",
            WeaponForm.Flamethrower => "flamethrower",
            WeaponForm.CryoCannon => "cryo",
            WeaponForm.ArcCoil => "arc",
            WeaponForm.SporeLauncher => "spore",
            WeaponForm.Railgun => "railgun",
            WeaponForm.Minigun => "minigun",
            WeaponForm.SwarmLauncher => "swarm",
            WeaponForm.OrbitBlades => "orbit.blade",
            WeaponForm.SentryDrone => "sentry",
            WeaponForm.ShieldRing => "ring.box",
            _ => null
        };

        /// <summary>
        /// The whitebox weapon (2026-11-10): the form's look from the table, its grip end at the front of the body and pointing along the
        /// aim, at the height the saved box was held, following the aim every frame (also while the body cannot turn: the slide, a hook).
        /// The looks are built on first use, one per form, and kept.
        /// </summary>
        void ShowFormLook(RunWeapon weapon)
        {
            var form = weapon != null ? weapon.form : default;
            if (shownLook && (weapon == null || shownLook.LookId != LookIdFor(form))) { shownLook.gameObject.SetActive(false); shownLook = null; }
            if (weapon == null || !body) return;
            if (!shownLook)
            {
                if (!formLooks.TryGetValue(form, out var look) || !look)
                    formLooks[form] = look = Whitebox.Build(LookIdFor(form), transform, "Whitebox " + form).GetComponent<WhiteboxLook>();
                look.gameObject.SetActive(true);
                shownLook = look;
                paintedElement = (ElementKind)(-1);
            }
            if (paintedElement != weapon.element)
            {
                paintedElement = weapon.element;
                PaintElement(shownLook, weapon.element);
            }
            SaveBox();
            Vector3 aim = Query.Flat(body.AimDirection);
            if (aim.sqrMagnitude < 1e-6f) aim = Query.Flat(transform.forward);
            if (aim.sqrMagnitude < 1e-6f) aim = Vector3.forward;
            aim.Normalize();
            float height = boxSaved ? boxHeight : DefaultHoldHeight;
            Whitebox.PlaceWorld(shownLook.transform, transform.position + Vector3.up * height + aim * (body.radius - WhiteboxInset),
                Quaternion.LookRotation(aim, Vector3.up));
        }

        /// <summary>The front end of a weapon look (the parts reaching furthest forward, within a few centimetres) takes the element's
        /// colour (ElementCombat.ColorFor, the colour of its shots); no element gives every part its table colour back.</summary>
        public static void PaintElement(WhiteboxLook look, ElementKind element)
        {
            if (!look) return;
            look.ResetColours();
            if (element == ElementKind.None) return;
            float front = float.NegativeInfinity;
            for (int i = 0; i < look.PartCount; i++) front = Mathf.Max(front, look.Part(i).Bounds.max.z);
            var colour = ElementCombat.ColorFor(element);
            for (int i = 0; i < look.PartCount; i++)
                if (look.Part(i).Bounds.max.z >= front - FrontEndReach) look.SetColour(i, colour);
        }

        const float FrontEndReach = .06f; // parts ending this close to the very front count as the front end

        Vector2 GripOf(int form)
        {
            if (formGrips != null && form >= 0 && form < formGrips.Length && formGrips[form] != Vector2.zero) return formGrips[form];
            if (form >= 0 && form < HandleGrips.Length && HandleGrips[form] != Vector2.zero) return HandleGrips[form];
            if (form == (int)WeaponForm.Pistol) return PistolGrip;
            return grip;
        }

        /// <summary>
        /// The picture of a form: its saved formSprites entry, or else the V2 art set's weapon icon (the same pictures), which is how a form
        /// added after the scene was saved gets one (the Pistol, since 2026-11-06). The art set is the one the body's V2CharacterIllustration
        /// uses, or the HUD's.
        /// </summary>
        Sprite SpriteOf(int form)
        {
            if (formSprites != null && form < formSprites.Length && formSprites[form]) return formSprites[form];
            if (!artSearched)
            {
                artSearched = true;
                foreach (var look in GetComponents<V2CharacterIllustration>()) if (look.art) { artSet = look.art; break; }
                if (!artSet)
                {
                    var hud = FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
                    if (hud) artSet = hud.art;
                }
            }
            return artSet && artSet.weaponSprites != null && form < artSet.weaponSprites.Length ? artSet.weaponSprites[form] : null;
        }

        static float LengthShare(int form) => form == (int)WeaponForm.Pistol ? PistolLengthShare : 1f;

        /// <summary>
        /// Bends the right arm (two bones, the elbow down and out) so the hand sits in front of the body, held out along the aim. Analytic, so
        /// it lands in the same place every frame whatever the animation did to the arm; weight below 1 eases between the animation and the hold.
        /// </summary>
        void Reach(Vector3 aim, float weight)
        {
            Transform upper = upperArm.bone, fore = foreArm.bone;
            Vector3 shoulder = upper.position, elbow = fore.position, wrist = hand.position;
            float first = Vector3.Distance(shoulder, elbow), second = Vector3.Distance(elbow, wrist), reach = first + second;
            if (first < 1e-4f || second < 1e-4f) return;
            Vector3 right = Vector3.Cross(Vector3.up, aim);
            Vector3 target = shoulder + (aim * holdReach.x - Vector3.up * holdReach.y + right * holdReach.z) * reach;
            Vector3 toTarget = target - shoulder;
            if (toTarget.sqrMagnitude < 1e-8f) return;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(first - second) + 1e-3f, reach * .999f);
            Vector3 line = toTarget.normalized;
            Vector3 pole = -Vector3.up + right * .6f - aim * .3f;
            Vector3 bend = pole - Vector3.Dot(pole, line) * line;
            if (bend.sqrMagnitude < 1e-8f) return;
            bend.Normalize();
            float cos = Mathf.Clamp((first * first + distance * distance - second * second) / (2f * first * distance), -1f, 1f);
            Vector3 elbowTo = shoulder + first * (cos * line + Mathf.Sqrt(1f - cos * cos) * bend);
            Vector3 wristTo = shoulder + distance * line;

            Quaternion upperFrom = upper.rotation, foreFrom = fore.rotation;
            upper.rotation = Quaternion.FromToRotation(elbow - shoulder, elbowTo - shoulder) * upperFrom;
            Quaternion upperHeld = upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, wristTo - fore.position) * fore.rotation;
            if (weight >= 1f) return;
            Quaternion foreHeld = fore.rotation;
            upper.rotation = Quaternion.Slerp(upperFrom, upperHeld, weight);
            fore.rotation = Quaternion.Slerp(foreFrom, foreHeld, weight);
        }
    }
}
