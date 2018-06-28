using System;
using System.Linq;

namespace LayeredFilesMergerEngine
{
    public interface ILayerNameInfoService
    {
        string GetName(string layerKey);
        string GetVersion(string layerKey);
    }

    public class LayerNameInfoService : ILayerNameInfoService
    {
        public string GetName(string layerKey)
        {
            if (layerKey.Contains('_'))
            {
                var parts = layerKey.Split('_');
                if (parts.Length != 2)
                {
                    throw new Exception("Please use a single underscore char in layer folder names only");
                }
                return parts[0];
            }
            return layerKey;
        }

        public string GetVersion(string layerKey)
        {
            if (layerKey.Contains('_'))
            {
                var parts = layerKey.Split('_');
                if (parts.Length != 2)
                {
                    throw new Exception("Please use a single underscore char in layer folder names only");
                }
                return parts[parts.Length-1];
            }
            return "-";
        }
    }
}
