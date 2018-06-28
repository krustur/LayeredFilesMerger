using System;

namespace LayeredFilesMergerEngine
{
    public class FileSysDto
    {
        public string Name { get; set; }

        public override string ToString()
        {
            return $"{nameof(Name)}: {Name}, {nameof(IsDirectory)}: {IsDirectory}";
        }

        public string Extension { get; set; }
        public bool IsDirectory { get; set; }
        public long? Size { get; set; }
        public DateTime Modified { get; set; }
        public string FullPath { get; set; }
        public string FileSystemPath { get; set; }
    }
}
