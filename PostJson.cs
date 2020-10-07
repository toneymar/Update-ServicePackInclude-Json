using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateSPInclude
{
    class PostJson
    {
        public List<Change> changes { get; set; }
        public string comment { get; set; }

        public class Change
        {
            public Item item { get; set; }
            public string changeType { get; set; }
            public Newcontent newContent { get; set; }
        }

        public class Item
        {
            public int version { get; set; }
            public string path { get; set; }
            public Contentmetadata contentMetadata { get; set; }
        }

        public class Contentmetadata
        {
            public int encoding { get; set; }
            public string contentType { get; set; }
        }

        public class Newcontent
        {
            public string content { get; set; }
        }
    }
}
