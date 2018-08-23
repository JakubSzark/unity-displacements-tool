using UnityEditor;
using UnityEngine;

public class DisplacementWindow : EditorWindow
{
    private static bool IsSmoothing;
    private static bool IsSculpting;

    private static bool ShowOutline = true;
    private static bool AutoLighting = true;
    private static bool WorldSpace = true;

    private static Vector3 Pos = new Vector3(0, 0, 0);
    // (X = xSize, Y = ySize, Z = Subdivisions)
    private static Vector3Int Size = new Vector3Int(10, 10, 1);
    // (X = Size, Y = Strength, Z = Falloff)
    private static Vector3 Brush = new Vector3(3, 0.25f, 1.0f);

    private static Vector3 Mouse;
    private static Vector3 LastVertex;
    private static RaycastHit Hit;

    private static Mode CurrentMode;
    private static Direction CurrentDir;

    public enum Mode { Settings, Sculpt }
    public enum Direction { Normal, X, Y, Z }

    private void OnFocus()
    {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
        SceneView.onSceneGUIDelegate += OnSceneGUI;
    }

    private void OnDestroy() {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
    }

    [MenuItem("Tools/Displacement")]
    public static void ShowWindow() {
        GetWindow(typeof(DisplacementWindow), false, "Displacement");
    }

    [MenuItem("GameObject/3D Object/Displacement", false, 0)]
    private static Displacement CreateDisplacement()
    {
        var go = new GameObject("Displacement",
            typeof(Displacement));
        go.GetComponent<MeshRenderer>().sharedMaterial =
            new Material(Shader.Find("Standard"));
        Selection.objects = new Object[1] { go };
        go.transform.position = Pos;
        return go.GetComponent<Displacement>();
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawGizmo(Displacement disp, GizmoType type)
    {
        if (!Tools.hidden | CurrentMode != Mode.Sculpt) return;

        Gizmos.color = Color.black;
        Gizmos.DrawSphere(Mouse, 0.05f);

        foreach (var v in disp.verts)
        {
            var vertexPos = disp.transform.TransformPoint(v);
            var vertexDistToMouse = Vector3.Distance(Mouse, vertexPos);
            var vertexDistFalloff = 1 - ((vertexDistToMouse - ((1 - Brush.z) 
                * Brush.x)) / Brush.x);

            if (vertexDistToMouse < Brush.x)
            {
                var color = Event.current.shift ? Color.red : Color.green;
                if (IsSmoothing) color = Color.blue;
                Gizmos.color = Color.Lerp(Color.white, color, vertexDistFalloff);
                Gizmos.DrawSphere(vertexPos, Mathf.Clamp(vertexDistFalloff, 0.1f, 0.15f));
            }

            var sculpt = Vector3.one;
            sculpt *= Event.current.shift ? -1 : 1;

            int dir = (int)CurrentDir;
            sculpt.Scale(dir == 1 ? Vector3.right : dir == 2 ?
                Vector3.up : dir == 3 ? Vector3.forward : Vector3.one);
            if (dir == 0) sculpt.Scale(Hit.normal);

            if (!WorldSpace)
                sculpt = disp.transform.TransformDirection(sculpt);

            Gizmos.color = Color.black;
            Gizmos.DrawRay(Mouse, sculpt);
        }
    }

    private void OnSceneGUI(SceneView view)
    {
        DrawOutline();
        GetMouseInput();
        KeyboardInput();
        Sculpt();

        HandleUtility.Repaint();
        Repaint();
    }

    private void DrawOutline()
    {
        if (!ShowOutline | CurrentMode == Mode.Sculpt) return;
        if (Selection.gameObjects.Length > 1) return;

        Vector3[] lines = new Vector3[]
        {
            new Vector3(Size.x / 2, 0, Size.y / 2),
            new Vector3(-Size.x / 2, 0, Size.y / 2),
            new Vector3(Size.x / 2, 0, -Size.y / 2),
            new Vector3(-Size.x / 2, 0, -Size.y / 2),
            new Vector3(Size.x / 2, 0, -Size.y / 2),
            new Vector3(Size.x / 2, 0, Size.y / 2),
            new Vector3(-Size.x / 2, 0, -Size.y / 2),
            new Vector3(-Size.x / 2, 0, Size.y / 2)
        };

        for(int i = 0; i < lines.Length; i++)
            lines[i] += Pos;

        Handles.color = Color.red;
        Handles.DrawLines(lines);
    }

    private void OnGUI()
    {
        ShowTopBar();

        switch(CurrentMode)
        {
            case Mode.Settings:
                ShowSettingsPanel();

                break;

            case Mode.Sculpt:
                ShowSculptPanel();
                break;
        }
    }

    private void ShowTopBar()
    {
        CenterGUI(() => GUILayout.Label("Modes", EditorStyles.boldLabel));
        CenterGUI(() =>
        {
            if (GUILayout.Toggle(CurrentMode == Mode.Settings, "Settings (E)", "Button"))
                CurrentMode = Mode.Settings;
            if (GUILayout.Toggle(CurrentMode == Mode.Sculpt, "Sculpt (R)", "Button"))
                CurrentMode = Mode.Sculpt;
        });

        EditorGUILayout.Space();
    }

    private void ShowSettingsPanel()
    {
        Pos = EditorGUILayout.Vector3Field("Position", Pos);
        var newSize = EditorGUILayout.Vector2IntField("Size", 
            new Vector2Int(Size.x, Size.y));
        Size = new Vector3Int(newSize.x, newSize.y, Size.z);

        if (Size.x <= 0) Size.x = 1;
        if (Size.y <= 0) Size.y = 1;

        Size.z = EditorGUILayout.IntSlider("Subdivisions", Size.z, 1, 4);
        ShowOutline = EditorGUILayout.Toggle("Show Outline", ShowOutline);
        EditorGUILayout.Space();

        CenterGUI(() =>
        {
            if (GUILayout.Button("Calculate Lighting"))
                LoopSelection(d => d.RecalculateLighting());

            if (GUILayout.Button("Calculate Collider"))
                LoopSelection(d => d.UpdateColliderMesh());
        });

        CenterGUI(() =>
        {
            if (GUILayout.Button("Create Displacement"))
            {
                var d = CreateDisplacement();
                d.Generate(Size);
            }

            if (Selection.gameObjects.Length > 1)
                if (GUILayout.Button("Sew"))
                    Sew();
        });

        EditorGUILayout.Space();

        CenterGUI(() =>
        {
            GUI.contentColor = Color.red;
            if (GUILayout.Button("Reset Displacement(s)"))
                LoopSelection(d => d.Generate(Size));
            GUI.contentColor = Color.white;
        });
    }

    private void ShowSculptPanel()
    {
        Brush.x = EditorGUILayout.Slider("Brush Size", Brush.x, 0.1f, 10);
        Brush.y = EditorGUILayout.Slider("Brush Strength", Brush.y, 0.1f, 10);
        Brush.z = EditorGUILayout.Slider("Brush Falloff", Brush.z, 0.0f, 1);
        CurrentDir = (Direction)EditorGUILayout.EnumPopup("Brush Direction", CurrentDir);
        EditorGUILayout.Space();
        AutoLighting = EditorGUILayout.Toggle("Automatic Lighting", AutoLighting);
        WorldSpace = EditorGUILayout.Toggle("World Space", WorldSpace);
    }

    private void CenterGUI(System.Action action)
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (action != null) action.Invoke();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void LoopSelection(System.Action<Displacement> action)
    {
        foreach (var b in Selection.gameObjects)
        {
            var d = b.GetComponent<Displacement>();
            if (d != null)
                if (action != null) action.Invoke(d);
        }
    }

    private void GetMouseInput()
    {
        if (Physics.Raycast(HandleUtility.GUIPointToWorldRay(Event.current.mousePosition),
            out Hit)) Mouse = Hit.point;
        if (Event.current.type == EventType.MouseUp)
        {
            LoopSelection(d => 
            {
                d.UpdateColliderMesh();
                if(AutoLighting) d.RecalculateLighting();
            });
        }
    }

    private void Sculpt()
    {
        IsSculpting = (Event.current.type == EventType.MouseDown |
            Event.current.type == EventType.MouseDrag) &&
                Event.current.button == 0;

        IsSmoothing = Event.current.control & 
            CurrentMode == Mode.Sculpt;

        var displacementSelected = Selection.gameObjects.Length > 0 &&
            Selection.gameObjects[0].GetComponent<Displacement>();

        if (CurrentMode == Mode.Sculpt & displacementSelected)
            HandleUtility.AddDefaultControl(GUIUtility.
                GetControlID(FocusType.Passive));

        Tools.hidden = CurrentMode == Mode.Sculpt & displacementSelected;

        if (!Tools.hidden | !IsSculpting) return;

        LoopSelection(d =>
        {
            for (int i = 0; i < d.verts.Length; i++)
            {
                var vertexPos = d.transform.TransformPoint(d.verts[i]);
                var vertexDistToMouse = Vector3.Distance(Mouse, vertexPos);
                var vertexDistFalloff = 1 - ((vertexDistToMouse - ((1 - Brush.z)
                    * Brush.x)) / Brush.x);

                if (vertexDistToMouse > Brush.x) continue;
                var sculpt = Vector3.one * Brush.y;
                sculpt *= Event.current.shift ? -1 : 1;

                var vert = WorldSpace ? vertexPos : d.verts[i];

                if (IsSmoothing)
                    sculpt = (-vert + LastVertex) * 0.5f * Brush.y;
                sculpt *= vertexDistFalloff;

                int dir = (int)CurrentDir;
                sculpt.Scale(dir == 1 ? Vector3.right : dir == 2 ?
                    Vector3.up : dir == 3 ? Vector3.forward : Vector3.one);
                if (dir == 0) sculpt.Scale(Hit.normal);

                if (WorldSpace)
                    sculpt = d.transform.InverseTransformDirection(sculpt);

                d.verts[i] += sculpt;
                LastVertex = WorldSpace ? vertexPos : d.verts[i];
                d.UpdateMeshVertices();
            }
        });
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

    private void KeyboardInput()
    {
        if (Event.current.keyCode == KeyCode.E)
            CurrentMode = Mode.Settings;
        if (Event.current.keyCode == KeyCode.R)
            CurrentMode = Mode.Sculpt;
        
        if (Event.current.keyCode == KeyCode.Escape)
            Selection.objects = new Object[0];
    }
}