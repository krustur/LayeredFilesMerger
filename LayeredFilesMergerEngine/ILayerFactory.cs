using System.IO;
using Newtonsoft.Json;

namespace LayeredFilesMergerEngine
{
    public interface ILayerFactory
    {
        GetLayerResult GetLayer(string layerPath, string layerKey);
    }

    public class LayerFactory : ILayerFactory
    {
        private readonly ILayerDetailsService _layerDetailsService;
        private readonly ILayerNameInfoService _layerNameInfoService;

        public LayerFactory(ILayerDetailsService layerDetailsService, ILayerNameInfoService layerNameInfoService)
        {
            _layerDetailsService = layerDetailsService;
            _layerNameInfoService = layerNameInfoService;
        }

        public GetLayerResult GetLayer(string layerPath, string layerKey)
        {
            Layer layer;
            GetLayerResultType resultType;

            if (_layerDetailsService.Exists(layerPath, layerKey))
            {
                
                layer = _layerDetailsService.LoadDetails(layerPath, layerKey);

                layer.Name = _layerNameInfoService.GetName(layerKey);
                layer.Version = _layerNameInfoService.GetVersion(layerKey);
                resultType = GetLayerResultType.Old;
            }
            else
            {
                layer = new Layer
                {
                    Name = _layerNameInfoService.GetName(layerKey), 
                    Version = _layerNameInfoService.GetVersion(layerKey), 
                    Include = false,
                };
                _layerDetailsService.SaveDetails(layerPath, layerKey, layer);
               
                resultType = GetLayerResultType.New;
            }

            return new GetLayerResult
            {
                LayerKey = layerKey,
                Layer = layer,
                ResultType = resultType
            };
        }

    }

    public class GetLayerResult
    {
        public GetLayerResultType ResultType { get; set; }
        public string LayerKey { get; set; }
        public Layer Layer { get; set; }
    }

    public enum GetLayerResultType
    {
        Unknown = 0,
        Old = 1,
        New = 2,
    }


}