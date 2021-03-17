using UnityEngine;
using UnityEditor;

namespace Framework.XRenderPipeline {
    [CustomPropertyDrawer(typeof(Quaternion))]
    class QuaternionPropertyDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var euler = property.quaternionValue.eulerAngles;
            EditorGUI.BeginChangeCheck();
            euler = EditorGUI.Vector3Field(position, label, euler);
            if (EditorGUI.EndChangeCheck())
                property.quaternionValue = Quaternion.Euler(euler);
        }
    }

}
