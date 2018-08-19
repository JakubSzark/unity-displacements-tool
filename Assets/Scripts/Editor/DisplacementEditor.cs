// Created By: Jakub P. Szarkowicz
// Email: Jakubshark@gmail.com

using UnityEditor;
using UnityEngine;
using System;

[CanEditMultipleObjects]
[CustomEditor(typeof(Displacement))]
public class DisplacementEditor : Editor
{
    private static Vector3 MousePos;

    private bool isPainting;

    private RaycastHit hit;
    private Displacement mainDisp;
    private Vector3 lastVertex;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DrawControls();
        DrawButtons();
    }

    private void OnSceneGUI()
    {
        UpdateMouseInput();
        UpdateMousePosition();
        LandscapeTerrain();
        KeyboardInput();
    }

    private void DrawButtons()
    {
        if (GUILayout.Button("Generate"))
            LoopSelection(d => d.Generate());

        if (GUILayout.Button("Fix Lighting"))
            LoopSelection(d => d.RecalculateLighting());

        if (Selection.objects.Length > 1)
            if (GUILayout.Button("Sew Displacements")) Sew();
    }

    private void DrawControls()
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.textColor = Color.green;
        boxStyle.margin = new RectOffset(20, 20, 10, 10);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("Controls:\nE: Move\tR: Sculpt\tT: Smooth", boxStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void LoopSelection(Action<Displacement> action)
    {
        foreach (var obj in Selection.gameObjects)
        {
            var d = obj.GetComponent<Displacement>();
            if (action != null) action.Invoke(d);
        }
    }

    private void LandscapeTerrain()
    {
        if (!Tools.hidden | !isPainting) return;

        LoopSelection(d =>
        {
            for (int i = 0; i < d.verts.Length; i++)
            {
                var vertexPos = d.transform.TransformPoint(d.verts[i]);
                var vertexDistToMouse = Vector3.Distance(MousePos, vertexPos);
                var vertexDistFalloff = 1 - ((vertexDistToMouse - ((1 - mainDisp.brushFalloff)
                    * mainDisp.brushSize)) / mainDisp.brushSize);

                if (vertexDistToMouse > mainDisp.brushSize) continue;
                int dir = (int)mainDisp.direction;
                var sculpt = Vector3.one * mainDisp.brushStrength;

                switch (mainDisp.type)
                {
                    case Displacement.BrushType.Sculpt:
                        sculpt *= Event.current.shift ? -1 : 1;
                        break;

                    case Displacement.BrushType.Smooth:
                        sculpt = (-d.verts[i] + lastVertex)
                            * 0.5f * mainDisp.brushStrength;
                        break;
                }

                sculpt *= vertexDistFalloff;
                sculpt.Scale(dir == 0 ? Vector3.right : dir == 1 ?
                    Vector3.up : dir == 2 ? Vector3.forward : Vector3.one);
                if (dir == 3) sculpt.Scale(hit.normal);

                d.verts[i] += sculpt;
                lastVertex = d.verts[i];
                d.UpdateMeshVertices();
            }
        });
    }

    private void KeyboardInput()
    {
        if (Event.current.keyCode == KeyCode.E)
            mainDisp.type = Displacement.BrushType.Move;
        if (Event.current.keyCode == KeyCode.R)
            mainDisp.type = Displacement.BrushType.Sculpt;
        if (Event.current.keyCode == KeyCode.T)
            mainDisp.type = Displacement.BrushType.Smooth;

        if (Event.current.keyCode == KeyCode.Escape)
            Selection.objects = new UnityEngine.Object[0];
    }

    private void UpdateMouseInput()
    {
        isPainting = (Event.current.type == EventType.MouseDown |
            Event.current.type == EventType.MouseDrag) &&
                Event.current.button == 0;

        if (Event.current.type == EventType.MouseUp)
            LoopSelection(d => d.UpdateColliderMesh());
    }

    private void UpdateMousePosition()
    {
        if (Selection.activeGameObject != null)
        {
            mainDisp = Selection.activeGameObject.GetComponent<Displacement>();
            if (Physics.Raycast(HandleUtility.GUIPointToWorldRay(Event.current.mousePosition), 
                out hit)) MousePos = hit.point;
        }
        else
        {
            mainDisp = null;
            Tools.hidden = false;
        }

        if (Event.current.type == EventType.MouseMove)
            SceneView.RepaintAll();

        if(mainDisp != null)
            Tools.hidden = mainDisp.type != Displacement.BrushType.Move;

        if(Tools.hidden)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    private void Sew()
    {
        var s = Selection.gameObjects;
        var d1 = s[0].GetComponent<Displacement>();
        var d2 = s[1].GetComponent<Displacement>();

        for (var i = 0; i < d1.verts.Length; i++)
        {
            var w1 = d1.transform.TransformPoint(d1.verts[i]);

            for (var j = 0; j < d2.verts.Length; j++)
            {
                var w2 = d2.transform.TransformPoint(d2.verts[j]);

                if (Vector3.Distance(w1, w2) <= 0.9f)
                {
                    d1.verts[i] = d1.transform.InverseTransformPoint(w2);
                    break;
                }
            }
        }

        d1.UpdateMeshVertices();
        d1.UpdateColliderMesh();

        d2.UpdateMeshVertices();
        d2.UpdateColliderMesh();
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawGizmo(Displacement disp, GizmoType type)
    {
        if (!Tools.hidden | disp.verts == null) return;

        Gizmos.color = Color.black;
        Gizmos.DrawSphere(MousePos, 0.1f);

        foreach(var v in disp.verts)
        {
            var vertexPos = disp.transform.TransformPoint(v);
            var vertexDistToMouse = Vector3.Distance(MousePos, vertexPos);
            var vertexDistFalloff = 1 - ((vertexDistToMouse - ((1 - disp.brushFalloff) 
                * disp.brushSize)) / disp.brushSize);

            if (vertexDistToMouse < disp.brushSize)
            {
                var color = Event.current.shift ? Color.red : Color.green;
                if (disp.type == Displacement.BrushType.Smooth) color = Color.blue;
                    Gizmos.color = Color.Lerp(Color.white, color, vertexDistFalloff);

                Gizmos.DrawCube(vertexPos, Vector3.one *
                    Mathf.Clamp(vertexDistFalloff, 0.1f, 0.2f));
            }
        }
    }

    [MenuItem("GameObject/3D Object/Displacement", false, 0)]
    static void CreateDisplacement()
    {
        var go = new GameObject("Displacement",
            typeof(Displacement));
        Selection.objects = new UnityEngine.Object[1] { go };
    }
}