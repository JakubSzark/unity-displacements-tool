using UnityEditor;
using UnityEngine;

using System.Collections.Generic;

namespace Szark
{
    /// <summary>
    /// This is the main toolbar window for the tool
    /// </summary>
    public class DisplacementToolbar : EditorWindow
    {
        enum Mode { Move, Sculpt }
        enum Direction { X, Y, Z, Normal }

        private static Mode mode = Mode.Move;
        private static Direction direction =
            Direction.Y;

        private static bool ctrlHeld;
        private static bool shiftHeld;
        private static bool mouseHeld;

        private static bool isSceneAssigned;
        private static bool autoLighting = true;

        private static float brushSize = 3;
        private static float brushStrength = 0.1f;
        private static float brushFalloff = 1.0f;

        private static Vector3 mousePos;

        private static EditorWindow newWindow;
        private static Displacement[] selected;
        private static RaycastHit hit;

        private static Texture2D newIcon, moveIcon,
            sculptIcon, sewIcon, refreshIcon;

        private void OnFocus()
        {
            newIcon = Resources.Load("Icons/NewIcon") as Texture2D;
            moveIcon = Resources.Load("Icons/MoveIcon") as Texture2D;
            sculptIcon = Resources.Load("Icons/SculptIcon") as Texture2D;
            refreshIcon = Resources.Load("Icons/RefreshIcon") as Texture2D;
            sewIcon = Resources.Load("Icons/SewIcon") as Texture2D;

            if (!isSceneAssigned)
            {
                SceneView.onSceneGUIDelegate += OnSceneGUI;
                isSceneAssigned = true;
            }
        }

        private void OnGUI()
        {
            DrawButtons();
            if (mode == Mode.Sculpt) DrawSculpt();
            DrawSettings();
        }

        private void OnDestroy() =>
            Tools.hidden = false;

        private void OnSceneGUI(SceneView view)
        {
            GetMouse();
            GetKeyboard();

            var displacements = GetSelected();
            selected = displacements;

            if (mode == Mode.Sculpt && mouseHeld)
            {
                if (!ctrlHeld)
                {
                    foreach (var d in displacements)
                        DisplacementTools.Sculpt(d, mousePos, GetBrushDirection(),
                            brushSize, brushStrength, brushFalloff);
                }
                else
                {
                    foreach (var d in displacements)
                        DisplacementTools.Smooth(d, mousePos, GetBrushDirection(),
                            brushSize, brushStrength, brushFalloff);
                }
            }

            if (mode == Mode.Sculpt && selected.Length > 0)
                HandleUtility.AddDefaultControl(GUIUtility.
                    GetControlID(FocusType.Passive));

            Repaint();
        }

        private static Vector3 GetBrushDirection()
        {
            var dir = Vector3.zero;

            switch (direction)
            {
                case Direction.X: dir = Vector3.right; break;
                case Direction.Y: dir = Vector3.up; break;
                case Direction.Z: dir = Vector3.forward; break;
                case Direction.Normal: dir = hit.normal; break;
            }

            if (shiftHeld) dir *= -1;
            return dir;
        }

        private void GetKeyboard()
        {
            ctrlHeld = Event.current.control;
            shiftHeld = Event.current.shift;
        }

        private void GetMouse()
        {
            if (Physics.Raycast(HandleUtility.GUIPointToWorldRay
                (Event.current.mousePosition), out hit))
                mousePos = hit.point;

            mouseHeld = (Event.current.type == (EventType)(0 | 3))
                && Event.current.button == 0;

            if (Event.current.type == EventType.MouseUp)
            {
                LoopSelection(d =>
                {
                    d.UpdateColliderMesh();
                    if (autoLighting)
                        d.RecalculateLighting();
                });
            }
        }

        private void DrawButtons()
        {
            if (GUILayout.Button(newIcon))
                newWindow = GetWindow<NewDisplacementWindow>
                    (true, "New Displacement");

            CenterGUI(() => GUILayout.Label($"Tools",
                EditorStyles.boldLabel));

            if (GUILayout.Toggle(mode == Mode.Move, moveIcon, "Button")) mode = Mode.Move;
            if (GUILayout.Toggle(mode == Mode.Sculpt, sculptIcon, "Button")) mode = Mode.Sculpt;

            CenterGUI(() => GUILayout.Label($"Mode: {mode}",
                EditorStyles.boldLabel));

            Tools.hidden = mode != Mode.Move;
        }

        private void DrawBrushFields()
        {
            EditorGUILayout.LabelField("Brush Size");
            brushSize = EditorGUILayout.FloatField(brushSize);
            EditorGUILayout.LabelField("Brush Strength");
            brushStrength = EditorGUILayout.FloatField(brushStrength);
            EditorGUILayout.LabelField("Brush Falloff");
            brushFalloff = EditorGUILayout.FloatField(brushFalloff);
        }

        private void DrawSculpt()
        {
            DrawBrushFields();
            EditorGUILayout.LabelField("Direction");
            direction = (Direction)EditorGUILayout.EnumPopup(direction);
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space();
            CenterGUI(() => GUILayout.Label($"Settings",
                EditorStyles.boldLabel));

            if (GUILayout.Button(sewIcon)) DisplacementTools.Sew(GetSelected());
            if (GUILayout.Button(refreshIcon)) Recalculate();

            GUI.color = Color.red;
            if (GUILayout.Button("Reset")) ResetDisplacements();
            GUI.color = Color.white;

            autoLighting = GUILayout.Toggle(autoLighting, "Auto Lighting");
        }

        private void CenterGUI(System.Action action)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            action?.Invoke();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private Displacement[] GetSelected()
        {
            var displacements = new List<Displacement>();
            foreach (var selection in Selection.gameObjects)
            {
                var disp = selection.GetComponent<Displacement>();
                if (disp != null) displacements.Add(disp);
            }

            return displacements.ToArray();
        }

        private void LoopSelection(System.Action<Displacement> action) {
            foreach (var b in GetSelected()) action?.Invoke(b);
        }

        private void Recalculate() =>
            LoopSelection(d => d.RecalculateLighting());

        private void ResetDisplacements() =>
            LoopSelection(d => d.ResetToOriginal());

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawGizmo(Displacement disp, GizmoType type)
        {
            if (mode != Mode.Sculpt) return;

            Gizmos.color = Color.black;
            Gizmos.DrawSphere(mousePos, 0.05f);

            foreach (var v in disp.Vertices)
            {
                var vPos = disp.transform.TransformPoint(v);
                var pos = disp.transform.InverseTransformPoint(mousePos);
                var dist = Vector3.Distance(pos, v);
                var falloff = 1 - (dist / brushSize * brushFalloff);

                if (dist < brushSize)
                {
                    var color = shiftHeld ? Color.red : ctrlHeld ? Color.blue : Color.green;
                    Gizmos.color = Color.Lerp(Color.white, color, falloff);
                    Gizmos.DrawSphere(vPos, Mathf.Clamp(falloff, 0.1f, 0.15f));
                }

                Gizmos.color = Color.black;
                Gizmos.DrawRay(mousePos, GetBrushDirection());
            }
        }

        [MenuItem("Tools/Szark's Tools/Displacements Toolbar")]
        public static void ShowWindow() =>
            GetWindow<DisplacementToolbar>("Displacements Toolbar");
    }
}