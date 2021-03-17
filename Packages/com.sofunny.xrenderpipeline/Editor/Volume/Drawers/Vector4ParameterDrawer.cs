using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Rendering;

namespace Framework.XRenderPipeline {
    [VolumeParameterDrawer(typeof(Vector4Parameter))]
    sealed class Vector4ParametrDrawer : VolumeParameterDrawer {
        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title) {
            var value = parameter.value;

            if (value.propertyType != SerializedPropertyType.Vector4)
                return false;

            value.vector4Value = EditorGUILayout.Vector4Field(title, value.vector4Value);
            return true;
        }
    }
}
