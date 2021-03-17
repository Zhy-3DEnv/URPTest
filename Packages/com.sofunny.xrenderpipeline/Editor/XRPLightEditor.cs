using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Framework.XRenderPipeline {
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(XRenderPipelineAsset))]
    class UniversalRenderPipelineLightEditor : LightEditor {
        AnimBool animSpotOptions = new AnimBool();
        AnimBool animPointOptions = new AnimBool();
        AnimBool animDirOptions = new AnimBool();
        AnimBool animAreaOptions = new AnimBool();
        AnimBool animRuntimeOptions = new AnimBool();
        AnimBool animShadowOptions = new AnimBool();
        AnimBool animShadowAngleOptions = new AnimBool();
        AnimBool animShadowRadiusOptions = new AnimBool();
        AnimBool animLightBounceIntensity = new AnimBool();

        class Styles {
            public readonly GUIContent SpotAngle = EditorGUIUtility.TrTextContent("Spot Angle", "Controls the angle in degrees at the base of a Spot light's cone.");

            public readonly GUIContent BakingWarning = EditorGUIUtility.TrTextContent("Light mode is currently overridden to Realtime mode. Enable Baked Global Illumination to use Mixed or Baked light modes.");
            public readonly GUIContent DisabledLightWarning = EditorGUIUtility.TrTextContent("Lighting has been disabled in at least one Scene view. Any changes applied to lights in the Scene will not be updated in these views until Lighting has been enabled again.");
            public readonly GUIContent SunSourceWarning = EditorGUIUtility.TrTextContent("This light is set as the current Sun Source, which requires a directional light. Go to the Lighting Window's Environment settings to edit the Sun Source.");

            public static readonly GUIContent ShadowRealtimeSettings = EditorGUIUtility.TrTextContent("Realtime Shadows", "Settings for realtime direct shadows.");
            public static readonly GUIContent ShadowStrength = EditorGUIUtility.TrTextContent("Strength", "Controls how dark the shadows cast by the light will be.");
            public static readonly GUIContent ShadowNearPlane = EditorGUIUtility.TrTextContent("Near Plane", "Controls the value for the near clip plane when rendering shadows. Currently clamped to 0.1 units or 1% of the lights range property, whichever is lower.");
            public static readonly GUIContent ShadowNormalBias = EditorGUIUtility.TrTextContent("Normal", "Controls the distance shadow caster vertices are offset along their normals when rendering shadow maps. Currently ignored for Point Lights.");

            public static GUIContent shadowBias = EditorGUIUtility.TrTextContent("Bias", "Select if the Bias should use the settings from the Pipeline Asset or Custom settings.");
            public static int[] optionDefaultValues = { 0, 1 };

            public static GUIContent[] displayedDefaultOptions =
            {
                new GUIContent("Custom"),
                new GUIContent("Use Pipeline Settings")
            };
        }

        static Styles s_Styles;

        public bool TypeIsSame { get { return !settings.lightType.hasMultipleDifferentValues; } }
        public bool ShadowTypeIsSame { get { return !settings.shadowsType.hasMultipleDifferentValues; } }
        public bool LightmappingTypeIsSame { get { return !settings.lightmapping.hasMultipleDifferentValues; } }
        public Light LightProperty { get { return target as Light; } }

        public bool SpotOptionsValue { get { return TypeIsSame && LightProperty.type == LightType.Spot; } }
        public bool PointOptionsValue { get { return TypeIsSame && LightProperty.type == LightType.Point; } }
        public bool DirOptionsValue { get { return TypeIsSame && LightProperty.type == LightType.Directional; } }
        public bool AreaOptionsValue { get { return TypeIsSame && (LightProperty.type == LightType.Rectangle || LightProperty.type == LightType.Disc); } }

        //  Area light shadows not supported
        public bool RuntimeOptionsValue { get { return TypeIsSame && (LightProperty.type != LightType.Rectangle && !settings.isCompletelyBaked); } }
        public bool BakedShadowRadius { get { return TypeIsSame && (LightProperty.type == LightType.Point || LightProperty.type == LightType.Spot) && settings.isBakedOrMixed; } }
        public bool BakedShadowAngle { get { return TypeIsSame && LightProperty.type == LightType.Directional && settings.isBakedOrMixed; } }
        public bool ShadowOptionsValue { get { return ShadowTypeIsSame && LightProperty.shadows != LightShadows.None; } }
#pragma warning disable 618
        public bool BakingWarningValue { get { return !UnityEditor.Lightmapping.bakedGI && LightmappingTypeIsSame && settings.isBakedOrMixed; } }
#pragma warning restore 618
        public bool ShowLightBounceIntensity { get { return true; } }

        public bool IsShadowEnabled { get { return settings.shadowsType.intValue != 0; } }

        XRPAdditionalLightData additionalLightData;
        SerializedObject additionalLightDataSO;

        SerializedProperty useAdditionalDataProp;


        protected override void OnEnable() {
            additionalLightData = LightProperty.gameObject.GetComponent<XRPAdditionalLightData>();
            settings.OnEnable();
            Init(additionalLightData);
            UpdateShowOptions(true);
        }

        void Init(XRPAdditionalLightData additionalLightData) {
            if (additionalLightData == null) {
                return;
            }
            additionalLightDataSO = new SerializedObject(additionalLightData);
            useAdditionalDataProp = additionalLightDataSO.FindProperty("usePipelineSettings");

            settings.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI() {
            if (s_Styles == null) {
                s_Styles = new Styles();
            }

            settings.Update();

            // Update AnimBool options. For properties changed they will be smoothly interpolated.
            UpdateShowOptions(false);

            settings.DrawLightType();

            Light light = target as Light;
            if (LightType.Directional != light.type && light == RenderSettings.sun) {
                EditorGUILayout.HelpBox(s_Styles.SunSourceWarning.text, MessageType.Warning);
            }

            EditorGUILayout.Space();

            // When we are switching between two light types that don't show the range (directional and area lights)
            // we want the fade group to stay hidden.
            using (var group = new EditorGUILayout.FadeGroupScope(1.0f - animDirOptions.faded))
                if (group.visible)
#if UNITY_2020_1_OR_NEWER
                    settings.DrawRange();
#else
                    settings.DrawRange(animAreaOptions.target);
#endif

            // Spot angle
            using (var group = new EditorGUILayout.FadeGroupScope(animSpotOptions.faded))
                if (group.visible) {
                    DrawSpotAngle();
                }

            // Area width & height
            using (var group = new EditorGUILayout.FadeGroupScope(animAreaOptions.faded))
                if (group.visible) {
                    settings.DrawArea();
                }

            settings.DrawColor();

            EditorGUILayout.Space();

            CheckLightmappingConsistency();
            using (var group = new EditorGUILayout.FadeGroupScope(1.0f - animAreaOptions.faded))
                if (group.visible) {
                    if (light.type != LightType.Disc) {
                        settings.DrawLightmapping();
                    }
                }

            settings.DrawIntensity();

            using (var group = new EditorGUILayout.FadeGroupScope(animLightBounceIntensity.faded))
                if (group.visible) {
                    settings.DrawBounceIntensity();
                }

            ShadowsGUI();

            settings.DrawRenderMode();
            settings.DrawCullingMask();

            EditorGUILayout.Space();

            if (SceneView.lastActiveSceneView != null) {
#if UNITY_2019_1_OR_NEWER
                var sceneLighting = SceneView.lastActiveSceneView.sceneLighting;
#else
                var sceneLighting = SceneView.lastActiveSceneView.sceneLighting;
#endif
                if (!sceneLighting) {
                    EditorGUILayout.HelpBox(s_Styles.DisabledLightWarning.text, MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void CheckLightmappingConsistency() {
            //Universal render-pipeline only supports baked area light, enforce it as this inspector is the universal one.
            if (settings.isAreaLightType && settings.lightmapping.intValue != (int)LightmapBakeType.Baked) {
                settings.lightmapping.intValue = (int)LightmapBakeType.Baked;
                serializedObject.ApplyModifiedProperties();
            }
        }

        void SetOptions(AnimBool animBool, bool initialize, bool targetValue) {
            if (initialize) {
                animBool.value = targetValue;
                animBool.valueChanged.AddListener(Repaint);
            } else {
                animBool.target = targetValue;
            }
        }

        void UpdateShowOptions(bool initialize) {
            SetOptions(animSpotOptions, initialize, SpotOptionsValue);
            SetOptions(animPointOptions, initialize, PointOptionsValue);
            SetOptions(animDirOptions, initialize, DirOptionsValue);
            SetOptions(animAreaOptions, initialize, AreaOptionsValue);
            SetOptions(animShadowOptions, initialize, ShadowOptionsValue);
            SetOptions(animRuntimeOptions, initialize, RuntimeOptionsValue);
            SetOptions(animShadowAngleOptions, initialize, BakedShadowAngle);
            SetOptions(animShadowRadiusOptions, initialize, BakedShadowRadius);
            SetOptions(animLightBounceIntensity, initialize, ShowLightBounceIntensity);
        }

        void DrawSpotAngle() {
            settings.DrawInnerAndOuterSpotAngle();
        }

        void DrawAdditionalShadowData() {
            bool hasChanged = false;
            int selectedUseAdditionalData;

            if (additionalLightDataSO == null) {
                selectedUseAdditionalData = 1;
            } else {
                additionalLightDataSO.Update();
                selectedUseAdditionalData = !additionalLightData.UsePipelineSettings ? 0 : 1;
            }

            Rect controlRectAdditionalData = EditorGUILayout.GetControlRect(true);
            if (additionalLightDataSO != null) {
                EditorGUI.BeginProperty(controlRectAdditionalData, Styles.shadowBias, useAdditionalDataProp);
            }
            EditorGUI.BeginChangeCheck();

            selectedUseAdditionalData = EditorGUI.IntPopup(controlRectAdditionalData, Styles.shadowBias, selectedUseAdditionalData, Styles.displayedDefaultOptions, Styles.optionDefaultValues);
            if (EditorGUI.EndChangeCheck()) {
                hasChanged = true;
            }
            if (additionalLightDataSO != null) {
                EditorGUI.EndProperty();
            }

            if (selectedUseAdditionalData != 1 && additionalLightDataSO != null) {
                EditorGUI.indentLevel++;
                EditorGUILayout.Slider(settings.shadowsBias, 0f, 10f, "Depth");
                EditorGUILayout.Slider(settings.shadowsNormalBias, 0f, 10f, Styles.ShadowNormalBias);
                EditorGUI.indentLevel--;

                additionalLightDataSO.ApplyModifiedProperties();
            }

            if (hasChanged) {
                if (additionalLightDataSO == null) {
                    LightProperty.gameObject.AddComponent<XRPAdditionalLightData>();
                    additionalLightData = LightProperty.gameObject.GetComponent<XRPAdditionalLightData>();

                    var asset = XRenderPipeline.PipelineAsset;
                    settings.shadowsBias.floatValue = asset.shadowDepthBias;
                    settings.shadowsNormalBias.floatValue = asset.shadowNormalBias;

                    Init(additionalLightData);
                }

                useAdditionalDataProp.intValue = selectedUseAdditionalData;
                additionalLightDataSO.ApplyModifiedProperties();
            }
        }

        void ShadowsGUI() {
            // Shadows drop-down. Area lights can only be baked and always have shadows.
            float show = 1.0f - animAreaOptions.faded;

            settings.DrawShadowsType();

            EditorGUI.indentLevel += 1;
            show *= animShadowOptions.faded;
            // Baked Shadow radius
            using (var group = new EditorGUILayout.FadeGroupScope(show * animShadowRadiusOptions.faded))
                if (group.visible)
                    settings.DrawBakedShadowRadius();

            // Baked Shadow angle
            using (var group = new EditorGUILayout.FadeGroupScope(show * animShadowAngleOptions.faded))
                if (group.visible) {
                    settings.DrawBakedShadowAngle();
                }

            // Runtime shadows - shadow strength, resolution and near plane offset
            // Bias is handled differently in UniversalRP
            using (var group = new EditorGUILayout.FadeGroupScope(show * animRuntimeOptions.faded)) {
                if (group.visible) {
                    EditorGUILayout.LabelField(Styles.ShadowRealtimeSettings);
                    EditorGUI.indentLevel += 1;
                    EditorGUILayout.Slider(settings.shadowsStrength, 0f, 1f, Styles.ShadowStrength);

                    DrawAdditionalShadowData();

                    // this min bound should match the calculation in SharedLightData::GetNearPlaneMinBound()
                    float nearPlaneMinBound = Mathf.Min(0.01f * settings.range.floatValue, 0.1f);
                    EditorGUILayout.Slider(settings.shadowsNearPlane, nearPlaneMinBound, 10.0f, Styles.ShadowNearPlane);
                    EditorGUI.indentLevel -= 1;
                }
            }

            EditorGUI.indentLevel -= 1;

            if (BakingWarningValue) {
                EditorGUILayout.HelpBox(s_Styles.BakingWarning.text, MessageType.Warning);
            }

            EditorGUILayout.Space();
        }

        protected override void OnSceneGUI() {
            if (!(GraphicsSettings.currentRenderPipeline is XRenderPipelineAsset)) {
                return;
            }

            Light light = target as Light;

            switch (light.type) {
                case LightType.Spot:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one))) {
                        CoreLightEditorUtilities.DrawSpotLightGizmo(light);
                    }
                    break;

                case LightType.Point:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, Quaternion.identity, Vector3.one))) {
                        CoreLightEditorUtilities.DrawPointLightGizmo(light);
                    }
                    break;

                case LightType.Rectangle:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one))) {
                        CoreLightEditorUtilities.DrawRectangleLightGizmo(light);
                    }
                    break;

                case LightType.Disc:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one))) {
                        CoreLightEditorUtilities.DrawDiscLightGizmo(light);
                    }
                    break;

                case LightType.Directional:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one))) {
                        CoreLightEditorUtilities.DrawDirectionalLightGizmo(light);
                    }
                    break;

                default:
                    base.OnSceneGUI();
                    break;
            }
        }
    }
}
