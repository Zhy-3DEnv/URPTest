using System;
using System.Linq;
using UnityEngine;

namespace Framework.XRenderPipeline {

    public enum ShaderPathID {
        Lit,
        SimpleLit,
        Unlit,
        ParticleUnlit,
        Count
    }

    public static class ShaderUtils {
        static readonly string[] s_ShaderPaths = {
            "XRenderPipeline/Lit",
            "XRenderPipeline/SimpleLit",
            "XRenderPipeline/Unlit",
            "XRenderPipeline/ParticleUnlit",
        };

        public static string GetShaderPath(ShaderPathID id) {
            int idx = (int)id;
            if (idx < 0 || idx >= (int)ShaderPathID.Count) {
                Debug.LogError("Cannot find shader.");
                return "";
            }
            return s_ShaderPaths[idx];
        }

        public static ShaderPathID GetEnumFromPath(string path) {
            int idx = Array.FindIndex(s_ShaderPaths, pt => pt == path);
            return (ShaderPathID)idx;
        }
    }

}
