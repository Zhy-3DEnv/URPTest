using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.XRenderPipeline {

    public struct RenderTargetHandle {
        const int k_CameraTargetID = -1;
        const int k_UserAssignedID = -2;
        public int id;
        RenderTargetIdentifier rtid;
        public static readonly RenderTargetHandle s_CameraTarget = new RenderTargetHandle { id = k_CameraTargetID };

        public void Init(string shaderProperty) {
            id = Shader.PropertyToID(shaderProperty);
        }

        public void Init(RenderTargetIdentifier renderTargetIdentifier) {
            id = k_UserAssignedID;
            rtid = renderTargetIdentifier;
        }

        public RenderTargetIdentifier Identifier() {
            if (id == k_CameraTargetID) {
                return BuiltinRenderTextureType.CameraTarget;
            }
            if (id == k_UserAssignedID) {
                return rtid;
            }
            return new RenderTargetIdentifier(id);
        }

        public bool Equals(RenderTargetHandle other) {
            if (id == k_UserAssignedID || other.id == k_UserAssignedID) {
                return Identifier() == other.Identifier();
            }
            return id == other.id;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }
            return obj is RenderTargetHandle && Equals((RenderTargetHandle)obj);
        }

        public override int GetHashCode() {
            return id;
        }

        public static bool operator ==(RenderTargetHandle lhs, RenderTargetHandle rhs) {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(RenderTargetHandle lhs, RenderTargetHandle rhs) {
            return !lhs.Equals(rhs);
        }
    }

}