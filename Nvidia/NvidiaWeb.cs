using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace NvidiaDrivers.Nvidia {
    public class API {
         static HttpClient client = new HttpClient();
         static String BaseURL = "https://www.nvidia.co.uk/Download/API/lookupValueSearch.aspx";
        public static (bool, string) IsNewDriver() {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");

            (string, DateTime) gpu = GPU.Current();

            // split graphics card name into vendor and model
            string[] gpuSplit = gpu.Item1.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            string model = gpuSplit[1];
            string guessedSeries = gpuSplit[3].Substring(0, 2);

            string mtl = modelToLookup(model);
            string stl = seriesToLookup(guessedSeries, mtl);
            string ctl = cardToLookup(gpu.Item1, stl);
            string otl = osToLookup();

            string downloadUrl = $"https://www.nvidia.co.uk/Download/processDriver.aspx?psid={stl}&pfid={ctl}&rpf={mtl}&osid={otl}&lid=2&lang=en-uk&ctk=0&dtid=1&dtcid=1";
            // Fetch downloadUrl.
            HttpResponseMessage response = client.GetAsync(downloadUrl).Result;
            string downloadPage = response.Content.ReadAsStringAsync().Result;
            var web = new HtmlWeb();
            var doc = web.Load(downloadPage);

            var element = doc.DocumentNode.SelectSingleNode("//*[@id=\"tdReleaseDate\"]").InnerText.Trim();
            // Parse date from "2021.11.16"
            DateTime.TryParseExact(element, "yyyy.MM.dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime releaseDate);            // If release date is newer than driver date, print it.
            if (releaseDate > gpu.Item2) {
                return (true, downloadPage);
            }
            else {
                return (false, "");
            }
        }

        // Attempts to guess series of card based on various factors. Currently does not support notebooks.
        public static string GuessGPU() {
            (string, DateTime) gpu = GPU.Current();

            // split graphics card name into vendor and model
            string[] gpuSplit = gpu.Item1.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            string model = gpuSplit[1];
            string guessedSeries = gpuSplit[3].Substring(0, 2);

            string mtl = modelToLookup(model);
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(BaseURL + "?TypeID=2&ParentID=" + mtl);
            string maybe = "";
            var LookupValues = xmlDoc.SelectNodes("//LookupValue");
            foreach (XmlNode LookupValue in LookupValues) {
                if (LookupValue.SelectSingleNode("Name").InnerText.Contains(" " + guessedSeries + " ") && !LookupValue.SelectSingleNode("Name").InnerText.Contains("Notebook")) {
                    maybe = LookupValue.SelectSingleNode("Name").InnerText;
                }
            }

            return maybe;
        }

        private static string modelToLookup(string model) {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(BaseURL + "?TypeID=1");
            string modelToLookup = "1";
            var LookupValues = xmlDoc.SelectNodes("//LookupValue");
            foreach (XmlNode LookupValue in LookupValues) {
                if (LookupValue.SelectSingleNode("Name").InnerText == model) {
                    modelToLookup = LookupValue.SelectSingleNode("Value").InnerText;
                }
            }

            return modelToLookup;
        }

        private static string seriesToLookup(string series, string mtl) {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(BaseURL + "?TypeID=2&ParentID=" + mtl);
            string seriesToLookup = "1";
            var LookupValues = xmlDoc.SelectNodes("//LookupValue");
            foreach (XmlNode LookupValue in LookupValues) {
                if (LookupValue.SelectSingleNode("Name").InnerText.Contains(" " + series + " ") && !LookupValue.SelectSingleNode("Name").InnerText.Contains("Notebook")) {
                    seriesToLookup = LookupValue.SelectSingleNode("Value").InnerText;
                }
            }

            return seriesToLookup;
        }

        private static string cardToLookup(string gpuFullname, string stl) {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(BaseURL + "?TypeID=3&ParentID=" + stl);
            string cardToLookup = "1";
            var LookupValues = xmlDoc.SelectNodes("//LookupValue");
            foreach (XmlNode LookupValue in LookupValues) {
                if (LookupValue.SelectSingleNode("Name").InnerText == gpuFullname.Replace("NVIDIA ", "")) {
                    cardToLookup = LookupValue.SelectSingleNode("Value").InnerText;
                }
            }

            return cardToLookup;
        }

        private static string osToLookup() {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            string os = "";
            foreach (ManagementObject mo in searcher.Get()) {
                foreach (PropertyData property in mo.Properties) {
                    if (property.Name == "Caption") {
                        os = property.Value.ToString();
                    }
                }
            }
            // Get middle two words of OS name.
            string[] osSplit = os.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            string osSimple = osSplit[1] + " " + osSplit[2];
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(BaseURL + "?TypeID=4");
            string osToLookup = "1";
            var LookupValues = xmlDoc.SelectNodes("//LookupValue");
            foreach (XmlNode LookupValue in LookupValues) {
                if (LookupValue.SelectSingleNode("Name").InnerText == osSimple) {
                    osToLookup = LookupValue.SelectSingleNode("Value").InnerText;
                }
            }

            return osToLookup;
        }
    }

    public class GPU {
        public String Name { get; set; }

        public static (string, DateTime) Current() {
             ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");

            string graphicsCard = string.Empty;
            DateTime driverDate = DateTime.Now;
            foreach (ManagementObject mo in searcher.Get()) {
                foreach (PropertyData property in mo.Properties) {
                    if (property.Name == "Description") {
                        graphicsCard = property.Value.ToString();
                    }
                    if (property.Name == "DriverDate") {
                        driverDate = DateTime.ParseExact(property.Value.ToString().Substring(0, 8), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            return (graphicsCard, driverDate);
        }
    }

    public class Driver {
        public string Version { get; }
        public string DatePublished { get; }
    }
}