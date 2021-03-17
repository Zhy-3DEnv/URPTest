﻿using System;
using UnityEngine;

namespace Framework.XRenderPipeline {
    [Serializable, VolumeComponentMenu("Post-Processing/Split Toning")]
    public sealed class SplitToning : VolumeComponent, IPostProcess {
        [Tooltip("The color to use for shadows.")]
        public ColorParameter shadows = new ColorParameter(Color.grey, false, false, true);

        [Tooltip("The color to use for highlights.")]
        public ColorParameter highlights = new ColorParameter(Color.grey, false, false, true);

        [Tooltip("Balance between the colors in the highlights and shadows.")]
        public ClampedFloatParameter balance = new ClampedFloatParameter(0f, -100f, 100f);

        public bool IsActive() => shadows != Color.grey || highlights != Color.grey;

        public bool IsOnChipMemoryCompatible() => true;
    }
}
