using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateSPInclude
{

    public class CheckinResponse
    {
        public object[] checkinNotes { get; set; }
        public Policyoverride policyOverride { get; set; }
        public int changesetId { get; set; }
        public string url { get; set; }
        public Author author { get; set; }
        public Checkedinby checkedInBy { get; set; }
        public DateTime createdDate { get; set; }
        public string comment { get; set; }

        public class Policyoverride
        {
            public object[] policyFailures { get; set; }
        }

        public class Author
        {
            public string displayName { get; set; }
            public string url { get; set; }
            public string id { get; set; }
            public string uniqueName { get; set; }
            public string imageUrl { get; set; }
        }

        public class Checkedinby
        {
            public string displayName { get; set; }
            public string url { get; set; }
            public string id { get; set; }
            public string uniqueName { get; set; }
            public string imageUrl { get; set; }
        }
    }

}
