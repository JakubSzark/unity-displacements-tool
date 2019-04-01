using UnityEngine;

namespace Szark
{
    /// <summary>
    /// In charge of doing calculation on a displacement
    /// or multiple displacements
    /// </summary>
    public class DisplacementTools : MonoBehaviour
    {
        /// <summary>
        /// Sculpts vertices on a displacement
        /// </summary>
        public static void Sculpt(Displacement d, Vector3 pos, Vector3 direction,
            float brushSize, float brushStrength, float brushFalloff)
        {
            for (int i = 0; i < d.Vertices.Length; i++)
            {
                // World Space Points
                var wPos = d.transform.TransformPoint(d.Vertices[i]);

                // Modifiers
                var dist = Vector3.Distance(pos, wPos);
                var falloff = 1 - (dist / brushSize * brushFalloff);
                if (dist > brushSize) continue;

                // Modify the Vertex
                d.Vertices[i] += direction * falloff * brushStrength;

                // Update the Mesh
                d.UpdateMeshVertices();
            }
        }

        /// <summary>
        /// Smooths vertices on a displacement
        /// </summary>
        public static void Smooth(Displacement d, Vector3 pos, Vector3 direction,
            float brushSize, float brushStrength, float brushFalloff)
        {
            for (int i = 0; i < d.Vertices.Length; i++)
            {
                // World Space Points
                var wPos = d.transform.TransformPoint(d.Vertices[i]);

                var smoothDir = (pos - wPos).normalized;
                smoothDir.Scale(direction);

                // Modifiers
                var dist = Vector3.Distance(pos, wPos);
                var falloff = 1 - (dist / brushSize * brushFalloff);
                if (dist > brushSize) continue;

                // Modify the Vertex
                d.Vertices[i] += smoothDir * falloff * brushStrength;

                // Update the Mesh
                d.UpdateMeshVertices();
            }
        }

        /// <summary>
        /// Sews the edges of displacements that are next to each other
        /// </summary>
        public static void Sew(Displacement[] d, float sewRange = 1.0f)
        {
            if (d.Length <= 1) return;

            for (int i = 0; i < d.Length; i++)
            {
                for (int v = 0; v < d[i].Vertices.Length; v++)
                {
                    var v1 = d[i].transform.TransformPoint(d[i].Vertices[v]);

                    for (int j = 0; j < d.Length; j++)
                    {
                        foreach (var w in d[j].Vertices)
                        {
                            var v2 = d[j].transform.TransformPoint(w);
                            if (Vector3.Distance(v1, v2) < sewRange)
                                d[i].Vertices[v].y = d[i].transform.InverseTransformPoint(v2).y;
                        }
                    }
                }

                d[i].UpdateMeshVertices();
                d[i].UpdateMeshVertices();
            }
        }
    }
}