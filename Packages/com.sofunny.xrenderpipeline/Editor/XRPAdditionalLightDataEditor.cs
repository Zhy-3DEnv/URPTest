using UnityEngine.Rendering;
using UnityEditor;

namespace Framework.XRenderPipeline {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(XRPAdditionalLightData))]
    public class XRPAdditionLightDataEditor : Editor {
        [MenuItem("CONTEXT/XRPAdditionalLightData/Remove Component")]
        static void RemoveComponent(MenuCommand command) {
            if (EditorUtility.DisplayDialog("Remove Component?", "Are you sure you want to remove this component? If you do, you will lose some settings.", "Remove", "Cancel")) {
                Undo.DestroyObjectImmediate(command.context);
            }
        }
    }
}
