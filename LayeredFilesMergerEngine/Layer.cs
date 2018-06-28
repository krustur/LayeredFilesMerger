using Newtonsoft.Json;

namespace LayeredFilesMergerEngine
{
    public class Layer
    {
        [JsonIgnore]
        public string Name { get; set; }
        [JsonIgnore]
        public string Version { get; set; }

        //[JsonIgnore]
        //public bool IsDirty { get; set; }

        public bool Include { get; set; }
        public ulong Priority { get; set; }
        //public IList<EnFile> EnFiles { get; set; }
    }

}
