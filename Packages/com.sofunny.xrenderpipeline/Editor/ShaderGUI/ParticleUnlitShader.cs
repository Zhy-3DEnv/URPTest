using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Framework.XRenderPipeline {
    using SurfaceType = ShaderGUICommon.SurfaceType;
    using CullMode = ShaderGUICommon.CullMode;

    public class ParticleUnlitShader : ShaderGUI {

        bool firstTimeApply = true;
        const int queueOffsetRange = 50;

        // for particles, we should not use straight alpha
        enum BlendMode {
            Premultiply = 0,
            Additive,
            Multiply
        }

        static class Styles {
            public static readonly GUIContent surfaceType = new GUIContent("Surface Type", "Surface Type");
            public static readonly GUIContent blendMode = new GUIContent("Blend Mode", "Blend Mode");
            public static readonly GUIContent cullMode = new GUIContent("Cull Mode", "Cull Mode");
            public static readonly GUIContent baseMap = new GUIContent("Base Map", "Base Map");
            public static readonly GUIContent distortionMap = new GUIContent("Distortion Map", "Distortion Map, XY: screen space distortion vector");
            public static readonly GUIContent softParticlesEnabled = new GUIContent("Soft Particles", "Makes particles fade out when they get close to intersecting with the surface of other geometry in the depth buffer.");
            public static readonly GUIContent softParticlesNearFadeDistanceText = new GUIContent("Near", "The distance from the other surface where the particle is completely transparent.");
            public static readonly GUIContent softParticlesFarFadeDistanceText = new GUIContent("Far", "The distance from the other surface where the particle is completely opaque.");
            public static readonly GUIContent cameraFadingEnabled = new GUIContent("Camera Fading", "Makes particles fade out when they get close to the camera.");
            public static readonly GUIContent cameraNearFadeDistance = new GUIContent("Near", "The distance from the camera where the particle is completely transparent.");
            public static readonly GUIContent cameraFarFadeDistance = new GUIContent("Far", "The distance from the camera where the particle is completely opaque.");
            public static readonly GUIContent distortionEnabled = new GUIContent("Distortion", "Creates a distortion effect by making particles perform refraction with the objects drawn before them.");
            public static readonly GUIContent distortionStrength = new GUIContent("Strength", "Controls how much the Particle distorts the background. ");
            public static readonly GUIContent distortionBlend = new GUIContent("Blend", "Controls how visible the distortion effect is. At 0, there’s no visible distortion. At 1, only the distortion effect is visible, not the background.");
            public static readonly GUIContent queueSlider = new GUIContent("Priority", "Determines the chronological rendering order for a Material. High values are rendered first.");
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties) {
            var material = (Material)materialEditor.target;
            if (firstTimeApply) {
                OnOpenGUI(material, materialEditor);
                firstTimeApply = false;
            }

            EditorGUI.BeginChangeCheck();

            // surface options
            var surfaceTypeProp = properties.First(x => x.name == "_SurfaceType");
            ShaderGUICommon.DoPopup(Styles.surfaceType, surfaceTypeProp, Enum.GetNames(typeof(SurfaceType)), materialEditor);
            if ((SurfaceType)material.GetFloat("_SurfaceType") == SurfaceType.Transparent) {
                var blendModeProp = properties.First(x => x.name == "_BlendMode");
                ShaderGUICommon.DoPopup(Styles.blendMode, blendModeProp, Enum.GetNames(typeof(BlendMode)), materialEditor);
            }
            EditorGUI.BeginChangeCheck();
            var cullModeProp = properties.First(x => x.name == "_Cull");
            EditorGUI.showMixedValue = cullModeProp.hasMixedValue;
            var cullMode = (CullMode)cullModeProp.floatValue;
            cullMode = (CullMode)EditorGUILayout.EnumPopup(Styles.cullMode, cullMode);
            if (EditorGUI.EndChangeCheck()) {
                materialEditor.RegisterPropertyChangeUndo(Styles.cullMode.text);
                cullModeProp.floatValue = (float)cullMode;
                material.doubleSidedGI = (CullMode)cullModeProp.floatValue != CullMode.Back;
            }

            EditorGUILayout.Space();

            // surface inputs
            var baseColorProp = properties.First(x => x.name == "_BaseColor");
            var baseMapProp = properties.First(x => x.name == "_BaseMap");
            materialEditor.TexturePropertySingleLine(Styles.baseMap, baseMapProp, baseColorProp);

            bool hasZWrite = (material.GetInt("_ZWrite") != 0);
            if(!hasZWrite) {
                // Soft Particles
                var softParticlesEnabledProp = properties.First(x => x.name == "_SoftParticlesEnabled");
                EditorGUI.showMixedValue = softParticlesEnabledProp.hasMixedValue;
                var softParticlesEnabled = softParticlesEnabledProp.floatValue;

                EditorGUI.BeginChangeCheck();
                softParticlesEnabled = EditorGUILayout.Toggle(Styles.softParticlesEnabled, softParticlesEnabled != 0.0f) ? 1.0f : 0.0f;
                if (EditorGUI.EndChangeCheck()) {
                    materialEditor.RegisterPropertyChangeUndo("Soft Particles Enabled");
                    softParticlesEnabledProp.floatValue = softParticlesEnabled;
                }
                if (softParticlesEnabled >= 0.5f) {
                    EditorGUI.indentLevel++;
                    var softParticlesNearFadeDistanceProp = properties.First(x => x.name == "_SoftParticlesNearFadeDistance");
                    var softParticlesFarFadeDistanceProp = properties.First(x => x.name == "_SoftParticlesFarFadeDistance");
                    ShaderGUICommon.TwoFloatSingleLine(new GUIContent("Surface Fade"),
                        softParticlesNearFadeDistanceProp,
                        Styles.softParticlesNearFadeDistanceText,
                        softParticlesFarFadeDistanceProp,
                        Styles.softParticlesFarFadeDistanceText,
                        materialEditor);
                    EditorGUI.indentLevel--;
                }

                // Camera Fading
                var cameraFadingEnabledProp = properties.First(x => x.name == "_CameraFadingEnabled");
                EditorGUI.showMixedValue = cameraFadingEnabledProp.hasMixedValue;
                var cameraFadingEnabled = cameraFadingEnabledProp.floatValue;

                EditorGUI.BeginChangeCheck();
                cameraFadingEnabled = EditorGUILayout.Toggle(Styles.cameraFadingEnabled, cameraFadingEnabled != 0.0f) ? 1.0f : 0.0f;
                if (EditorGUI.EndChangeCheck()) {
                    materialEditor.RegisterPropertyChangeUndo("Camera Fading Enabled");
                    cameraFadingEnabledProp.floatValue = cameraFadingEnabled;
                }

                if (cameraFadingEnabled >= 0.5f) {
                    var cameraNearFadeDistanceProp = properties.First(x => x.name == "_CameraNearFadeDistance");
                    var cameraFarFadeDistanceProp = properties.First(x => x.name == "_CameraFarFadeDistance");
                    EditorGUI.indentLevel++;
                    ShaderGUICommon.TwoFloatSingleLine(new GUIContent("Distance"),
                        cameraNearFadeDistanceProp,
                        Styles.cameraNearFadeDistance,
                        cameraFarFadeDistanceProp,
                        Styles.cameraFarFadeDistance,
                        materialEditor);
                    EditorGUI.indentLevel--;
                }

                // Distortion
                var distortionEnabledProp = properties.First(x => x.name == "_DistortionEnabled");
                EditorGUI.showMixedValue = distortionEnabledProp.hasMixedValue;
                var distortionEnabled = distortionEnabledProp.floatValue;

                EditorGUI.BeginChangeCheck();
                distortionEnabled = EditorGUILayout.Toggle(Styles.distortionEnabled, distortionEnabled != 0.0f) ? 1.0f : 0.0f;
                if (EditorGUI.EndChangeCheck()) {
                    materialEditor.RegisterPropertyChangeUndo("Distortion Enabled");
                    distortionEnabledProp.floatValue = distortionEnabled;
                }

                if (distortionEnabled >= 0.5f) {
                    EditorGUI.indentLevel++;
                    var distortionMapProp = properties.First(x => x.name == "_DistortionMap");
                    materialEditor.TexturePropertySingleLine(Styles.distortionMap, distortionMapProp);
                    var distortionStrengthProp = properties.First(x => x.name == "_DistortionStrength");
                    var distortionBlendProp = properties.First(x => x.name == "_DistortionBlend");
                    materialEditor.ShaderProperty(distortionStrengthProp, Styles.distortionStrength);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = distortionStrengthProp.hasMixedValue;
                    var blend = EditorGUILayout.Slider(Styles.distortionBlend, distortionBlendProp.floatValue, 0f, 1f);
                    if(EditorGUI.EndChangeCheck()) {
                        distortionBlendProp.floatValue = blend;
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();

            // advanced options
            var queueOffsetProp = properties.First(x => x.name == "_QueueOffset");
            if (queueOffsetProp != null) {
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = queueOffsetProp.hasMixedValue;
                var queue = EditorGUILayout.IntSlider(Styles.queueSlider, (int)queueOffsetProp.floatValue, -queueOffsetRange, queueOffsetRange);
                if (EditorGUI.EndChangeCheck())
                    queueOffsetProp.floatValue = queue;
                EditorGUI.showMixedValue = false;
            }

            if (EditorGUI.EndChangeCheck()) {
                foreach (var obj in materialEditor.targets) {
                    SetupMaterialBlendMode((Material)obj);
                    SetupMaterialKeywords((Material)obj);
                }
            }
        }

        public virtual void OnOpenGUI(Material material, MaterialEditor materialEditor) {
            foreach (var obj in materialEditor.targets) {
                SetupMaterialBlendMode((Material)obj);
                SetupMaterialKeywords((Material)obj);
            }
        }

        public static void SetupMaterialBlendMode(Material material) {
            var queueOffset = 0;
            if (material.HasProperty("_QueueOffset")) {
                queueOffset = queueOffsetRange - (int)material.GetFloat("_QueueOffset");
            }
            SurfaceType surfaceType = (SurfaceType)material.GetFloat("_SurfaceType");
            if (surfaceType == SurfaceType.Opaque) {
                material.renderQueue = (int)RenderQueue.Geometry + queueOffset;
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
            } else {
                BlendMode blendMode = (BlendMode)material.GetFloat("_BlendMode");
                var queue = (int)RenderQueue.Transparent;

                // Specific Transparent Mode Settings
                switch (blendMode) {
                    case BlendMode.Premultiply:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        break;
                    case BlendMode.Additive:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        break;
                    case BlendMode.Multiply:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        break;
                }
                // General Transparent Material Settings
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_ZWrite", 0);
                material.renderQueue = queue + queueOffset;
            }
        }

        public static void SetupMaterialKeywords(Material material) {
            bool isTransparent = material.GetTag("RenderType", false) == "Transparent";
            // Soft particles
            var useSoftParticles = false;
            bool hasZWrite = (material.GetInt("_ZWrite") != 0);
            if (material.HasProperty("_SoftParticlesEnabled")) {
                useSoftParticles = (material.GetFloat("_SoftParticlesEnabled") > 0.0f && isTransparent);
                if (useSoftParticles) {
                    var softParticlesNearFadeDistance = material.GetFloat("_SoftParticlesNearFadeDistance");
                    var softParticlesFarFadeDistance = material.GetFloat("_SoftParticlesFarFadeDistance");
                    // clamp values
                    if (softParticlesNearFadeDistance < 0.0f) {
                        softParticlesNearFadeDistance = 0.0f;
                        material.SetFloat("_SoftParticlesNearFadeDistance", 0.0f);
                    }

                    if (softParticlesFarFadeDistance < 0.0f) {
                        softParticlesFarFadeDistance = 0.0f;
                        material.SetFloat("_SoftParticlesFarFadeDistance", 0.0f);
                    }
                    // set keywords
                    material.SetVector("_SoftParticleFadeParams",
                        new Vector4(softParticlesNearFadeDistance,
                            1.0f / (softParticlesFarFadeDistance - softParticlesNearFadeDistance), 0.0f, 0.0f));
                } else {
                    material.SetVector("_SoftParticleFadeParams", new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                }

                if (useSoftParticles) {
                    material.EnableKeyword("_SOFTPARTICLES_ON");
                } else {
                    material.DisableKeyword("_SOFTPARTICLES_ON");
                }
            }
            // Camera fading
            var useCameraFading = false;
            if (material.HasProperty("_CameraFadingEnabled") && isTransparent) {
                useCameraFading = (material.GetFloat("_CameraFadingEnabled") > 0.0f);
                if (useCameraFading) {
                    var cameraNearFadeDistance = material.GetFloat("_CameraNearFadeDistance");
                    var cameraFarFadeDistance = material.GetFloat("_CameraFarFadeDistance");
                    // clamp values
                    if (cameraNearFadeDistance < 0.0f) {
                        cameraNearFadeDistance = 0.0f;
                        material.SetFloat("_CameraNearFadeDistance", 0.0f);
                    }
                    if (cameraFarFadeDistance < 0.0f) {
                        cameraFarFadeDistance = 0.0f;
                        material.SetFloat("_CameraFarFadeDistance", 0.0f);
                    }
                    material.SetVector("_CameraFadeParams", new Vector4(cameraNearFadeDistance, 1.0f / (cameraFarFadeDistance - cameraNearFadeDistance), 0.0f, 0.0f));
                } else {
                    material.SetVector("_CameraFadeParams", new Vector4(0.0f, Mathf.Infinity, 0.0f, 0.0f));
                }
            }
            // Distortion
            if (material.HasProperty("_DistortionEnabled")) {
                var useDistortion = (material.GetFloat("_DistortionEnabled") > 0.0f) && isTransparent;
                if (useDistortion) {
                    material.EnableKeyword("_DISTORTION_ON");
                    material.SetFloat("_DistortionStrengthScaled", material.GetFloat("_DistortionStrength") * 0.1f);
                } else {
                    material.DisableKeyword("_DISTORTION_ON");
                }
            }
            bool useFading = (useSoftParticles || useCameraFading) && !hasZWrite;
            if (useFading) {
                material.EnableKeyword("_FADING_ON");
            } else {
                material.DisableKeyword("_FADING_ON");
            }
        }

    }

}
