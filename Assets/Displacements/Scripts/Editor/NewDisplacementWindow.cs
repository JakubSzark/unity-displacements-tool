using UnityEditor;
using UnityEngine;

namespace Szark
{
    /// <summary>
    /// This is a window for creating new displacements
    /// </summary>
    public class NewDisplacementWindow : EditorWindow
    {
        private int xSize = 10;
        private int zSize = 10;
        private int subDivisions = 1;

        private Displacement currentDisp;

        private void OnGUI()
        {
            if (currentDisp == null) return;

            EditorGUI.BeginChangeCheck();

            GUILayout.Label("Sub Divisions");
            subDivisions = EditorGUILayout.IntSlider(subDivisions, 1, 4);
            GUILayout.Label("Width");
            xSize = EditorGUILayout.IntField(xSize);
            GUILayout.Label("Depth");
            zSize = EditorGUILayout.IntField(zSize);

            if (EditorGUI.EndChangeCheck())
                currentDisp.Generate(xSize, zSize, subDivisions);

            if (GUILayout.Button("Create"))
            {
                currentDisp = null;
                Close();
            }
        }

        private void OnDestroy()
        {
            if (currentDisp != null)
                DestroyImmediate(currentDisp.gameObject);
        }

        private void OnFocus()
        {
            if (currentDisp == null)
            {
                currentDisp = CreateDisplacement();
                SetSelection(currentDisp.gameObject);
            }
        }

        private Displacement CreateDisplacement()
        {
            var obj = new GameObject("Displacement");
            var disp = obj.AddComponent<Displacement>();
            var renderer = obj.GetComponent<MeshRenderer>();
            var unlitShader = Shader.Find("Unlit/Color");
            renderer.sharedMaterial = new Material(unlitShader);
            disp.Generate(xSize, zSize, subDivisions);
            return disp;
        }

        private void SetSelection(GameObject obj) =>
            Selection.objects = new Object[] { obj };
    }
}