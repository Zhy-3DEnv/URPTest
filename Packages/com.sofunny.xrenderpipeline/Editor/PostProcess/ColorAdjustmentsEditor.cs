using UnityEngine;
using UnityEditor;

namespace Framework.XRenderPipeline {
    [VolumeComponentEditor(typeof(ColorAdjustments))]
    sealed class ColorAdjustmentsEditor : VolumeComponentEditor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
        }
    }
}
