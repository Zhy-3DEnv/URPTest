using System;
using UnityEngine;

namespace Framework.XRenderPipeline {
    [Serializable, VolumeComponentMenu("Post-Processing/White Balance")]
    public sealed class WhiteBalance : VolumeComponent, IPostProcess {
        [Tooltip("Sets the white balance to a custom color temperature.")]
        public ClampedFloatParameter temperature = new ClampedFloatParameter(0f, -100, 100f);

        [Tooltip("Sets the white balance to compensate for a green or magenta tint.")]
        public ClampedFloatParameter tint = new ClampedFloatParameter(0f, -100, 100f);

        public bool IsActive() => temperature.value != 0f || tint.value != 0f;

        public bool IsOnChipMemoryCompatible() => true;
    }
}
