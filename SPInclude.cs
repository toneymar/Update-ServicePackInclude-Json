using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateSPInclude
{
    public class SPInclude
    {
        public List<File> files { get; set; }

        //Adds a new file to the json
        public void add(string filename)
        {
            File newFile = new File();
            newFile.filename = filename;
            newFile.include = "true";
            files.Add(newFile);
        }

        public class File
        {
            public string filename { get; set; }
            public string include { get; set; }
        }
    }
}
