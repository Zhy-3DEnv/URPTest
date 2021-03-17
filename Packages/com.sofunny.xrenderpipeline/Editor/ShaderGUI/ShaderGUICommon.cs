using System;
using UnityEngine;
using UnityEditor;

namespace Framework.XRenderPipeline {

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

        public static void TwoFloatSingleLine(GUIContent title, MaterialProperty prop1, GUIContent prop1Label,
            MaterialProperty prop2, GUIContent prop2Label, MaterialEditor materialEditor, float labelWidth = 30f) {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop1.hasMixedValue || prop2.hasMixedValue;
            Rect rect = EditorGUILayout.GetControlRect();
            EditorGUI.PrefixLabel(rect, title);
            var indent = EditorGUI.indentLevel;
            var preLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = labelWidth;
            Rect propRect1 = new Rect(rect.x + preLabelWidth, rect.y,
                (rect.width - preLabelWidth) * 0.5f, EditorGUIUtility.singleLineHeight);
            var prop1val = EditorGUI.FloatField(propRect1, prop1Label, prop1.floatValue);

            Rect propRect2 = new Rect(propRect1.x + propRect1.width, rect.y,
                propRect1.width, EditorGUIUtility.singleLineHeight);
            var prop2val = EditorGUI.FloatField(propRect2, prop2Label, prop2.floatValue);

            EditorGUI.indentLevel = indent;
            EditorGUIUtility.labelWidth = preLabelWidth;

            if (EditorGUI.EndChangeCheck()) {
                materialEditor.RegisterPropertyChangeUndo(title.text);
                prop1.floatValue = prop1val;
                prop2.floatValue = prop2val;
            }

            EditorGUI.showMixedValue = false;
        }
    }

}

