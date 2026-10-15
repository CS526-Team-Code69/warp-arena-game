using UnityEngine;

namespace AnomalyArena
{
    public class ShotCharge : WeaponRuntime
    {
        const float MinLineWidth = 0.12f;
        const float MaxLineWidth = 0.5f;

        GunChargedShotEffect cfg;
        float t;
        Material mat;
        Transform line;

        public float Charge01 => cfg ? Mathf.Clamp01(t / Mathf.Max(0.01f, cfg.maxChargeTime)) : 0f;
        public override float SpeedMultiplier => cfg.chargeMoveMultiplier;

        public void Begin(GunChargedShotEffect e, Weapon w, IWeaponHolder u)
        {
            cfg = e;
            Bind(w, u);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "AimLine";
            Destroy(quad.GetComponent<Collider>());
            line = quad.transform;
            line.SetParent(transform, false);
            var r = quad.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            mat = GameManager.Instance.FxMat(new Color(1f, 0.35f, 0.1f, 0.3f));
            r.sharedMaterial = mat;
            UpdateLine();
        }

        void Update()
        {
            if (user == null || !user.IsAlive || user.State != CharacterState.Normal)
            {
                Finish();
                return;
            }

            t += Time.deltaTime;
            UpdateLine();
        }

        void UpdateLine()
        {
            float k = Charge01;
            Vector3 aim = Query.Flat(user.AimDirection);
            Vector3 from = Query.AtCastHeight(user.Position);
            float length = Query.WallBetween(from, from + aim * cfg.shotRange, out var wall)
                ? wall.distance
                : cfg.shotRange;

            line.position = new Vector3(from.x, 0.04f, from.z) + aim * (length * 0.5f);
            line.rotation = Quaternion.LookRotation(aim) * Quaternion.Euler(90f, 0f, 0f);
            line.localScale = new Vector3(Mathf.Lerp(MinLineWidth, MaxLineWidth, k), length, 1f);

            float a = k >= 1f ? 0.55f + 0.2f * Mathf.Sin(Time.time * 25f) : Mathf.Lerp(0.3f, 0.5f, k);
            mat.color = new Color(1f, 0.35f, 0.1f, a);
        }

        public override void OnUseReleased()
        {
            cfg.Fire(user, Charge01);
            Finish();
        }

        void OnDestroy()
        {
            if (mat) Destroy(mat);
        }
    }
}