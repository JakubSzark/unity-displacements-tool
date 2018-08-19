// Created By: Jakub P. Szarkowicz
// Email: Jakubshark@gmail.com

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Displacement))]
[CanEditMultipleObjects]
public class DisplacementEditor : Editor
{
    private Displacement disp;
    private static Vector3 mousePos;
    private RaycastHit hit;

    private bool isPainting;
    private Vector3 lastVector;

    [MenuItem("GameObject/3D Object/Displacement", false, 0)]
    static void CreateDisplacement()
    {
        var go = new GameObject("Displacement", 
            typeof(Displacement));
        Selection.objects = new Object[1] { go };
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(10);

        if (GUILayout.Button("Generate"))
        {
            foreach(var obj in Selection.gameObjects)
            {
                var d = obj.GetComponent<Displacement>();
                if (d == null) continue;
                d.Generate();
            }
        }

        if (GUILayout.Button("Fix Lighting"))
        {
            foreach (var obj in Selection.gameObjects)
            {
                var d = obj.GetComponent<Displacement>();
                if (d == null) continue;
                d.RecalculateLighting();
            }
        }

        if (Selection.objects.Length > 1) {
            if (GUILayout.Button("Sew Displacements")) Sew();
        }
    }

    private void OnSceneGUI()
    {
        UpdateMouseInput();
        UpdateMousePosition();
        LandscapeTerrain();
        KeyboardInput();
    }

    private void LandscapeTerrain()
    {
        if (!Tools.hidden | !isPainting) return;

        foreach(var obj in Selection.gameObjects)
        {
            var d = obj.GetComponent<Displacement>();
            if (d == null) continue;

            for (int i = 0; i < d.verts.Length; i++)
            {
                var vertexPos = d.transform.TransformPoint(d.verts[i]);
                var vertexDistToMouse = Vector3.Distance(mousePos, vertexPos);
                var vertexDistFalloff = 1 - ((vertexDistToMouse - ((1 - disp.brushFalloff)
                    * disp.brushSize)) / disp.brushSize);

                if (vertexDistToMouse > disp.brushSize) continue;
                int dir = (int)disp.bDir;
                Vector3 sculpt = Vector3.one * disp.brushStrength;

                switch (disp.bType)
                {
                    case Displacement.BrushType.Sculpt:
                        sculpt *= Event.current.shift ? -1 : 1;
                        break;

                    case Displacement.BrushType.Smooth:
                        sculpt = (-d.verts[i] + lastVector)
                            * 0.5f * disp.brushStrength;
                        break;
                }

                sculpt *= vertexDistFalloff;
                sculpt.Scale(dir == 0 ? Vector3.right : dir == 1 ?
                    Vector3.up : dir == 2 ? Vector3.forward : Vector3.one);
                if (dir == 3) sculpt.Scale(hit.normal);

                d.verts[i] += sculpt;

                lastVector = d.verts[i];
                d.RecalculateMesh();
            }
        }
    }

    private void KeyboardInput()
    {
        if (Event.current.keyCode == KeyCode.E)
            disp.bType = Displacement.BrushType.Move;
        if (Event.current.keyCode == KeyCode.R)
            disp.bType = Displacement.BrushType.Sculpt;
        if (Event.current.keyCode == KeyCode.T)
            disp.bType = Displacement.BrushType.Smooth;
        if (Event.current.keyCode == KeyCode.Escape)
            Selection.objects = new Object[0];
    }

    private void UpdateMouseInput()
    {
        isPainting = (Event.current.type == EventType.MouseDown |
            Event.current.type == EventType.MouseDrag) &&
                Event.current.button == 0;


        if(Event.current.type == EventType.MouseUp)
        {
            foreach (var obj in Selection.gameObjects)
            {
                var d = obj.GetComponent<Displacement>();
                if (d == null) continue;
                d.RecalculateCollider();
            }
        }
    }

    private void UpdateMousePosition()
    {
        var active = Selection.activeGameObject;
        if (active != null && active.GetComponent<Displacement>())
        {
            disp = active.GetComponent<Displacement>();
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out hit)) mousePos = hit.point;
        }
        else
        {
            disp = null;
            Tools.hidden = false;
        }

        if (Event.current.type == EventType.MouseMove)
            SceneView.RepaintAll();

        if(disp != null)
            Tools.hidden = disp.bType != Displacement.BrushType.Move;

        if(Tools.hidden)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawGizmo(Displacement disp, GizmoType type)
    {
        if (!Tools.hidden) return;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(mousePos, 0.1f);

        if (disp == null | disp.verts == null) return;

        foreach(var v in disp.verts)
        {
            var vertexPos = disp.transform.TransformPoint(v);
            var vertexDistToMouse = Vector3.Distance(mousePos, vertexPos);
            var vertexDistFalloff = 1 - ((vertexDistToMouse - ((1 - disp.brushFalloff) 
                * disp.brushSize)) / disp.brushSize);

            var color = Event.current.shift ? Color.red : Color.green;
            Gizmos.color = Color.Lerp(Color.black, color, vertexDistFalloff);

            if(vertexDistToMouse < disp.brushSize)
                Gizmos.DrawSphere(vertexPos, 0.1f);
        }
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

                if(Vector3.Distance(w1, w2) <= 0.9f)
                {
                    d1.verts[i] = d1.transform.InverseTransformPoint(w2);
                    break;
                }
            }
        }

        d1.RecalculateMesh();
        d1.RecalculateCollider();

        d2.RecalculateMesh();
        d2.RecalculateCollider();
    }
}
