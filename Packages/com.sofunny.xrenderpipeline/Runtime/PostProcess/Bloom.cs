using System;
using UnityEngine;

namespace Framework.XRenderPipeline {
    [Serializable, VolumeComponentMenu("Post-Processing/Bloom")]
    public sealed class Bloom : VolumeComponent, IPostProcess {

        [Tooltip("Filters out pixels under this level of brightness. Value is in gamma-space.")]
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);

        [Tooltip("Strength of the bloom filter.")]
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);

        [Tooltip("Changes the extent of veiling effects.")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);

        [Tooltip("Clamps pixels to control the bloom amount.")]
        public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);

        [Tooltip("Global tint of the bloom filter.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        [Tooltip("Enable upsampling blur to get high quality result. Get rid of mach bands")]
        public BoolParameter highQualityUpsampling = new BoolParameter(false);

        [Tooltip("Enable prefilter blur to eliminate bloom flickering")]
        public BoolParameter prefilterBlur = new BoolParameter(false);

        [Tooltip("Bloom mipmap count. Large number generate large bloom range, default is 4, min: 3, max: 6")]
        public ClampedIntParameter bloomMipCount = new ClampedIntParameter(5, 3, 6);

        public bool IsActive() => intensity.value > 0f;

        public bool IsOnChipMemoryCompatible() => false;
    }
}

