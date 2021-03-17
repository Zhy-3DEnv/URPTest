using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.XRenderPipeline {

    public class PhysicalCameraData : MonoBehaviour {
        public float aperture = 16.0f;
        public float shutterSpeed = 1.0f / 125.0f;
        public float sensitivity = 100.0f;

        public float GetExposure() {
            float e = (aperture * aperture) / shutterSpeed * 100.0f / sensitivity;
            return 1.0f / (1.2f * e);
        }
    }
}

