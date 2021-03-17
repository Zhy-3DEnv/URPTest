using System;
using UnityEngine;
using UnityEditor;

public static class ShaderGUICommon {

    public enum SurfaceType {
        Opaque = 0,
        Transparent
    }

    public enum CullMode {
        None = 0,
        Front = 1,
        Back = 2
    }

    public static void DoPopup(GUIContent label, MaterialProperty property, string[] options, MaterialEditor materialEditor) {
        if (property == null) {
            throw new ArgumentNullException("property");
        }

        EditorGUI.showMixedValue = property.hasMixedValue;

        var mode = property.floatValue;
        EditorGUI.BeginChangeCheck();
        mode = EditorGUILayout.Popup(label, (int)mode, options);
        if (EditorGUI.EndChangeCheck()) {
            materialEditor.RegisterPropertyChangeUndo(label.text);
            property.floatValue = mode;
        }

        EditorGUI.showMixedValue = false;
    }
}

