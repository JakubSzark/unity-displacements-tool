// Created By: Jakub P. Szarkowicz
// Email: Jakubshark@gmail.com

using System;
using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class Displacement : MonoBehaviour 
{
    [Header("Base")]
    public int displacementSizeX = 10;
    public int displacementSizeZ = 10;
    [Range(1, 4)]
    public int subDivision = 2;

    [Header("Brush")]
    [Range(1, 10)]
    public float brushSize = 3;
    [Range(0.01f, 1)]
    public float brushStrength = 0.25f;
    [Range(0, 1)]
    public float brushFalloff = 0.1f;
    public Direction bDir = (Direction)1;
    public BrushType bType = 0;

    [SerializeField]
    [HideInInspector]
    private int instanceID = 0;

    [HideInInspector]
    public Vector3[] verts;

    private MeshFilter filter;
    private MeshCollider coll;

    [Serializable]
    public class Tex
    {
        public Texture2D texture;
        public float smoothness;
        public float metallic;
    }

    public enum Direction
    {
        X, Y, Z, Normal
    }

    public enum BrushType
    {
        Move,
        Sculpt,
        Smooth
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        filter = GetComponent<MeshFilter>();
        coll = GetComponent<MeshCollider>();

        var m = new Mesh();
        filter.sharedMesh = m;
        filter.sharedMesh.name = "Displacement" + UnityEngine.Random.Range(0, 10000);

        var width = displacementSizeX * subDivision;
        var depth = displacementSizeZ * subDivision;

        verts = new Vector3[(width + 1) * (depth + 1)];

        var tangents = new Vector4[verts.Length];
        var tangent = new Vector4(1f, 0f, 0f, -1f);
        var uv = new Vector2[verts.Length];

        for (int i = 0, y = 0; y <= depth; y++)
        {
            for (int x = 0; x <= width; x++, i++)
            {
                uv[i] = new Vector2((float)x / width, (float)y / depth);
                verts[i] = new Vector3((float)(x - width / 2) / subDivision, 0, 
                    (float)(y - depth / 2) / subDivision);
                tangents[i] = tangent;
            }
        }

        filter.sharedMesh.vertices = verts;
        filter.sharedMesh.tangents = tangents;
        filter.sharedMesh.uv = uv;

        var triangles = new int[width * depth * 6];

        for (int ti = 0, vi = 0, y = 0; y < depth; y++, vi++)
        {
            for (int x = 0; x < width; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                triangles[ti + 4] = triangles[ti + 1] = vi + width + 1;
                triangles[ti + 5] = vi + width + 2;
            }
        }

        filter.sharedMesh.triangles = triangles;
        filter.sharedMesh.RecalculateNormals();
        coll.sharedMesh = filter.sharedMesh;
    }

    public void RecalculateMesh() {
        GetComponent<MeshFilter>().sharedMesh.vertices = verts;
    }

    public void RecalculateLighting()
    {
        GetComponent<MeshFilter>().sharedMesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh.RecalculateTangents();
        GetComponent<MeshFilter>().sharedMesh.RecalculateNormals();
    }

    public void RecalculateCollider()
    {
        GetComponent<MeshCollider>().sharedMesh = 
            GetComponent<MeshFilter>().sharedMesh;
    }

    #if UNITY_EDITOR
    [ExecuteInEditMode]
    void Awake()
    {
        if (Application.isPlaying)
            return;

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
}
