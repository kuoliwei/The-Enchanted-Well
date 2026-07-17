using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RadialWaveBlurController))]
public class RadialWaveBlurControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var ctrl = (RadialWaveBlurController)target;
        var presetNames = ctrl.GetPresetNames();

        if (presetNames != null && presetNames.Length > 0)
        {
            var indexProp = serializedObject.FindProperty("selectedPresetIndex");

            EditorGUI.BeginChangeCheck();
            int newIdx = EditorGUILayout.Popup("Preset", indexProp.intValue, presetNames);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ctrl, "Change Radial Wave Blur Preset");
                ctrl.ApplyPreset(newIdx);
                EditorUtility.SetDirty(ctrl);
            }

            EditorGUILayout.Space();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Presets not loaded. Make sure RadialWaveBlurPresets.json exists in Assets/Resources/.",
                MessageType.Warning);
            EditorGUILayout.Space();
        }

        DrawDefaultInspector();
    }
}
