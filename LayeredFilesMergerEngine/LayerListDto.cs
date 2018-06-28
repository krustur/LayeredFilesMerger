namespace LayeredFilesMergerEngine
{
    public class LayerListDto
    {
        public override string ToString()
        {
            return $"[{Include}] [{Priority}] {Name} {Version}";
        }

        public string Key { get; set; }
        public bool Include { get; set; }
        public ulong Priority { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
    }
}
