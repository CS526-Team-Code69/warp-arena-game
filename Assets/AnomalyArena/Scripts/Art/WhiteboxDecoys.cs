using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The decoy skill's stand-in (and the void trial's) in whitebox (2026-11-10): the saved capsule is hidden and the whitebox table's
    /// "decoy" stands in its place, the player's shape in see-through white, facing where the player aims (as the illustrated art's
    /// mirror image does, V5Vfx). WhiteboxCharacter adds it to the player; the looks hang from a scene object of their own, so they do
    /// not move with the player. The decoys themselves (WorldMap) are only read.
    /// </summary>
    public sealed class WhiteboxDecoys : MonoBehaviour
    {
        Transform holder;
        Stand decoy, trialDecoy;

        sealed class Stand
        {
            public Transform source;
            public Renderer[] saved;
            public bool hidden;
            public Transform look;
        }

        void OnDestroy()
        {
            if (holder) Destroy(holder.gameObject);
        }

        void LateUpdate()
        {
            var map = WorldMap.Active;
            Tick(ref decoy, map ? map.decoy : null);
            Tick(ref trialDecoy, map ? map.trialDecoy : null);
        }

        void Tick(ref Stand stand, Transform source)
        {
            if (!source)
            {
                if (stand?.look) stand.look.gameObject.SetActive(false);
                return;
            }
            if (stand == null || stand.source != source)
            {
                stand = new Stand { source = source, saved = source.GetComponentsInChildren<MeshRenderer>(true), hidden = false };
            }
            bool whitebox = !Art.Enabled;
            if (stand.hidden != whitebox)
            {
                // forceRenderingOff, not enabled: the illustrated art's mirror switches enabled off and on.
                stand.hidden = whitebox;
                foreach (var r in stand.saved) if (r) r.forceRenderingOff = whitebox;
            }
            bool on = whitebox && source.gameObject.activeInHierarchy;
            if (!on)
            {
                if (stand.look && stand.look.gameObject.activeSelf) stand.look.gameObject.SetActive(false);
                return;
            }
            if (!holder) holder = new GameObject("Whitebox decoys").transform;
            if (!stand.look) stand.look = Whitebox.Build("decoy", holder, "Whitebox " + source.name);
            if (!stand.look.gameObject.activeSelf) stand.look.gameObject.SetActive(true);
            var gm = GameManager.Instance;
            var player = gm ? gm.player : null;
            float ground = player ? player.transform.position.y : 0f;
            Vector3 facing = player ? Query.Flat(player.AimDirection) : Vector3.forward;
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.forward;
            stand.look.SetPositionAndRotation(new Vector3(source.position.x, ground, source.position.z), Quaternion.LookRotation(facing.normalized, Vector3.up));
        }
    }
}
