using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Sketch), editorForChildClasses: true)]
public class SketchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (target is not Sketch sketch)
            return;
        if (!Application.isPlaying || !sketch.isActiveAndEnabled)
            return;

        if (GUILayout.Button("Restart"))
            sketch.RestartSketch();
    }
}