using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public enum MaterialProperty
    {
        Albedo,
        Specular,
        // ...
    }

    public enum ObjectProperty
    {
        MotionVector,
        // ...
    }

    public unsafe struct RenderPassSettings
    {
        public static RenderPassSettings @default = new RenderPassSettings { };

        public RenderPassSettings(RenderPassSettings other)
        {

        }

        /// <summary>
        /// State the property to render. In case of several SetFullscreenOutput chained call, only last will be used.
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        public ref RenderPassSettings SetFullscreenOutput(MaterialProperty mat)
        {
            return ref *ThisPtr;
        }

        /// <summary>
        /// State the property to render. In case of several SetFullscreenOutput chained call, only last will be used.
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        public ref RenderPassSettings SetFullscreenOutput(ObjectProperty mat)
        {
            return ref *ThisPtr;
        }

        RenderPassSettings* ThisPtr
        {
            get
            {
                fixed (RenderPassSettings* pThis = &this)
                    return pThis;
            }
        }
    }
}
