using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// How a boss's change of phase looks (Enemy.PhaseShift.cs), added to a boss at runtime. While it gathers, motes and streaks are drawn
    /// in to it, the ground under it glows brighter and light swells at its chest; the burst throws out a flash, rings to the reach that
    /// pushes the player back, dust, rock, sparks and cracks; a hit turned away during the change flashes a hex shell around it. In a later
    /// phase it gives off a few embers, more in its third. The Tyrant's colour is a burning red, the Prophet's the void's violet. Without the
    /// art the change shows as the whitebox effects' rings closing in on it, a ring and a flash at the burst, and a pale flash for a hit
    /// turned away.
    /// </summary>
    public sealed class V5BossPhaseFx : MonoBehaviour
    {
        static readonly Color TyrantRage = new(1f, .36f, .14f), ProphetVoid = new(.72f, .42f, 1f);

        Enemy enemy;
        int burstSeen, guardSeen;
        bool primed;
        float guardShownAt = -1f, ringAt;

        void LateUpdate()
        {
            if (!enemy) enemy = GetComponent<Enemy>();
            if (!enemy || !enemy.definition) return;
            if (!primed)
            {
                primed = true;
                burstSeen = enemy.PhaseBurstSequence;
                guardSeen = enemy.PhaseGuardSequence;
            }

            bool burst = enemy.PhaseBurstSequence != burstSeen, guard = enemy.PhaseGuardSequence != guardSeen;
            burstSeen = enemy.PhaseBurstSequence;
            guardSeen = enemy.PhaseGuardSequence;
            // Nothing new is drawn while the world stands still (the pause, a card pick) or once the boss is gone.
            if (!enemy.IsAlive || Time.timeScale <= 0f) return;

            var game = GameManager.Instance;
            var tuning = game && game.waves && game.waves.bosses != null ? game.waves.bosses : BossTuning.Defaults;
            Color tint = enemy.definition.kind == EnemyKind.Tyrant ? TyrantRage : ProphetVoid;
            Vector3 feet = Query.Flat(enemy.Position);
            float body = Mathf.Max(.8f, enemy.radius);
            var cam = game ? game.cam : null;
            Vector3 top = cam && enemy.TryModelTop(cam, out var modelTop) ? modelTop : feet + Vector3.up * (body * 2f + .6f);
            float height = Mathf.Max(1.2f, top.y - feet.y);
            Vector3 chest = feet + Vector3.up * (height * .6f);
            float reach = Mathf.Max(body + 1f, tuning.shiftPushRadius);
            bool art = V5Vfx.On;

            if (burst)
            {
                if (art) V5Vfx.I.PhaseBurst(feet, chest, body, reach, tint);
                else
                {
                    Fx.Ring(feet, reach, tint, .45f);
                    Fx.Pop(chest, tint, reach * .7f);
                }
            }

            if (enemy.PhaseShiftShown)
            {
                if (!enemy.PhaseShiftBurstDone)
                {
                    float t = Mathf.Clamp01(enemy.PhaseShift01 / Mathf.Max(.01f, tuning.shiftBurstShare));
                    if (art) V5Vfx.I.PhaseGather(feet, chest, body, t, tint);
                    else if (Time.time >= ringAt)
                    {
                        // Rings closing in on it, more often as the burst nears.
                        ringAt = Time.time + Mathf.Lerp(.4f, .18f, t);
                        Fx.Ring(feet, Mathf.Lerp(reach, body + .5f, t), tint, .2f);
                    }
                }
                else if (art) V5Vfx.I.PhaseAura(feet, body, height, tint, 3f);
            }
            else if (art && enemy.BossPhase >= 2) V5Vfx.I.PhaseAura(feet, body, height, tint, enemy.BossPhase >= 3 ? 1.8f : 1f);

            if (guard && Time.time - guardShownAt >= .07f)
            {
                guardShownAt = Time.time;
                if (art) V5Vfx.I.PhaseGuard(chest, body, height, tint);
                else Fx.Pop(chest, Color.Lerp(tint, Color.white, .6f), body * 2.2f);
            }
        }
    }
}
