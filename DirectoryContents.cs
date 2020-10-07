using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateSPInclude
{
    public class DirectoryContents
    {
        public Value[] value { get; set; }
        public int count { get; set; }

        public class Value
        {
            public int version { get; set; }
            public DateTime changeDate { get; set; }
            public int encoding { get; set; }
            public string path { get; set; }
            public bool isFolder { get; set; }
            public string url { get; set; }
            public int size { get; set; }
            public string hashValue { get; set; }
        }
    }
}
