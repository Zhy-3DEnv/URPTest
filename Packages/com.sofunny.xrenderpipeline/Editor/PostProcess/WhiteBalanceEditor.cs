using UnityEditor;

namespace Framework.XRenderPipeline {
    [VolumeComponentEditor(typeof(WhiteBalance))]
    sealed class WhiteBalanceEditor : VolumeComponentEditor
    {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
        }
    }
}
