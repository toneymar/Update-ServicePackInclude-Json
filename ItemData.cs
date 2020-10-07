using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateSPInclude
{
    public class ItemData
    {
        public int version { get; set; }
        public DateTime changeDate { get; set; }
        public int size { get; set; }
        public string hashValue { get; set; }
        public int encoding { get; set; }
        public string path { get; set; }
        public string content { get; set; }
        public Contentmetadata contentMetadata { get; set; }
        public string url { get; set; }
        public _Links _links { get; set; }

        public class Contentmetadata
        {
            public int encoding { get; set; }
            public string contentType { get; set; }
            public string fileName { get; set; }
            public string extension { get; set; }
            public string vsLink { get; set; }
        }

        public class _Links
        {
            public Self self { get; set; }
        }

        public class Self
        {
            public string href { get; set; }
        }
    }
}
