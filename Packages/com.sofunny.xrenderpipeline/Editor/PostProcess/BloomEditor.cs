using System.Linq;
using UnityEngine.Rendering;
using UnityEditor;

namespace Framework.XRenderPipeline {
    [VolumeComponentEditor(typeof(Bloom))]
    sealed class BloomEditor : VolumeComponentEditor {
        SerializedDataParameter m_Threshold;
        SerializedDataParameter m_Intensity;
        SerializedDataParameter m_Scatter;
        SerializedDataParameter m_Clamp;
        SerializedDataParameter m_Tint;
        SerializedDataParameter m_HighQualityUpsampling;
        SerializedDataParameter m_PrefilterBlur;
        SerializedDataParameter m_BloomMipCnt;

        public override void OnEnable() {
            var o = new PropertyFetcher<Bloom>(serializedObject);

            m_Threshold = Unpack(o.Find(x => x.threshold));
            m_Intensity = Unpack(o.Find(x => x.intensity));
            m_Scatter = Unpack(o.Find(x => x.scatter));
            m_Clamp = Unpack(o.Find(x => x.clamp));
            m_Tint = Unpack(o.Find(x => x.tint));
            m_HighQualityUpsampling= Unpack(o.Find(x => x.highQualityUpsampling));
            m_PrefilterBlur = Unpack(o.Find(x => x.prefilterBlur));
            m_BloomMipCnt = Unpack(o.Find(x => x.bloomMipCount));
        }

        public override void OnInspectorGUI() {
            EditorGUILayout.LabelField("Bloom", EditorStyles.miniLabel);

            PropertyField(m_Threshold);
            PropertyField(m_Intensity);
            PropertyField(m_Scatter);
            PropertyField(m_Clamp);
            PropertyField(m_Tint);
            PropertyField(m_HighQualityUpsampling);
            PropertyField(m_PrefilterBlur);
            PropertyField(m_BloomMipCnt);
        }
    }
}
