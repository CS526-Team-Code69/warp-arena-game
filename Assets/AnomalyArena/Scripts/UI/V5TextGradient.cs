using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{

    public sealed class V5TextGradient : BaseMeshEffect
    {
        public Color top = Color.white;
        public Color bottom = new(.66f, .88f, .96f);

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            var vertex = new UIVertex();
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                lowest = Mathf.Min(lowest, vertex.position.y);
                highest = Mathf.Max(highest, vertex.position.y);
            }
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.color = (Color32)((Color)vertex.color * Color.Lerp(bottom, top, Mathf.InverseLerp(lowest, highest, vertex.position.y)));
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
