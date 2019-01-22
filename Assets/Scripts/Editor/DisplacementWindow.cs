using UnityEditor;
using UnityEngine;

public class DisplacementWindow : EditorWindow
{
    private static bool isSmoothing;
    private static bool isSculpting;

    private static bool showOutline = true;
    private static bool autoLighting = true;
    private static bool worldSpace = true;

    private static Vector3 pos = new Vector3(0, 0, 0);
    // (X = xSize, Y = ySize, Z = Subdivisions)
    private static Vector3Int size = new Vector3Int(10, 10, 1);
    // (X = Size, Y = Strength, Z = Falloff)
    private static Vector3 brush = new Vector3(3, 0.25f, 1.0f);

    private static Vector3 mouse;
    private static Vector3 lastVertex;
    private static RaycastHit hit;

    private static Mode currentMode;
    private static Direction currentDir;

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
        go.transform.position = pos;
        return go.GetComponent<Displacement>();
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawGizmo(Displacement disp, GizmoType type)
    {
        if (!Tools.hidden | currentMode != Mode.Sculpt) return;

        Gizmos.color = Color.black;
        Gizmos.DrawSphere(mouse, 0.05f);

        foreach (var v in disp.verts)
        {
            var vertexPos = disp.transform.TransformPoint(v);
            var vertexDistToMouse = Vector3.Distance(mouse, vertexPos);
            var vertexDistFalloff = 1 - ((vertexDistToMouse - ((1 - brush.z) 
                * brush.x)) / brush.x);

            if (vertexDistToMouse < brush.x)
            {
                var color = Event.current.shift ? Color.red : Color.green;
                if (isSmoothing) color = Color.blue;
                Gizmos.color = Color.Lerp(Color.white, color, vertexDistFalloff);
                Gizmos.DrawSphere(vertexPos, Mathf.Clamp(vertexDistFalloff, 0.1f, 0.15f));
            }

            var sculpt = Vector3.one;
            sculpt *= Event.current.shift ? -1 : 1;

            int dir = (int)currentDir;
            sculpt.Scale(dir == 1 ? Vector3.right : dir == 2 ?
                Vector3.up : dir == 3 ? Vector3.forward : Vector3.one);
            if (dir == 0) sculpt.Scale(hit.normal);

            if (!worldSpace)
                sculpt = disp.transform.TransformDirection(sculpt);

            Gizmos.color = Color.black;
            Gizmos.DrawRay(mouse, sculpt);
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
        if (!showOutline | currentMode == Mode.Sculpt) return;
        if (Selection.gameObjects.Length > 1) return;

        Vector3[] lines = new Vector3[]
        {
            new Vector3(size.x / 2, 0, size.y / 2),
            new Vector3(-size.x / 2, 0, size.y / 2),
            new Vector3(size.x / 2, 0, -size.y / 2),
            new Vector3(-size.x / 2, 0, -size.y / 2),
            new Vector3(size.x / 2, 0, -size.y / 2),
            new Vector3(size.x / 2, 0, size.y / 2),
            new Vector3(-size.x / 2, 0, -size.y / 2),
            new Vector3(-size.x / 2, 0, size.y / 2)
        };

        for(int i = 0; i < lines.Length; i++)
            lines[i] += pos;

        Handles.color = Color.red;
        Handles.DrawLines(lines);
    }

    private void OnGUI()
    {
        ShowTopBar();

        switch(currentMode)
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
            if (GUILayout.Toggle(currentMode == Mode.Settings, "Settings (E)", "Button"))
                currentMode = Mode.Settings;
            if (GUILayout.Toggle(currentMode == Mode.Sculpt, "Sculpt (R)", "Button"))
                currentMode = Mode.Sculpt;
        });

        EditorGUILayout.Space();
    }

    private void ShowSettingsPanel()
    {
        pos = EditorGUILayout.Vector3Field("Position", pos);
        var newSize = EditorGUILayout.Vector2IntField("Size", 
            new Vector2Int(size.x, size.y));
        size = new Vector3Int(newSize.x, newSize.y, size.z);

        if (size.x <= 0) size.x = 1;
        if (size.y <= 0) size.y = 1;

        size.z = EditorGUILayout.IntSlider("Subdivisions", size.z, 1, 4);
        showOutline = EditorGUILayout.Toggle("Show Outline", showOutline);
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
                d.Generate(size);
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
                LoopSelection(d => d.Generate(size));
            GUI.contentColor = Color.white;
        });
    }

    private void ShowSculptPanel()
    {
        brush.x = EditorGUILayout.Slider("Brush Size", brush.x, 0.1f, 10);
        brush.y = EditorGUILayout.Slider("Brush Strength", brush.y, 0.1f, 10);
        brush.z = EditorGUILayout.Slider("Brush Falloff", brush.z, 0.0f, 1);
        currentDir = (Direction)EditorGUILayout.EnumPopup("Brush Direction", currentDir);
        EditorGUILayout.Space();
        autoLighting = EditorGUILayout.Toggle("Automatic Lighting", autoLighting);
        worldSpace = EditorGUILayout.Toggle("World Space", worldSpace);
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
            out hit)) mouse = hit.point;
        if (Event.current.type == EventType.MouseUp)
        {
            LoopSelection(d => 
            {
                d.UpdateColliderMesh();
                if(autoLighting) d.RecalculateLighting();
            });
        }
    }

    private void Sculpt()
    {
        isSculpting = (Event.current.type == EventType.MouseDown |
            Event.current.type == EventType.MouseDrag) &&
                Event.current.button == 0;

        isSmoothing = Event.current.control & 
            currentMode == Mode.Sculpt;

        var displacementSelected = Selection.gameObjects.Length > 0 &&
            Selection.gameObjects[0].GetComponent<Displacement>();

        if (currentMode == Mode.Sculpt & displacementSelected)
            HandleUtility.AddDefaultControl(GUIUtility.
                GetControlID(FocusType.Passive));

        Tools.hidden = currentMode == Mode.Sculpt & displacementSelected;

        if (!Tools.hidden | !isSculpting) return;

        LoopSelection(d =>
        {
            for (int i = 0; i < d.verts.Length; i++)
            {
                var vertexPos = d.transform.TransformPoint(d.verts[i]);
                var vertexDistToMouse = Vector3.Distance(mouse, vertexPos);
                var vertexDistFalloff = 1 - ((vertexDistToMouse - ((1 - brush.z)
                    * brush.x)) / brush.x);

                if (vertexDistToMouse > brush.x) continue;
                var sculpt = Vector3.one * brush.y;
                sculpt *= Event.current.shift ? -1 : 1;

                var vert = worldSpace ? vertexPos : d.verts[i];

                if (isSmoothing)
                    sculpt = (-vert + lastVertex) * 0.5f * brush.y;
                sculpt *= vertexDistFalloff;

                int dir = (int)currentDir;
                sculpt.Scale(dir == 1 ? Vector3.right : dir == 2 ?
                    Vector3.up : dir == 3 ? Vector3.forward : Vector3.one);
                if (dir == 0) sculpt.Scale(hit.normal);

                if (worldSpace)
                    sculpt = d.transform.InverseTransformDirection(sculpt);

                d.verts[i] += sculpt;
                lastVertex = worldSpace ? vertexPos : d.verts[i];
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
            currentMode = Mode.Settings;
        if (Event.current.keyCode == KeyCode.R)
            currentMode = Mode.Sculpt;
        
        if (Event.current.keyCode == KeyCode.Escape)
            Selection.objects = new Object[0];
    }
}