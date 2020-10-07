using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateSPInclude
{
    public class Changes
    {
        public int count { get; set; }
        public Value[] value { get; set; }
    }

    public class Value
    {
        public Item item { get; set; }
        public string changeType { get; set; }
    }

    public class Item
    {
        public int version { get; set; }
        public int size { get; set; }
        public string hashValue { get; set; }
        public string path { get; set; }
        public string url { get; set; }
    }

}
