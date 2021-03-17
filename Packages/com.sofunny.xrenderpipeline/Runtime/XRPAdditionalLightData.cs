using System;
using UnityEngine;

namespace Framework.XRenderPipeline {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class XRPAdditionalLightData : MonoBehaviour {
        [Tooltip("Controls the usage of pipeline settings.")]
        [SerializeField] bool usePipelineSettings = true;

        public bool UsePipelineSettings {
            get { return usePipelineSettings; }
            set { usePipelineSettings = value; }
        }
    }
}
