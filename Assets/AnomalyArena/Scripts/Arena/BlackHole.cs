using UnityEngine;

namespace AnomalyArena
{
    public enum HolePhase { Active, Collapsing, Portal, Gone }

    public sealed class BlackHole : MonoBehaviour
    {
        [Tooltip("Diameter of the circular black core in world grid units.")] public float size = 7f;
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
        public float TemporarySecondsLeft => temporaryLife;
        public Vector3 Centre => transform.position;
        public float KillRadiusNominal=>size*.5f;
        public float KillRadius=>KillRadiusNominal*(Phase==HolePhase.Collapsing?Mathf.Clamp01(collapseTimer/1.5f):1f);
        public float GravityRadius=>KillRadiusNominal+gravityCells;

        public void ResetHole()
        {
            protectedOwner = null;
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
            if (Temporary || Phase != HolePhase.Active || WorldMap.Active == null || !WorldMap.Active.CanFeed) return;
            Feeds = Mathf.Min(feedsToOpen, Feeds + count);
            if (Feeds < feedsToOpen) return;
            Phase = HolePhase.Collapsing;
            collapseTimer = 1.5f;
            OpenedWave = wave;
            WavesLeft = 3;
            RefreshLook();
            WorldMap.Active.HoleChanged();
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

        void RefreshLook()
        {
            if (killVisual) { killVisual.gameObject.SetActive(Phase == HolePhase.Active || Phase == HolePhase.Collapsing); killVisual.localScale = new Vector3(size, 0.05f, size); }
            if (gravityVisual) { gravityVisual.gameObject.SetActive(Phase == HolePhase.Active); gravityVisual.localScale = new Vector3(size + gravityCells * 2f, 0.025f, size + gravityCells * 2f); }
            if (portalVisual) portalVisual.gameObject.SetActive(Phase == HolePhase.Portal);
        }
    }
}
