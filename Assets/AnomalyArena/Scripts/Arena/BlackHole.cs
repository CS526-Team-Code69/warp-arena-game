using UnityEngine;

namespace AnomalyArena
{
    public enum HolePhase { Active, Collapsing, Portal, Gone }

    /// <summary>Saved radial kill core and gravity footprint, with a separate feed count for each hole.</summary>
    public sealed class BlackHole : MonoBehaviour
    {
        [Tooltip("圆形黑核的直径，单位：世界格。")] public float size = 7f;
        [Tooltip("Gravity border beyond each side, in cells.")] public float gravityCells = 2f;
        public float pullSpeed = 3f;
        public int feedsToOpen = 40;
        public Transform killVisual;
        public Transform gravityVisual;
        public Transform portalVisual;
        public int Feeds { get; private set; }
        public HolePhase Phase { get; private set; } = HolePhase.Active;
        public int OpenedWave { get; private set; }
        public int WavesLeft { get; private set; } = 3;
        float collapseTimer;
        float temporaryLife;
        public bool Temporary { get; private set; }
        Combatant protectedOwner;
        /// <summary>Who opened this temporary hole (a Pocket Void's caster, or the Nash Prophet), null for a map hole or a trial portal
        /// (WorldMap.SkillObjectStanding: no second Pocket Void while its hole is open).</summary>
        public Combatant Owner { get; private set; }
        public float TemporarySecondsLeft => temporaryLife;
        /// <summary>Whether what this hole swallows counts toward its gate (the classic run). The V2 campaign's hole opens by its timed
        /// damage check instead (since 2026-11-10), so the campaign turns this off: it still swallows, but counts nothing.</summary>
        [System.NonSerialized] public bool countsFeeds = true;
        /// <summary>The V2 campaign's text over its open hole (V2WorldLabels): the check's damage share and time while it runs, empty
        /// otherwise. Null keeps the classic count.</summary>
        [System.NonSerialized] public string campaignLabel;
        public Vector3 Centre => transform.position;
        public float KillRadiusNominal=>size*.5f;
        public float KillRadius=>KillRadiusNominal*(Phase==HolePhase.Collapsing?Mathf.Clamp01(collapseTimer/1.5f):1f);
        /// <summary>A map hole's pull ring (the open mode's two, the story's one) is this many times gravityCells wide, so a step into it
        /// leaves room to turn back. A hole made for a moment (a skill's, the Nash Prophet's) keeps its own width.</summary>
        public const float MapRingScale = 2.5f;
        /// <summary>A map hole pulls this many times as fast as the player walks: walking out of its ring loses ground slowly, a sprint
        /// gets out, and every monster, which walks slower than the player, is taken.</summary>
        public const float PullOverWalk = 1.1f;
        public float GravityRadius=>KillRadiusNominal+gravityCells*(Temporary?1f:MapRingScale);
        /// <summary>A map hole's pull for a player who walks at walkSpeed cells a second.</summary>
        public static float MapPull(float walkSpeed) => Mathf.Max(.1f, PullOverWalk * walkSpeed);

        public void ResetHole()
        {
            protectedOwner = Owner = null;
            Feeds = 0;
            Phase = HolePhase.Active;
            collapseTimer = 0f;
            RefreshLook();
        }

        public void MakeTemporary(Vector3 at, float width, float life, Combatant owner = null, bool protectOwner = false, float gravityWidth = 1f)
        {
            transform.position = at;
            size = width;
            gravityCells = gravityWidth;
            protectedOwner = protectOwner ? owner : null;
            Owner = owner;
            Temporary = true;
            temporaryLife = life;
            Phase = HolePhase.Active;
            gameObject.SetActive(true);
            RefreshLook();
        }

        public bool Contains(Vector3 at, Combatant actor = null)
        {
            if (actor && actor == protectedOwner) return false;
            if (Phase != HolePhase.Active && Phase != HolePhase.Collapsing) return false;
            return Query.Flat(at-Centre).sqrMagnitude<KillRadius*KillRadius;
        }

        public bool InGravity(Vector3 at) => Phase == HolePhase.Active && Query.Flat(at-Centre).sqrMagnitude<GravityRadius*GravityRadius;
        public Vector3 Pull(Vector3 at, Combatant actor = null) => actor && actor == protectedOwner ? Vector3.zero : InGravity(at) ? Query.Flat(Centre - at).normalized * pullSpeed : Vector3.zero;

        public void Feed(int count, int wave)
        {
            if (!countsFeeds || Temporary || Phase != HolePhase.Active || WorldMap.Active == null || !WorldMap.Active.CanFeed) return;
            Feeds = Mathf.Min(feedsToOpen, Feeds + count);
            if (Feeds < feedsToOpen) return;
            Collapse(wave);
        }

        /// <summary>The V2 campaign's hole passed its timed damage check: it collapses into the hidden gate as a fed-full hole does.</summary>
        public void OpenGate(int wave)
        {
            if (Temporary || Phase != HolePhase.Active) return;
            Collapse(wave);
        }

        void Collapse(int wave)
        {
            Phase = HolePhase.Collapsing;
            collapseTimer = 1.5f;
            OpenedWave = wave;
            WavesLeft = 3;
            RefreshLook();
            if (WorldMap.Active) WorldMap.Active.HoleChanged();
        }

        public void Tick(float dt)
        {
            if (Temporary)
            {
                temporaryLife -= dt;
                if (temporaryLife <= 0f) { Phase = HolePhase.Gone; gameObject.SetActive(false); WorldMap.Active.HoleChanged(); }
            }
            if (Phase != HolePhase.Collapsing) return;
            collapseTimer -= dt;
            if (killVisual) killVisual.localScale = new Vector3(size, 0.05f, size) * Mathf.Max(0.01f, collapseTimer / 1.5f);
            if (collapseTimer > 0f) return;
            Phase = HolePhase.Portal;
            RefreshLook();
            WorldMap.Active.HoleChanged();
        }

        public void WaveEnded(int wave)
        {
            if (Phase != HolePhase.Portal) return;
            WavesLeft = Mathf.Max(0, 3 - (wave - OpenedWave));
            if (WavesLeft == 0) Close();
        }

        public void Close()
        {
            Phase = HolePhase.Gone;
            RefreshLook();
            if (WorldMap.Active) WorldMap.Active.HoleChanged();
        }

        /// <summary>A continued save: this map hole where it stood, with its feeds and its phase as they were saved (a hole saved while it
        /// collapsed comes back as the portal it was turning into).</summary>
        public void Restore(Vector3 at, int feeds, HolePhase phase, int openedWave, int wavesLeft)
        {
            transform.position = new Vector3(at.x, 0f, at.z);
            Feeds = Mathf.Clamp(feeds, 0, Mathf.Max(0, feedsToOpen));
            Phase = phase == HolePhase.Collapsing ? HolePhase.Portal : phase;
            OpenedWave = openedWave;
            WavesLeft = Mathf.Clamp(wavesLeft, 0, 3);
            collapseTimer = 0f;
            RefreshLook();
            if (WorldMap.Active) WorldMap.Active.HoleChanged();
        }

        /// <summary>A portal of the void trial (WorldMap.Trial.cs): the open-portal look at this point. It never swallows, pulls or counts and
        /// never runs out; WorldMap hides it when the trial ends.</summary>
        public void ShowAsTrialPortal(Vector3 at)
        {
            transform.position = at;
            protectedOwner = Owner = null;
            Temporary = true;
            temporaryLife = float.PositiveInfinity;
            Phase = HolePhase.Portal;
            gameObject.SetActive(true);
            RefreshLook();
        }

        void RefreshLook()
        {
            if (killVisual) { killVisual.gameObject.SetActive(Phase == HolePhase.Active || Phase == HolePhase.Collapsing); killVisual.localScale = new Vector3(size, 0.05f, size); }
            if (gravityVisual) { gravityVisual.gameObject.SetActive(Phase == HolePhase.Active); gravityVisual.localScale = new Vector3(GravityRadius * 2f, 0.025f, GravityRadius * 2f); }
            if (portalVisual) portalVisual.gameObject.SetActive(Phase == HolePhase.Portal);
        }
    }
}
