using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.XRenderPipeline {

    public enum LightUnit {
        LUMEN, // for punctual lights (point, spot)
        LUX, // for directional light
        CANDELA // for punctual lights (point, spot)
    }

    public class PhysicalLightData : MonoBehaviour {
        public LightUnit lightUnit = LightUnit.LUX;
        // TODO: add color temperature mode
    }
}
