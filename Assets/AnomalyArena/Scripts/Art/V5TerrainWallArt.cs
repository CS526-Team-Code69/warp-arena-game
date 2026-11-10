using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// The stone body of one wall object. It reads the wall's rectangle from the object's own position and scale (its collider is a unit box scaled to that rectangle) and
    /// builds the stones in a child that cancels the scale, so nothing stretches. A copy of a wall (LayoutWalls, TyrantTerrain) is moved and rescaled after it is made
    /// and builds its own stones from its new rectangle. Shown only with the illustrated art on; gameplay never reads it.
    /// To give another wall object the same stone: V5TerrainWallArt.Attach(gameObject, V5WallStyle.Ruins), then Refresh after changing its size. Its shadow on the ground
    /// comes with it, on the maps that have V5TerrainGround.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(450)]
    public sealed class V5TerrainWallArt : MonoBehaviour
    {
        const string ChildName = "V5 Stone";
        /// <summary>Walls standing now; V5TerrainGround draws their shadows.</summary>
        public static readonly List<V5TerrainWallArt> Standing = new();

        public V5WallStyle style;
        // Serialized so a piece cut from a long wall keeps the direction of its courses when it is copied.
        [SerializeField, HideInInspector] int axis = -1;
        Transform stone;
        MeshFilter filter;
        V5WallShape shape;
        Vector3 builtScale, builtAt;
        V5WallStyle builtStyle;
        bool pending = true;

        /// <summary>The built wall, null until the first Refresh with the illustrated art on or while it is off.</summary>
        public V5WallShape Shape => stone && stone.gameObject.activeSelf ? shape : null;
        /// <summary>The child holding the stone mesh.</summary>
        public Transform Stone => stone;

        /// <summary>Adds the stone to a wall object, or returns the one it has (a copy of a wall keeps its original's stone). It is built by the end of the frame.</summary>
        public static V5TerrainWallArt Attach(GameObject wall, V5WallStyle style)
        {
            if (wall.TryGetComponent(out V5TerrainWallArt art)) return art;
            art = wall.AddComponent<V5TerrainWallArt>();
            art.style = style;
            return art;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget() => Standing.Clear();

        void OnEnable()
        {
            if (!Standing.Contains(this)) Standing.Add(this);
            pending = true;
            V5TerrainGround.MarkDirty();
        }

        void OnDisable()
        {
            Standing.Remove(this);
            V5TerrainGround.MarkDirty();
        }

        void OnDestroy()
        {
            if (shape != null && shape.mesh) Destroy(shape.mesh);
        }

        void LateUpdate()
        {
            if (!pending && !transform.hasChanged) return;
            Refresh();
        }

        /// <summary>Asks for a Refresh by the end of this frame.</summary>
        public void Invalidate() => pending = true;

        /// <summary>Builds the stone if the wall's rectangle changed since last time, and shows it or hides it with the illustrated art.</summary>
        public void Refresh()
        {
            pending = false;
            transform.hasChanged = false;
            if (!stone)
            {
                // A copy of a wall comes with its original's child; its mesh is replaced below.
                stone = transform.Find(ChildName);
                if (stone) filter = stone.GetComponent<MeshFilter>();
            }

            if (!Art.Enabled)
            {
                if (stone && stone.gameObject.activeSelf)
                {
                    stone.gameObject.SetActive(false);
                    V5TerrainGround.MarkDirty();
                }
                return;
            }

            Vector3 scale = transform.lossyScale, at = transform.position;
            bool same = shape != null && builtStyle == style && (scale - builtScale).sqrMagnitude < 1e-6f && (at - builtAt).sqrMagnitude < 1e-6f;
            if (!same) Build(scale, at);
            if (stone && !stone.gameObject.activeSelf)
            {
                stone.gameObject.SetActive(true);
                V5TerrainGround.MarkDirty();
            }
        }

        void Build(Vector3 scale, Vector3 at)
        {
            float width = Mathf.Abs(scale.x), depth = Mathf.Abs(scale.z);
            if (width <= .01f || depth <= .01f || Mathf.Abs(scale.y) <= .0001f) return;
            // Courses run along the wall; a short piece keeps the direction it was given.
            if (width >= depth * 1.6f) axis = 0;
            else if (depth >= width * 1.6f) axis = 1;
            else if (axis < 0) axis = width >= depth ? 0 : 1;
            // A map wall takes its stones from the world, so a cut piece keeps them.
            var rect = new Rect(at.x - width * .5f, at.z - depth * .5f, width, depth);
            var built = V5TerrainWalls.Build(style, rect, axis);
            if (shape != null && shape.mesh) Destroy(shape.mesh);
            shape = built;
            if (!stone)
            {
                var child = new GameObject(ChildName);
                stone = child.transform;
                stone.SetParent(transform, false);
                filter = child.AddComponent<MeshFilter>();
                var body = child.AddComponent<MeshRenderer>();
                body.shadowCastingMode = ShadowCastingMode.Off;
                body.receiveShadows = false;
                body.reflectionProbeUsage = ReflectionProbeUsage.Off;
                body.sharedMaterial = V5TerrainWalls.MaterialOf(style);
            }
            if (stone.TryGetComponent(out MeshRenderer own)) own.sharedMaterial = V5TerrainWalls.MaterialOf(style);
            filter.sharedMesh = built.mesh;
            // The child cancels the wall's scale and stands on the ground, whatever height the wall object sits at.
            stone.localRotation = Quaternion.identity;
            stone.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            stone.localPosition = new Vector3(0f, -at.y / scale.y, 0f);
            builtScale = scale;
            builtAt = at;
            builtStyle = style;
            V5TerrainGround.MarkDirty();
        }
    }
}
