using System.IO;
using Newtonsoft.Json;
using Serilog;

namespace LayeredFilesMergerEngine
{
    public interface ILayerDetailsService
    {
        bool Exists(string layerPath, string layerKey);
        Layer LoadDetails(string layerPath, string layerKey);
        void SaveDetails(string layerPath, string layerKey, Layer layer);
    }

    public class LayerDetailsService : ILayerDetailsService
    {
        private readonly ILogger _logger;

        public LayerDetailsService(ILogger logger)
        {
            _logger = logger;
        }

        public bool Exists(string layerPath, string layerKey)
        {
            var layerJsonPath = GetPath(layerPath, layerKey);
            return File.Exists(layerJsonPath);

        }

        public Layer LoadDetails(string layerPath, string layerKey)
        {
            var layerJsonPath = GetPath(layerPath, layerKey);
            var json = File.ReadAllText(layerJsonPath);

            _logger.Information($"LayerDetailsService: LoadDetails {layerJsonPath} [{json}]");

            var layer = JsonConvert.DeserializeObject<Layer>(json);
            //layer.IsDirty = false;
            return layer;
        }

        public void SaveDetails(string layerPath, string layerKey, Layer layer)
        {
            var layerJsonPath = GetPath(layerPath, layerKey);
            var json = JsonConvert.SerializeObject(layer);
            File.WriteAllText(layerJsonPath, json);
            //layer.IsDirty = false;
            _logger.Information($"LayerDetailsService: SaveDetails {layerJsonPath} [{json}]");
        }

        private string GetPath(string layerPath, string layerName)
        {
            return Path.Combine(layerPath, layerName, "layerDetails.json");
        }
    }
}