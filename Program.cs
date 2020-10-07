using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace UpdateSPInclude
{
    class Program
    {
        //Get data from API
        private static async Task<string> GetJson(string accessToken, string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(
                            System.Text.ASCIIEncoding.ASCII.GetBytes(
                                string.Format("{0}:{1}", "", accessToken))));

                    using (HttpResponseMessage response = client.GetAsync(url).Result)
                    {
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to get data from API using {url}");
                throw ex;
            }
        }

        //Post data to API
        private static async Task<string> PostJson(string accessToken, string url, string postJson)
        {
            HttpContent content = new StringContent(postJson, Encoding.UTF8, "application/json");
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(
                            System.Text.ASCIIEncoding.ASCII.GetBytes(
                                string.Format("{0}:{1}", "", accessToken))));

                    using (HttpResponseMessage response = client.PostAsync(url, content).Result)
                    {
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to post data to API using {url}");
                throw ex;
            }
        }

        //Filter changes by file extension, and by folder
        private static List<string> Filter(Changes changes, string filter)
        {
            Console.WriteLine($"Filtering out {filter} files ...");

            //Match files to filter criteria
            List<string> files = new List<string>();
            foreach (Value c in changes.value)
            {
                string path = c.item.path;
                
                if (path.EndsWith(filter) && (path.Contains("Viewpoint/ClientSide") || path.Contains("Viewpoint/ServerSide") || path.Contains("BI/Reports") || path.Contains("Viewpoint/ExternalClientServer")))
                {
                    if (filter == ".rpt")
                    {
                        files.Add(Path.GetFileName(path));
                    }
                    //Assemblies are formatted properly in "GetDll"
                    else
                    {
                        files.Add(path);
                    } 
                }
            }

            //Print filtered files to the console
            if (files.Count == 0)
            {
                Console.WriteLine($"No {filter} files found.\n");
                
                return files;
            }
            else
            {
                Console.WriteLine($"These {filter} files were found:");
                foreach (string f in files)
                {
                    Console.WriteLine(Path.GetFileName(f));
                }
                Console.WriteLine("");
                return files;
            }
        }

        //Takes the original SPInclude and a list of changes and updates file accordingly
        private static string ModifySPInclude(string original, List<string> changes)
        {
            //Convert SPInclude to an object
            try
            {
                JsonConvert.DeserializeObject<SPInclude>(original);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Coult not interpret ServicePackInclude.json. File may not be correctly formatted.");
                throw ex;
            }

            SPInclude spInclude = JsonConvert.DeserializeObject<SPInclude>(original);

            foreach (string change in changes)
            {
                //Assume file isn't present in SPInclude
                bool fileExists = false;
                foreach (var file in spInclude.files)
                {
                    //If there is a match, set include to 'true'
                    if (file.filename == change)
                    {
                        file.include = "true";
                        fileExists = true;
                    }
                }

                //If the file isn't present in SPInclude, add it with value 'true'
                if (!fileExists)
                {
                    spInclude.add(change);
                }
            }

            return JsonConvert.SerializeObject(spInclude, Newtonsoft.Json.Formatting.Indented);
        }

        //Modifies a given file (defined by the path) in the repository with given content, and current changeset version
        private static async Task<string> RepoModify(string accessToken, string content, string path, int currentVersion)
        {
            //Used to send POST request to API
            string postURL = "https://dev.azure.com/ViewpointVSO/_apis/tfvc/changesets?api-version=5.1";

            //Template to create json object
            string TemplateJson = @"{
                             ""changes"": [
                                {
                                 ""item"": {
                                 ""version"": 0,
                                 ""path"": ""template"",
                                 ""contentMetadata"": {
                                   ""encoding"": 0,
                                   ""contentType"": ""template""
                                  }
                                },
                               ""changeType"": ""template"",
                               ""newContent"": {
                                  ""content"": ""template""
                                }
                              }
                            ],
                           ""comment"": ""template""
                          }";

            //Create json object from template
            PostJson postJson = JsonConvert.DeserializeObject<PostJson>(TemplateJson);

            //Update the data in the json object
            postJson.changes[0].item.version = currentVersion;
            postJson.changes[0].item.path = path;
            postJson.changes[0].item.contentMetadata.encoding = 1200;
            postJson.changes[0].item.contentMetadata.contentType = "text/plain";
            postJson.changes[0].changeType = "edit";
            postJson.changes[0].newContent.content = content;
            postJson.comment = "Automated checkin";

            //Convert json object to a string
            string postJsonStr = JsonConvert.SerializeObject(postJson);

            //Send a POST request to the API 
            return await PostJson(accessToken, postURL, postJsonStr);
        }

        //From a given file in the repository, gets the project file that corresponds to it.
        private static async Task<string> GetProjectFile(string path, string buildNumber, string accessToken)
        {
            //Split path by '/'
            string[] splitPath = path.Split('/');

            //Search for proj one level up from current path until one is found
            int currentLevel = splitPath.Length - 1; //End of path (file or folder name)
            int originalLevel = currentLevel;
            bool found = false;
            string ret = "unassigned";

            while (!found)
            {
                //Remove current level from filepath
                path = path.Replace($"/{splitPath[currentLevel]}", "");

                //Stop when the 'Viewpoint' directory is reached. Past there is too far. Send alert email.
                if (path.EndsWith("Viewpoint"))
                {
                    string message = $"WARNING: No project file was found for '{splitPath[originalLevel]}'";
                    Console.WriteLine(message);
                    string emailSubject = $"DLL NOT FOUND: BUILD {buildNumber}";
                    SendAlertEmail(emailSubject, message);
                    return "";
                }

                //Get items in path from api
                string requestUrl = $"https://dev.azure.com/ViewpointVSO/Vista/_apis/tfvc/items?scopePath={path}&api-version=5.1";
                string response = await GetJson(accessToken, requestUrl);

                //Ensure json is interpreted correctly
                try
                {
                    JsonConvert.DeserializeObject<DirectoryContents>(response);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Could not deserialize response from API. Path to get items in a directory might be invalid.");
                    throw ex;
                }
                DirectoryContents dc = JsonConvert.DeserializeObject<DirectoryContents>(response);


                Console.WriteLine($"Looking for {splitPath[originalLevel]}'s project file in {path} ...");

                //If the original file is vb
                if (splitPath[originalLevel].EndsWith(".vb"))
                {
                    //For each item in the current directory
                    foreach (var item in dc.value)
                    {
                        //Write its path to the console
                        Console.WriteLine(item.path);

                        //If it ends with vbproj, end the loop and return its path
                        if (item.path.EndsWith(".vbproj"))
                        {
                            ret = item.path;
                            found = true;
                        }
                    }
                    Console.WriteLine("");

                    //Go up a level in the directory, if it's found it won't enter this loop again
                    currentLevel--;
                }
                //If the original file is cs
                else if (splitPath[originalLevel].EndsWith(".cs"))
                {
                    foreach (var item in dc.value)
                    {
                        Console.WriteLine(item.path);

                        if (item.path.EndsWith(".csproj"))
                        {
                            ret = item.path;
                            found = true;
                        }
                    }
                    Console.WriteLine("");

                    currentLevel--;
                }
                else
                {
                    throw new ArgumentException($"Tried to find the project file for a non-vb/cs file: {splitPath[originalLevel]}");
                }
            }
            //Return the project file's path as a string
            return ret;
        }

        //Gets the name of the .dll created based on a given project file, makes sure that the code file is included in the project file.
        private static async Task<string> GetDll(string projFilePath, string originalFilePath, string buildNumber, string accessToken)
        {
            //Get the contents of the project file (xml)
            string xmlJsonRequestUrl = $"https://dev.azure.com/ViewpointVSO/Vista/_apis/tfvc/items?path={projFilePath}&includeContent=true&api-version=5.1";
            string response = await GetJson(accessToken, xmlJsonRequestUrl);
            try
            {
                JsonConvert.DeserializeObject<ItemData>(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not interpret response from API when requesting data from {projFilePath}");
                throw ex;
            }
            ItemData projFileData = JsonConvert.DeserializeObject<ItemData>(response);
            string projFileXml = projFileData.content;

            //Load xml and get the assembly name
            XmlDocument xml = new XmlDocument();
            try
            {
                xml.LoadXml(projFileXml);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Could not load the project file xml to find the assembly name.");
                throw ex;
            }
            XmlNodeList assemblyNodeList = xml.GetElementsByTagName("AssemblyName");

            //Warn if there is more than one assembly name, or none is found
            if (assemblyNodeList.Count > 1)
            {
                Console.WriteLine("WARNING: Multiple assembly names found in project file, using the first one.");
            }
            else if (assemblyNodeList.Count < 1)
            {
                throw new Exception("No tag matching 'AssemblyName' was found in the project file.");
            }

            //Check if there's a match in compile-include section
            XmlNodeList compileNodeList = xml.GetElementsByTagName("Compile");
            string originalFileName = Path.GetFileName(originalFilePath);
            bool found = false;
            Console.WriteLine($"Ensuring {originalFileName} is included in the project file ...\n");
            
            for (int i = 0; i < compileNodeList.Count; i++)
            {
                if (compileNodeList[i].OuterXml.Contains(originalFileName))
                {
                    found = true;
                }
            }
            if (!found)
            {
                string message = $"WARNING: Could not find '{originalFileName}' included in '{Path.GetFileName(projFilePath)}'. Dll for this file will not be included.\n";
                Console.WriteLine(message);
                string emailSubject = $"DLL NOT FOUND: BUILD {buildNumber}";
                SendAlertEmail(emailSubject, message);
                return "";
            }

            //Make sure the file in question compiles to a .dll (not an .exe for example)
            Console.WriteLine("Verifying that the output type is a '.dll' ...\n");
            XmlNodeList outputTypeNodeList = xml.GetElementsByTagName("OutputType");
            
            if (outputTypeNodeList.Count > 1)
            {
                Console.WriteLine("WARNING: Multiple output types found in project file, verifying with the first one.");
            }
            else if (outputTypeNodeList.Count < 1)
            {
                throw new Exception("No tag matching 'OutputType' was found in the project file.");
            }

            if (!outputTypeNodeList[0].InnerText.Equals("Library"))
            {
                string message = $"WARNING: '{originalFileName}' is not compiled to a library. Instead it is compiled to a '{outputTypeNodeList[0].InnerText}', skipping.\n";
                Console.WriteLine(message);

                //Send alert email
                string emailSubject = $"CHECK SERVICEPACKINCLUDE CHANGE: BUILD {buildNumber}";
                SendAlertEmail(emailSubject, message);
                return "";
            }

            //Get dll name from AssemblyName property, append dll. If from ExternalClientServer folder, include appropriate prefix.
            string assemblyName = "";
            if (projFilePath.Contains("Viewpoint/ExternalClientServer"))
            {
                //Add appropriate extension
                if (projFilePath.Contains("DbAccessForExternalClient") || projFilePath.Contains("ExternalAccessDataWcfService") || projFilePath.Contains("ExternalAccessDataWebWcfServiceHost"))
                {
                    assemblyName = $"_PublishedWebsites\\ExternalAccessDataWebWcfServiceHost\\bin\\{assemblyNodeList[0].InnerText}.dll";
                }
                else if (projFilePath.Contains("ExternalClientAccessCrystal"))
                {
                    assemblyName = $"_PublishedWebsites\\ExternalClientAccessCrystal\\bin\\{assemblyNodeList[0].InnerText}.dll";
                }

                //Send alert email for ExternalClientServer
                string message;
                if (assemblyName == "")
                {
                    message = $"WARNING: Please check ServicePackInclude.json for build {buildNumber}, as the automated update may only partially cover Viewpoint/ExternalClientServer. No dll could be found for '{originalFileName}'\n";
                    Console.WriteLine(message);
                }
                else
                {
                    message = $"WARNING: Please check ServicePackInclude.json for build {buildNumber}, as the automated update may only partially cover Viewpoint/ExternalClientServer. Included '{assemblyName}' for '{originalFileName}'\n";
                    Console.WriteLine(message);
                }

                string emailSubject = $"CHECK SERVICEPACKINCLUDE CHANGE: BUILD {buildNumber}";
                SendAlertEmail(emailSubject, message);
            }
            else
            {
                assemblyName = $"{assemblyNodeList[0].InnerText}.dll";
            }

            //Return it
            return assemblyName;
        }

        //For a given set of code files (cs and vb), created a list of their corresponding dll files.
        private static async Task<List<string>> GetAssemblyFiles(List<string> files, string buildNumber, string accessToken)
        {
            //List to return
            List<string> assemblyFiles = new List<string>();
            
            //Make sure it's not empty
            if (!(files.Count == 0))
            {
                foreach (string file in files)
                {
                    //Get the project file
                    string projFile = await GetProjectFile(file, buildNumber, accessToken);
                    
                    //If a project file is found
                    if (!projFile.Equals(""))
                    {
                        //Write it to the console
                        Console.WriteLine($"Found project file for {Path.GetFileName(file)}: {Path.GetFileName(projFile)}\n");

                        //Get the dll from the project file
                        string assemblyFile = await GetDll(projFile, file, buildNumber, accessToken);

                        //If the assembly file is found
                        if (!assemblyFile.Equals(""))
                        {
                            //Print it to the console
                            Console.WriteLine($"Assembly file: {assemblyFile}\n");

                            //Add it to the list
                            if (!assemblyFiles.Contains(assemblyFile))
                            {
                                assemblyFiles.Add(assemblyFile);
                            }
                        }
                    }
                }
            }
            return assemblyFiles;
        }

        private static async void UpdateSPInclude_API(string APIPath, List<string> reports, List<string> assemblies, string accessToken)
        {
            //Deserialize the data currently stored in the json
            Console.WriteLine($"Getting current ServicePackInclude.json from the AZDO repository at {APIPath} ...\n");
            string APIRequestURL = $"https://dev.azure.com/ViewpointVSO/Vista/_apis/tfvc/items?path={APIPath}&includeContent=true&api-version=5.1";
            string spJson = await GetJson(accessToken, APIRequestURL);

            //Ensure good json response
            try
            {
                JsonConvert.DeserializeObject<ItemData>(spJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Bad response from api when attempting to retrieve current servicepackinclude json.");
                throw ex;
            }

            ItemData APIData = JsonConvert.DeserializeObject<ItemData>(spJson);

            //Include the changed reports
            string original = APIData.content;
            string reportsAdded = ModifySPInclude(original, reports);
            string updatedSPInclude = ModifySPInclude(reportsAdded, assemblies);

            //If the modified version is the samee as the original, exit. Otherwise attempt to update it in the repo.
            Console.WriteLine("Checking to see if it needs to be changed...\n");
            if (updatedSPInclude == original)
            {
                Console.WriteLine("File already up to date.");
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("Attempting to modify it in the repository...\n");
                string response = await RepoModify(accessToken, updatedSPInclude, APIData.path, APIData.version);

                //Ensure good json response
                try
                {
                    JsonConvert.DeserializeObject<CheckinResponse>(response);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: A bad response was recieved when attempting to check in the file.");
                    throw ex;
                }

                CheckinResponse checkinResponse = JsonConvert.DeserializeObject<CheckinResponse>(response);
                Console.WriteLine($"Changeset {checkinResponse.changesetId} successfuly checked in at {checkinResponse.createdDate.ToLocalTime()}");
                Environment.Exit(0);
            }
        }

        private static void UpdateSPInclude_Local(string localPath, List<string> reports, List<string> assemblies)
        {
            Console.WriteLine($"Getting local SP Include file from {localPath} ...\n");

            //Ensure file can be found
            try
            {
                File.ReadAllText(localPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not find local SP Include file at {localPath}");
                throw ex;
            }
            string original = File.ReadAllText(localPath);
            
            //Modify the file
            string reportsAdded = ModifySPInclude(original, reports);
            string updatedSPInclude = ModifySPInclude(reportsAdded, assemblies);

            //See if the file actually changed. If it did, updated it. If not, continue.
            if (original == updatedSPInclude)
            {
                Console.WriteLine("File already up to date.\n");
            }
            else
            {
                File.WriteAllText(localPath, updatedSPInclude);
                Console.WriteLine("Updated SP include file in the local build directory.\n");
            }
            
        }

        private static void SendAlertEmail(string subject, string message)
        {
            //List of email addresses to send the alert to
            List<string> addresses = new List<string>();
            addresses.Add("PLACEHOLDER");
            addresses.Add("PLACEHOLDER");

            //Create a new mail message from dbupgrader
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress("PLACEHOLDER");

            //Add recepient addresses 
            foreach (string address in addresses)
            {
                mail.To.Add(new MailAddress(address));
            }

            //Set the subject and body
            mail.Subject = subject;
            mail.Body = message;

            //Create the smtp client
            SmtpClient smtpClient = new SmtpClient();
            smtpClient.Host = "smtp.gmail.com";
            smtpClient.Port = 587;
            smtpClient.Credentials = new NetworkCredential("PLACEHOLDER", "PLACEHOLDER");
            smtpClient.EnableSsl = true;

            //Send the email and write to the console
            Console.WriteLine($"Sending alert emails to: {String.Join(", ", addresses)} ...\n");
            smtpClient.Send(mail);
            Console.WriteLine("Alert email sent.\n");
        }

        public async static Task<int> Main(string[] args)
        {
            //Check arguments are supplied
            if (args.Length != 5)
            {
                throw new ArgumentException($"{args.Length} arguments were supplied, please include exactly 5 arguments: the changeset id, definition name, build number, local directory of the build, and an access token.");
            }

            //Params for REST requests
            string changesetId = args[0];
            string definitionName = args[1];
            string buildNumber = args[2];
            string localDirectory = args[3];
            string accessToken = args[4];

            //Extract the branch from definition name
            string branch = definitionName.Replace("Vista.", "").Replace(".ServicePack.Installer", "");

            Console.WriteLine($"Using changeset: {changesetId}");
            Console.WriteLine($"Branch: {branch}\n");
            Console.WriteLine($"Build number: {buildNumber}");
            Console.WriteLine($"Local build directory: {localDirectory}");

            //Get data for the latest changes associated with the build and print them to the console
            Console.WriteLine("Getting changes...");
            string changesUrl = $"https://dev.azure.com/ViewpointVSO/_apis/tfvc/changesets/{changesetId}/changes?api-version=5.1";
            string changesJson = await GetJson(accessToken, changesUrl);

            //Ensure json can be interpreted (accessToken is correct)
            try
            {
                JsonConvert.DeserializeObject<Changes>(changesJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Bad response from api when getting changes. Could be due to a bad access token.");
                throw ex;
            }

            Changes changes = JsonConvert.DeserializeObject<Changes>(changesJson);

            //Print all found changes to the console
            Console.WriteLine("Changes found:");
            bool spinclude = false;
            foreach (Value c in changes.value)
            {
                string file = c.item.path;
                Console.WriteLine(file);
                if (Path.GetFileName(file) == "ServicePackInclude.json")
                {
                    spinclude = true;
                }
            }

            //If servicepackinclude.json is in the changeset, don't continue.
            if (spinclude)
            {
                Console.WriteLine("Servicepackinlcude.json was included in this changeset, skipping update.");
                Environment.Exit(0);
            }

            Console.WriteLine("");

            Console.WriteLine("Looking for changed files in Viewpoint/Clientside, Viewpoint/ServerSide, Viewpoint/ExternalClientServer, and BI/Reports ...\n");
            //Filter out RPT files and print them to the console 
            List<string> reports = Filter(changes, ".rpt");

            //Filter out VB files and print them to the console
            List<string> vbFiles = Filter(changes, ".vb");

            //Filter out CS files and print them to the console
            List<string> csFiles = Filter(changes, ".cs");

            //If there are no files relevant to SPInclude, exit.
            if (reports.Count == 0 && vbFiles.Count == 0 && csFiles.Count == 0)
            {
                Console.WriteLine("No relevant files found.");
                Environment.Exit(0);
            }

            //Find the project files associated with VB and CS files, then get the name of the assmebly file from them
            List<string> vbAssemblies = await GetAssemblyFiles(vbFiles, buildNumber, accessToken);
            List<string> csAssemblies = await GetAssemblyFiles(csFiles, buildNumber, accessToken);

            //Combine the lists
            List<string> assemblies = vbAssemblies.Concat(csAssemblies).ToList();

            //Write the final list of files to be changed to the console
            Console.WriteLine("");
            if (!(reports.Count == 0))
            {
                Console.WriteLine("Reports to be included:");
                foreach (string r in reports)
                {
                    Console.WriteLine(Path.GetFileName(r));
                }
            }
            else
            {
                Console.WriteLine("No reports to be included.");
            }
            Console.WriteLine("");
            if (!(assemblies.Count == 0))
            {
                Console.WriteLine("Assemblies to be included:");
                foreach (string a in assemblies)
                {
                    Console.WriteLine(a); //assemblies are converted from paths in 'GetAssemblyFiles'
                }
            }
            else
            {
                Console.WriteLine("No assemblies to be included.");
            }
            Console.WriteLine("");

            //Modify in the local build server directory
            string localSPIncludePath = $"{localDirectory}/Installers/Vista_ServicePack/ContentLists/ServicePackInclude.json";
            UpdateSPInclude_Local(localSPIncludePath, reports, assemblies);

            //Modify in AZDO repo using API
            string spJsonAPIPath = $"$/Vista/Branches/{branch}/Installers/Vista_ServicePack/ContentLists/ServicePackInclude.json";
            UpdateSPInclude_API(spJsonAPIPath, reports, assemblies, accessToken);

            //Return
            return 0;
        }
    }
}
