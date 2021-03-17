using System;
using UnityEngine;

namespace Framework.XRenderPipeline {
    public enum TonemappingMode {
        None,
        Neutral,
        ACES,
        Uchimura
    }

    [Serializable, VolumeComponentMenu("Post-Processing/Tonemapping")]
    public sealed class Tonemapping : VolumeComponent, IPostProcess {
        [Tooltip("Select a tonemapping algorithm to use for the color grading process.")]
        public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None);

        public bool IsActive() => mode.value != TonemappingMode.None;

        public bool IsOnChipMemoryCompatible() => true;
    }

    [Serializable]
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> {
        public TonemappingModeParameter(TonemappingMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}
