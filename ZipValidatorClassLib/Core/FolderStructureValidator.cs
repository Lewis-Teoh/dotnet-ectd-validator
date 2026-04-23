using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ZipValidatorClassLib
{
    public class FolderStructureValidator
    {
        private const string RegionalFile = "sg-regional.xml";
        public string? ApplicationFolderName { get; private set; }
        public string? SequenceFolderName { get; private set; }
        private static readonly Regex Level1Pattern = new(@"^e\d{8}sg\d{4}$", RegexOptions.IgnoreCase);
        private static readonly Regex Level2Pattern = new(@"^\d{4}$");
        private static readonly HashSet<string> AllowedLevel3Folders = ["m1", "m2", "m3", "m4", "m5", "util"];
        private static readonly HashSet<string> AllowedLevel3Files = ["index.xml", "index-md5.txt", "index.html"];
        private static readonly HashSet<string> RequiredLevel3Files = ["index.xml", "index-md5.txt"];

        private readonly List<ValidationErrorCode> _errors = [];
        public IReadOnlyList<ValidationErrorCode> Errors => _errors.AsReadOnly();

        public void Validate(JObject rootJson)
        {
            JToken? applicationLevel = rootJson.SelectToken(@"$.root[*].directory");
            if (applicationLevel == null || applicationLevel.Type != JTokenType.Array)
            {
                _errors.Add(ValidationErrorCode.RootMustHaveAtLeastOneFolder);
                return;
            }

            if (applicationLevel.Count() > 1)
            {
                IEnumerable<JToken?> appLevel = applicationLevel.SelectTokens("$.[*].name");
                foreach (var app in appLevel.ToList())
                {
                    if (app != null && HelperFunction.TryParseEctdID(app.ToString(), out _))
                    {
                        _errors.Add(ValidationErrorCode.RootMustHaveExactlyOneFolder);
                        return;
                    }
                }
            }

            if (rootJson["root"] is not JArray rootArr || rootArr.Count == 0)
            {
                _errors.Add(ValidationErrorCode.RootMustHaveAtLeastOneFolder);
                return;
            }

            var rootItem = rootArr[0];

            if (rootItem["file"] is JArray rootFiles && rootFiles.Count > 0)
                _errors.Add(ValidationErrorCode.RootFilesNotAllowed);

            JArray? dirLevel0 = rootItem["directory"] as JArray;
            var dir0 = dirLevel0![0];
            string folderName = dir0["name"]?.ToString() ?? "";

            if (!Level1Pattern.IsMatch(folderName))
            {
                _errors.Add(ValidationErrorCode.InvalidLevel1FolderName);
                return;
            }

            ApplicationFolderName = folderName;
            ValidateLevel0(dir0);
        }

        private void ValidateLevel0(JToken dir0)
        {
            if (dir0["file"] is JArray lvl1Files && lvl1Files.Count > 0)
            {
                _errors.Add(ValidationErrorCode.Level2FilesNotAllowed);
                return;
            }

            if (dir0["directory"] is not JArray level1Dirs || level1Dirs.Count != 1)
            {
                _errors.Add(ValidationErrorCode.SequenceLevelMustHaveExactlyOneFolder);
                return;
            }

            var dir1 = level1Dirs[0];
            string folderName = dir1["name"]?.ToString() ?? "";

            if (!Level2Pattern.IsMatch(folderName))
                _errors.Add(ValidationErrorCode.InvalidSequenceFolderName);
            else
                SequenceFolderName = folderName;

            ValidateLevel1(dir1);
        }

        private void ValidateLevel1(JToken dir1)
        {
            var level3Folders = new HashSet<string>();
            bool hasM1 = false, hasUtil = false;

            foreach (var item in dir1["directory"] ?? new JArray())
            {
                string folderName = item["name"]?.ToString() ?? "";
                level3Folders.Add(folderName);

                if (!AllowedLevel3Folders.Contains(folderName))
                    _errors.Add(ValidationErrorCode.InvalidLevel3Folders);

                if (folderName == "m1")
                {
                    hasM1 = true;
                    ValidateM1(item);
                }
                else if (folderName == "util")
                {
                    hasUtil = true;
                    ValidateUtil(item);
                }
            }

            if (!hasM1) _errors.Add(ValidationErrorCode.M1FolderIsRequired);
            if (!hasUtil) _errors.Add(ValidationErrorCode.UtilFolderIsRequired);

            var presentFiles = new HashSet<string>();
            foreach (var file in dir1["file"] ?? new JArray())
            {
                string fname = file["name"]?.ToString() ?? "";
                if (!AllowedLevel3Files.Contains(fname))
                    _errors.Add(ValidationErrorCode.InvalidLevel3Files);
                else
                    presentFiles.Add(fname);
            }

            foreach (var required in RequiredLevel3Files)
            {
                if (!presentFiles.Contains(required))
                    _errors.Add(ValidationErrorCode.InvalidLevel3Files);
            }
        }

        private void ValidateM1(JToken m1Dir)
        {
            if (m1Dir["directory"] is not JArray level4Dirs)
            {
                _errors.Add(ValidationErrorCode.M1MustContainOnlySg);
                return;
            }

            if (m1Dir["file"] is JArray filesInM1 && filesInM1.Count > 0)
            {
                foreach (var _ in filesInM1)
                    _errors.Add(ValidationErrorCode.FilesNotAllowedInM1);
            }

            if (level4Dirs.Count != 1 || level4Dirs.First()["name"]?.ToString() != "sg")
            {
                _errors.Add(ValidationErrorCode.M1MustContainOnlySg);
                return;
            }

            var sgFolder = level4Dirs.First();
            var sgFiles = sgFolder["file"] as JArray ?? new JArray();
            bool hasSgRegionalXml = sgFiles.Any(f => f["name"]?.ToString() == RegionalFile);

            if (!hasSgRegionalXml)
                _errors.Add(ValidationErrorCode.MissingSgRegionalXml);
        }

        private void ValidateUtil(JToken utilDir)
        {
            var files = utilDir["file"] as JArray;
            if (files != null)
                _errors.Add(ValidationErrorCode.InvalidFilesInUtil);

            if (utilDir["directory"] is not JArray utilDirs)
            {
                _errors.Add(ValidationErrorCode.DtdAndStyleFoldersRequired);
                return;
            }

            var subfolders = utilDirs.Select(d => d["name"]?.ToString()).ToList();
            if (!subfolders.Contains("dtd")) _errors.Add(ValidationErrorCode.UtilFolderMustContainDtd);
            if (!subfolders.Contains("style")) _errors.Add(ValidationErrorCode.UtilFolderMustContainStyle);

            foreach (var dir in utilDirs)
            {
                string name = dir["name"]?.ToString() ?? "";
                if (name != "dtd" && name != "style")
                {
                    _errors.Add(ValidationErrorCode.InvalidFolderInUtil);
                }
                else if (name == "dtd")
                {
                    var result = ValidateDtdFolder(dir);
                    if (result.HasValue) _errors.Add(result.Value);
                }
                else if (name == "style")
                {
                    var result = ValidateStyleFolder(dir);
                    if (result.HasValue) _errors.Add(result.Value);
                }
            }
        }

        private static ValidationErrorCode? ValidateDtdFolder(JToken dtdFolderToken)
        {
            var files = dtdFolderToken["file"] as JArray;
            if (files == null)
                return ValidationErrorCode.DtdFolderIsEmpty;

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file["name"]?.ToString() ?? "").ToLowerInvariant();
                if (extension != ".xsd" && extension != ".dtd")
                    return ValidationErrorCode.InvalidFileTypeInDtdFolder;
            }

            return null;
        }

        private static ValidationErrorCode? ValidateStyleFolder(JToken styleFolder)
        {
            var files = styleFolder["file"] as JArray;
            if (files == null)
                return ValidationErrorCode.StyleFolderIsEmpty;

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file["name"]?.ToString() ?? "").ToLowerInvariant();
                if (extension != ".xsl" && extension != ".xml")
                    return ValidationErrorCode.InvalidFileTypeInStyleFolder;
            }

            return null;
        }
    }

    public enum ValidationErrorCode
    {
        RootMustHaveAtLeastOneFolder = 1000,
        InvalidLevel1FolderName = 1001,
        RootFilesNotAllowed = 1002,
        RootMustHaveExactlyOneFolder = 1003,

        SequenceLevelMustHaveExactlyOneFolder = 2000,
        InvalidSequenceFolderName = 2001,
        Level2FilesNotAllowed = 2002,

        InvalidLevel3Folders = 3000,
        M1FolderIsRequired = 3001,
        UtilFolderIsRequired = 3002,
        InvalidLevel3Files = 3003,
        MissingRequiredLevel3Files = 3004,

        M1MustContainOnlySg = 4000,
        FilesNotAllowedInM1 = 4001,
        MissingSgFolder = 4002,
        MissingSgRegionalXml = 4003,

        DtdAndStyleFoldersRequired = 4004,
        UtilFolderMustContainDtd = 4005,
        UtilFolderMustContainStyle = 4006,
        InvalidFolderInUtil = 4007,
        InvalidFilesInUtil = 4008,

        InvalidFileTypeInDtdFolder = 5000,
        InvalidFileTypeInStyleFolder = 5001,
        DtdFolderIsEmpty = 5002,
        StyleFolderIsEmpty = 5003,
    }
}
