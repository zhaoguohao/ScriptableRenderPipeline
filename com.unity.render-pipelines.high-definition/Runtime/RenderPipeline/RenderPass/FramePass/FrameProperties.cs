namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public struct FrameProperties
    {
        public readonly Vector2Int outputSize;
        public readonly Matrix4x4 cameraToWorldMatrixRHS;
        public readonly Matrix4x4 projectionMatrix;

        public FrameProperties(Vector2Int outputSize, Matrix4x4 cameraToWorldMatrixRhs, Matrix4x4 projectionMatrix)
        {
            this.outputSize = outputSize;
            cameraToWorldMatrixRHS = cameraToWorldMatrixRhs;
            this.projectionMatrix = projectionMatrix;
        }

        public static FrameProperties From(HDCamera hdCamera)
            => new FrameProperties(
                new Vector2Int(hdCamera.actualWidth, hdCamera.actualHeight),
                hdCamera.camera.cameraToWorldMatrix,
                hdCamera.projMatrix
            );
    }
}
