using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX.Block;
using UnityEngine;
using UnityEngine.Experimental.VFX;

namespace UnityEditor.VFX
{
    [VFXInfo]
    class VFXQuadOutput : VFXAbstractParticleOutput
    {
        public enum PrimitiveType
        {
            Triangle,
            Quad,
            Octagon,
        }

        [VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField]
        protected PrimitiveType primitiveType = PrimitiveType.Quad;

        //[VFXSetting] // tmp dont expose as settings atm
        public bool useGeometryShader = false;

        public override string name
        {
            get
            {
                switch (primitiveType)
                {
                    case PrimitiveType.Triangle: return "Triangle Output";
                    case PrimitiveType.Quad: return "Quad Output";
                    case PrimitiveType.Octagon: return "Octagon Output";
                    default: throw new NotImplementedException();
                }
            }
        }
        public override string codeGeneratorTemplate { get { return RenderPipeTemplate("VFXParticleQuad"); } }
        public override VFXTaskType taskType
        {
            get
            {
                if (useGeometryShader)
                    return VFXTaskType.ParticlePointOutput;

                switch (primitiveType)
                {
                    case PrimitiveType.Triangle:    return VFXTaskType.ParticleTriangleOutput;
                    case PrimitiveType.Quad:        return VFXTaskType.ParticleQuadOutput;
                    case PrimitiveType.Octagon:     return VFXTaskType.ParticleOctagonOutput;
                    default:                        throw new NotImplementedException();
                }
            }
        }
        public override bool supportsUV { get { return true; } }

        public override IEnumerable<string> additionalDefines
        {
            get
            {
                foreach (var def in base.additionalDefines)
                    yield return def;

                if (useGeometryShader)
                    yield return "USE_GEOMETRY_SHADER";

                switch (primitiveType)
                {
                    case PrimitiveType.Triangle:    yield return "VFX_PRIMITIVE_TRIANGLE"; break;
                    case PrimitiveType.Quad:        yield return "VFX_PRIMITIVE_QUAD"; break;
                    case PrimitiveType.Octagon:     yield return "VFX_PRIMITIVE_OCTAGON"; break;
                }
            }
        }

        public override IEnumerable<VFXAttributeInfo> attributes
        {
            get
            {
                yield return new VFXAttributeInfo(VFXAttribute.Position, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Color, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Alpha, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Alive, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisZ, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleZ, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotZ, VFXAttributeMode.Read);

                yield return new VFXAttributeInfo(VFXAttribute.Size, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleZ, VFXAttributeMode.Read);

                if (usesFlipbook)
                    yield return new VFXAttributeInfo(VFXAttribute.TexIndex, VFXAttributeMode.Read);
            }
        }

        public class OctagonInputProperties
        {
            [Range(0, 1)]
            public float cropFactor = 0.5f * (1.0f - Mathf.Tan(Mathf.PI / 8.0f)); // regular octagon
        }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get
            {
                var properties = base.inputProperties;
                if (primitiveType == PrimitiveType.Octagon)
                    properties = properties.Concat(PropertiesFromType("OctagonInputProperties"));
                return properties;
            }
        }

        protected override IEnumerable<VFXNamedExpression> CollectGPUExpressions(IEnumerable<VFXNamedExpression> slotExpressions)
        {
            foreach (var exp in base.CollectGPUExpressions(slotExpressions))
                yield return exp;

            yield return slotExpressions.First(o => o.name == "mainTexture");
            if (primitiveType == PrimitiveType.Octagon)
                yield return slotExpressions.First(o => o.name == "cropFactor");
        }

        public class InputProperties
        {
            public Texture2D mainTexture = VFXResources.defaultResources.particleTexture;
        }
    }
}
