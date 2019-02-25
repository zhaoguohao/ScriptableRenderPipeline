using System.Collections.Generic;

namespace UnityEngine.Experimental.VoxelizedShadowMaps
{
    public class VxShadowMapsManager
    {
        private List<DirectionalVxShadowMap> _dirVxShadowMapList   = new List<DirectionalVxShadowMap>();
        private List<PointVxShadowMap>       _pointVxShadowMapList = new List<PointVxShadowMap>();
        private List<SpotVxShadowMap>        _spotVxShadowMapList  = new List<SpotVxShadowMap>();

        public VxShadowMapsManager()
        {
            var dirVxShadowMaps   = Object.FindObjectsOfType<DirectionalVxShadowMap>();
            var pointVxShadowMaps = Object.FindObjectsOfType<PointVxShadowMap>();
            var spotVxShadowMaps  = Object.FindObjectsOfType<SpotVxShadowMap>();

            foreach (var vxsm in dirVxShadowMaps)
                _dirVxShadowMapList.Add(vxsm);
            foreach (var vxsm in pointVxShadowMaps)
                _pointVxShadowMapList.Add(vxsm);
            foreach (var vxsm in spotVxShadowMaps)
                _spotVxShadowMapList.Add(vxsm);
        }

        public void Cleanup()
        {
            _dirVxShadowMapList.Clear();
            _pointVxShadowMapList.Clear();
            _spotVxShadowMapList.Clear();
        }

        public DirectionalVxShadowMap MainDirVxShadowMap { get { return _dirVxShadowMapList[0]; } }
    }
}
