using Ionic.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace ZipValidatorClassLib
{
    public static class HelperFunction
    {
        public static bool NoZipFilesExist(JToken? token)
        {
            if (token == null)
                return true;

            var files = token["file"];
            if (files != null)
            {
                foreach (var file in files)
                {
                    var name = file?["name"]?.ToString();
                    if (!string.IsNullOrEmpty(name) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            var directories = token["directory"];
            if (directories != null)
            {
                foreach (var dir in directories)
                {
                    if (!NoZipFilesExist(dir))
                        return false;
                }
            }

            return true;
        }

        public static bool IsFileExistAtDepth(JObject jObject, int targetDepth)
        {
            string jPath = $"$..directory[?(@['@depth']=='{targetDepth}')]";
            var directoriesAtDepth = jObject.SelectTokens(jPath);

            foreach (var dir in directoriesAtDepth)
            {
                if (dir["file"]?.Any() == true)
                    return true;
            }

            return false;
        }

        public static bool IsFileExistAtRootLevel(JObject jObject)
        {
            JArray? rootArray = (JArray?)jObject["root"];
            return rootArray != null && rootArray.Count > 0 && rootArray[0]["file"] != null;
        }

        public static bool IsZipContentType(string contentType) =>
            contentType.Equals("application/zip", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/x-zip-compressed", StringComparison.OrdinalIgnoreCase);

        public static bool IsPasswordProtected(ZipFile zip) => zip.Any(e => e.UsesEncryption);

        public static bool HasHiddenFile(ZipFile zip, out List<string> listOfHiddenFiles)
        {
            listOfHiddenFiles = zip.Where(e => e.Attributes.HasFlag(FileAttributes.Hidden)).Select(x => x.FileName).ToList();
            return listOfHiddenFiles.Any();
        }

        public static bool HasZipInZip(ZipFile zip)
        {
            foreach (var entry in zip)
            {
                if (!entry.IsDirectory && entry.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static bool IsZipContentExtensionValid(ZipFile zip)
        {
            List<string> acceptableExt = [".dtd", ".pdf", ".xsl", ".xsd", ".xml", ".txt"];
            List<FileInfo> fileInfos = zip.Select(e => new FileInfo(e.FileName)).ToList();
            List<string> allFileExt = fileInfos.Select(x => x.Extension).ToList();
            return allFileExt.Any(ext => !acceptableExt.Contains(ext));
        }

        public static bool TryParseSequenceNum(string? sequenceNumber, out string? seqNum)
        {
            if (sequenceNumber == null || sequenceNumber.Length != 4)
            {
                seqNum = null;
                return false;
            }

            Regex regex = new(@"^[0-9]{4}$");
            if (!regex.Match(sequenceNumber).Success)
            {
                seqNum = null;
                return false;
            }

            seqNum = sequenceNumber;
            return true;
        }

        public static bool TryParseEctdID(string? applicationFolderName, out string ectdID)
        {
            ectdID = applicationFolderName ?? string.Empty;

            if (string.IsNullOrEmpty(applicationFolderName) || !applicationFolderName.StartsWith("e", StringComparison.OrdinalIgnoreCase))
                return false;

            string[] parts = applicationFolderName.Split("sg");
            if (parts.Length < 2)
                return false;

            string eDate = parts[0];
            string runningNum = parts[1];

            if (string.IsNullOrEmpty(eDate) || string.IsNullOrEmpty(runningNum))
                return false;

            string dateText = eDate.Substring(1);
            if (!DateTime.TryParseExact(dateText, "yyyyMMdd", null, DateTimeStyles.None, out _))
                return false;

            Regex regex = new(@"^[0-9]{4}$");
            return regex.Match(runningNum).Success;
        }

        public static bool IsPdfPasswordProtected(MemoryStream pdfStream)
        {
            try
            {
                pdfStream.Position = 0;
                using PdfDocument document = PdfDocument.Open(pdfStream);
                return false;
            }
            catch (BadPasswordException)
            {
                return true;
            }
            catch (PdfDocumentEncryptedException)
            {
                return true;
            }
        }

        public static string XMLtoJson(string xml)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            return JsonConvert.SerializeXmlNode(xmlDoc);
        }

        public static string GetErrorMessage(ZipValidatorErrorTypes errorTypes)
        {
            return errorTypes switch
            {
                ZipValidatorErrorTypes.ContainsHiddenFiles => "Package scan failed. Your .zip package contains hidden file(s). Please remove hidden files from the package.",
                ZipValidatorErrorTypes.ContainsPassword => "Package scan failed. Your .zip package requires a password to open and/or contains password protected file(s). Please remove all passwords from the package and/or file(s).",
                ZipValidatorErrorTypes.InvalidFolderStructure => "Package scan failed. Your .zip package folder structure is incorrect. Please check folder structure again and refer to the Singapore eCTD Specification for guidance.",
                ZipValidatorErrorTypes.ZipPackageEmpty => "Package scan failed. Your .zip package is empty. Please check your .zip package again.",
                ZipValidatorErrorTypes.NotZipFormat => "Package scan failed. Your package is either in unknown format or damaged. Please check your package again and ensure that you upload in .zip format only.",
                ZipValidatorErrorTypes.ContainsMalicious => "Package scan failed. Your .zip package has been flagged as potentially having malicious content. Please conduct a virus and malicious file scan locally, correct the issue and upload a clean package.",
                ZipValidatorErrorTypes.ContainsMoreThanOneSequence => "Package scan failed. Your .zip package contains more than one sequence. Please upload only one sequence per package.",
                ZipValidatorErrorTypes.CheckSumMismatch => "Package scan failed. Checksum mismatch in .zip package after the pre-transmission malicious scan. You may attempt to resubmit once. Otherwise, kindly contact HSA for assistance and reference the Transmission ID.",
                ZipValidatorErrorTypes.ContainsMoreThanOneApplicationFolder => "Package scan failed. Your .zip package contains more than one application. Please upload only one application per package.",
                ZipValidatorErrorTypes.EctdIdNotExisted => "Package verification failed. The Application Folder does not exist or is not an eCTD ID. Please check the structure and naming of your Application Folder.",
                ZipValidatorErrorTypes.EctdIdAndEntityIdNotLinked => "Package verification failed. You are not authorized to submit the package. Please check that your CorpPass login is for the correct company and that the Application Folder is a valid eCTD ID (requested and issued through the Portal).",
                ZipValidatorErrorTypes.EctdIdHasDisabled => "Package verification failed. The eCTD ID has been disabled. Please ensure you are using the correct eCTD ID or contact HSA for assistance.",
                ZipValidatorErrorTypes.UnknownScanningError => "Package scan failed. An unknown error has occurred during Package Check, please try again later. If the problem persists, please contact HSA for assistance.",
                _ => throw new NotSupportedException()
            };
        }

        public static string ListOfPathsToXML(List<string> listOfPaths, ZipFile zip)
        {
            using var stringWriter = new StringWriter();
            using var writer = new XmlTextWriter(stringWriter);

            writer.WriteStartDocument();
            writer.WriteStartElement("root");
            writer.WriteAttributeString("xmlns:json", "http://james.newtonking.com/projects/json");
            writer.WriteAttributeString("json:Array", "true");

            var previous = Array.Empty<string>();
            foreach (var str in listOfPaths)
            {
                var current = str.Split('/', StringSplitOptions.RemoveEmptyEntries);
                int i;
                for (i = 0; i < Math.Min(current.Length, previous.Length); i++)
                {
                    if (current[i] != previous[i])
                        break;
                }

                for (int j = i; j < previous.Length; j++)
                    writer.WriteEndElement();

                for (int j = i; j < current.Length; j++)
                {
                    if (string.IsNullOrEmpty(Path.GetExtension(current[j])))
                    {
                        writer.WriteStartElement("directory");
                        writer.WriteAttributeString("depth", j.ToString());
                        writer.WriteAttributeString("json:Array", "true");
                        writer.WriteElementString("name", current[j]);
                    }
                    else
                    {
                        writer.WriteStartElement("file");
                        writer.WriteAttributeString("json:Array", "true");
                        ZipEntry? zipEntry = zip.FirstOrDefault(x => x.FileName.Equals(str));
                        if (zipEntry != null)
                        {
                            writer.WriteAttributeString("encrypted", zipEntry.UsesEncryption.ToString());
                            writer.WriteAttributeString("last-modified", zipEntry.LastModified.ToString());
                        }
                        writer.WriteElementString("name", current[j]);
                    }
                }

                previous = current;
            }

            writer.WriteEndDocument();
            return stringWriter.ToString();
        }
    }
}
