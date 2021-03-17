using System;

namespace Framework.XRenderPipeline {

    [Flags]
    public enum ClearFlag {
        None = 0,
        Color = 1 << 0,
        Depth = 1 << 1,
        All = Depth | Color
    }

    public enum DepthBits {
        None = 0,
        Depth8 = 8,
        Depth16 = 16,
        Depth24 = 24,
        Depth32 = 32
    }

    public enum MSAASamples {
        None = 1,
        MSAA2x = 2,
        MSAA4x = 4,
        MSAA8x = 8
    }
}


