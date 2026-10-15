using UnityEngine;

namespace AnomalyArena
{
    public abstract class WeaponEffect : ScriptableObject
    {
        [Header("Identity")] public EffectId id;
        public WeaponType type;
        public string displayName;
        [TextArea] public string description;

        [Tooltip("Minimum interval between two uses")]
        public float cooldown = 0.3f;

        [Tooltip("While the left button is held, keeps using it automatically at the cooldown interval")]
        public bool holdToRepeat;

        [Tooltip("Shots per use (clip size); 1 = every shot spends a use")] [Min(1)]
        public int roundsPerUse = 1;

        public abstract void Use(Weapon weapon, IWeaponHolder user);
    }


    public abstract class WeaponRuntime : MonoBehaviour
    {
        protected Weapon weapon;
        protected IWeaponHolder user;
        bool finished;

        public virtual bool LocksMovement => false;
        public virtual float SpeedMultiplier => 1f;
        public virtual bool CanSwap => false;

        protected void Bind(Weapon w, IWeaponHolder u)
        {
            weapon = w;
            user = u;
            if (w != null) w.Active = this;
            GameManager.Instance.Register(this);
        }


        public virtual void OnUsePressed()
        {
        }


        public virtual void OnSwapAway()
        {
        }


        public virtual void OnUseReleased()
        {
        }


        public virtual void Cancel() => Finish();

        protected void Finish()
        {
            if (finished) return;
            finished = true;
            if (weapon != null && weapon.Active == this) weapon.Active = null;
            GameManager.Instance.Unregister(this);
            Destroy(gameObject);
            if (user != null) user.OnWeaponRuntimeFinished();
        }
    }


    public abstract class Projectile : MonoBehaviour, IDamageDealer
    {
        public IWeaponHolder Owner { get; protected set; }

        protected virtual void Awake() => GameManager.Instance.Register(this);

        protected virtual void OnDestroy()
        {
            if (GameManager.Instance) GameManager.Instance.Unregister(this);
        }

        protected static Transform MakeVisual(Transform parent, PrimitiveType t, Vector3 scale, Color c,
            Vector3 localPos = default)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.transform.localPosition = localPos;
            go.GetComponent<Renderer>().sharedMaterial = GameManager.Instance.Mat(c);
            return go.transform;
        }


        protected TrailRenderer AddTrail(Color c, float width)
        {
            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.35f;
            tr.startWidth = width;
            tr.endWidth = 0f;
            tr.sharedMaterial = GameManager.Instance.FxMat(c);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tr;
        }
    }
}