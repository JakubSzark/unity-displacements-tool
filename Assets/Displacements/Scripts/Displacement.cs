using UnityEngine;

namespace Szark
{
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class Displacement : MonoBehaviour
    {
        public Vector3[] Vertices { get; set; }

        public MeshFilter filter;
        private MeshCollider coll;

        [SerializeField, HideInInspector]
        private Vector3Int data;

        private void OnEnable()
        {
            filter = GetComponent<MeshFilter>();
            coll = GetComponent<MeshCollider>();
        }

        private void Update()
        {
            // Updates vertices in case this displacement is duplicated
            if (Vertices == null)
            {
                filter.sharedMesh = Instantiate(filter.sharedMesh);
                Vertices = filter.sharedMesh.vertices;
                UpdateColliderMesh();
            }
        }

        /// <summary>
        /// Generates a plane
        /// </summary>
        public void Generate(int xSize, int zSize, int subDivisions)
        {
            if (xSize == 0 || zSize == 0 || subDivisions == 0) return;

            filter.sharedMesh = new Mesh() { name = $"Displacement" };
            data = new Vector3Int(xSize, zSize, subDivisions);
            var subSize = new Vector2Int(xSize, zSize) * subDivisions;
            Vertices = new Vector3[(subSize.x + 1) * (subSize.y + 1)];

            var tangent = new Vector4(1f, 0f, 0f, -1f);
            var tangents = new Vector4[Vertices.Length];
            var uv = new Vector2[Vertices.Length];

            for (int i = 0, y = 0; y <= subSize.y; y++)
            {
                for (int x = 0; x <= subSize.x; x++, i++)
                {
                    tangents[i] = tangent;
                    uv[i] = new Vector2((float)x / subSize.x, (float)y / subSize.y);
                    Vertices[i] = new Vector3((x - subSize.x * 0.5f) / subDivisions,
                        0, (y - subSize.y * 0.5f) / subDivisions);
                }
            }

            filter.sharedMesh.vertices = Vertices;
            filter.sharedMesh.tangents = tangents;
            filter.sharedMesh.uv = uv;

            var triangles = new int[subSize.x * subSize.y * 6];

            for (int ti = 0, vi = 0, y = 0; y < subSize.y; y++, vi++)
            {
                for (int x = 0; x < subSize.x; x++, ti += 6, vi++)
                {
                    triangles[ti] = vi;
                    triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                    triangles[ti + 4] = triangles[ti + 1] = vi + subSize.x + 1;
                    triangles[ti + 5] = vi + subSize.x + 2;
                }
            }

            filter.sharedMesh.triangles = triangles;

            RecalculateLighting();
            UpdateColliderMesh();
        }

        /// <summary>
        /// Updates the vertices of the displacment
        /// </summary>
        public void UpdateMeshVertices() =>
            filter.sharedMesh.vertices = Vertices;

        /// <summary>
        /// Updates the collider to newest mesh
        /// </summary>
        public void UpdateColliderMesh() =>
            coll.sharedMesh = filter.sharedMesh;

        /// <summary>
        /// Calculates bounds, normals, and tangents for lighting
        /// </summary>
        public void RecalculateLighting()
        {
            filter.sharedMesh.RecalculateBounds();
            filter.sharedMesh.RecalculateTangents();
            filter.sharedMesh.RecalculateNormals();
        }

        /// <summary>
        /// Resets the displacement into a flat plane
        /// </summary>
        public void ResetToOriginal() =>
            Generate(data.x, data.y, data.z);
    }
}