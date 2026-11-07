using UnityEngine;

namespace AnomalyArena
{

    [DefaultExecutionOrder(1150)]                                                                                            
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

        V2ModelPresentation model;
        Combatant body;
        Animator boneSource;
        AnimatedBone upperArm, foreArm;
        Transform hand;
        Vector3 savedPosition;
        Quaternion savedRotation;
        bool saved, holding;
        float hold;

        const float WhiteboxInset = .1f;                                                                                     
        Vector3 savedBoxPosition;
        Quaternion savedBoxRotation;
        float boxHeight;
        bool boxSaved, boxPlaced;

        void LateUpdate()
        {
            var active = weapons ? weapons.Active : null;
            int form = active != null ? (int)active.form : -1;
            bool valid = form >= 0 && form < formSprites.Length;
            bool daggerAway = active != null && active.form == WeaponForm.PhaseDagger && active.variant == 1
                && (active.inFlight || active.recallPlanted);
            if (!body) body = GetComponent<Combatant>();
            bool inModel = HeldByModel();
            bool dead = inModel && body && body.State == CharacterState.Dead;                                               
            if (illustration) illustration.enabled = valid && !daggerAway && Art.Enabled && !dead;
            if (whitebox)
            {

                bool boxOut = valid && !daggerAway && !Art.Enabled && !(body && body.State == CharacterState.Dead);
                whitebox.enabled = boxOut;
                PlaceWhitebox(boxOut);
            }
            bool v5 = V5Vfx.On;
            if (valid && illustration && (form != lastForm || v5 != lastV5))
            {
                lastForm = form; lastV5 = v5; illustration.sprite = formSprites[form];

                if (illustration.sprite)
                    illustration.transform.localScale = Vector3.one * ((v5 ? length * V5HandShare : length) / Mathf.Max(.01f, illustration.sprite.bounds.size.x));
            }
            if (illustration) Place(inModel && valid && !daggerAway && !dead, form);
        }

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
                int colon = boneName.LastIndexOf(':');                         
                if (colon >= 0) boneName = boneName.Substring(colon + 1);
                if (!upper && Named(boneName, "RightArm")) upper = bone;
                else if (!fore && Named(boneName, "RightForeArm")) fore = bone;
                else if (!hand && Named(boneName, "RightHand")) hand = bone;
            }

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
                if (flip) gripPoint.y = -gripPoint.y;                                             
            }
            picture.SetPositionAndRotation(hand.position - rotation * (gripPoint * picture.lossyScale.x), rotation);
        }

        void PlaceWhitebox(bool shown)
        {
            var box = whitebox.transform;
            if (!boxSaved)
            {
                boxSaved = true;
                savedBoxPosition = box.localPosition; savedBoxRotation = box.localRotation;
                boxHeight = transform.InverseTransformPoint(box.position).y;
            }
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

        Vector2 GripOf(int form)
        {
            if (formGrips != null && form >= 0 && form < formGrips.Length && formGrips[form] != Vector2.zero) return formGrips[form];
            if (form >= 0 && form < HandleGrips.Length && HandleGrips[form] != Vector2.zero) return HandleGrips[form];
            return grip;
        }

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
