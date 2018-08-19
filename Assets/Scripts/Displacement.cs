// Created By: Jakub P. Szarkowicz
// Email: Jakubshark@gmail.com

using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]

[DisallowMultipleComponent]
public class Displacement : MonoBehaviour 
{
    [Header("Base")]
    public Vector2Int size = new Vector2Int(10, 10);
    [Range(1, 4)] public int subDivisions = 1;

    [Header("Brush")]
    [Range(1, 9)] public float brushSize = 3f;
    [Range(0, 1)] public float brushStrength = 0.25f;
    [Range(0, 1)] public float brushFalloff = 1f;

    public Direction direction = Direction.Normal;
    public BrushType type = BrushType.Move;

    [HideInInspector]
    public Vector3[] verts;

    [SerializeField, HideInInspector]
    private int instanceID = 0;

    public enum Direction { X, Y, Z, Normal }
    public enum BrushType { Move, Sculpt, Smooth }

    #if UNITY_EDITOR
    [ExecuteInEditMode]
    void Awake()
    {
        if (Application.isPlaying) return;

        if (instanceID == 0)
        {
            instanceID = GetInstanceID();
            return;
        }

        if (instanceID != GetInstanceID() && GetInstanceID() < 0)
        {
            instanceID = GetInstanceID();
            Generate();
        }
    }
    #endif

    public void Generate()
    {
        var filter = GetComponent<MeshFilter>();

        filter.sharedMesh = new Mesh
        {
            name = "Displacement" +
            Random.Range(0, 10000)
        };

        var subSize = size * subDivisions;
        verts = new Vector3[(subSize.x + 1) * (subSize.y + 1)];

        var tangents = new Vector4[verts.Length];
        var tangent = new Vector4(1f, 0f, 0f, -1f);
        var uv = new Vector2[verts.Length];

        for (int i = 0, y = 0; y <= subSize.y; y++)
        {
            for (int x = 0; x <= subSize.x; x++, i++)
            {
                uv[i] = new Vector2((float)x / subSize.x, (float)y / subSize.y);
                verts[i] = new Vector3((float)(x - subSize.x / 2) / subDivisions, 0, 
                    (float)(y - subSize.y / 2) / subDivisions);
                tangents[i] = tangent;
            }
        }

        filter.sharedMesh.vertices = verts;
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
        filter.sharedMesh.RecalculateNormals();
        UpdateColliderMesh();
    }

    public void UpdateMeshVertices() {
        GetComponent<MeshFilter>().sharedMesh.vertices = verts;
    }

    public void RecalculateLighting()
    {
        GetComponent<MeshFilter>().sharedMesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh.RecalculateTangents();
        GetComponent<MeshFilter>().sharedMesh.RecalculateNormals();
    }

    public void UpdateColliderMesh()
    {
        GetComponent<MeshCollider>().sharedMesh = 
            GetComponent<MeshFilter>().sharedMesh;
    }
}