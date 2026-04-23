using Ionic.Zip;
using Newtonsoft.Json.Linq;
using Xunit;
using ZipValidatorClassLib;

namespace ZipValidatorClassLib.Tests
{
    public class HelperFunctionUnitTest
    {
        public const string TEST_FILE_BASE_PATH = ".\\EctdPackageTestFile";

        [Fact]
        public void IsZipContentType_ValidContentType_ReturnsTrue()
        {
            string contentType = "application/zip";
            bool result = HelperFunction.IsZipContentType(contentType);
            Assert.True(result);
        }

        [Fact]
        public void IsZipContentType_InvalidContentType_ReturnsFalse()
        {
            string contentType = "application/pdf";
            bool result = HelperFunction.IsZipContentType(contentType);
            Assert.False(result);
        }

        [Fact]
        public void IsPasswordProtected_ZipWithPassword_ReturnsTrue()
        {
            var stream = new MemoryStream(File.ReadAllBytes($"{TEST_FILE_BASE_PATH}\\protected.zip"));
            using ZipFile zip = ZipFile.Read(stream);
            bool result = HelperFunction.IsPasswordProtected(zip);
            Assert.True(result);
        }

        [Fact]
        public void IsPasswordProtected_ZipWithoutPassword_ReturnsFalse()
        {
            var stream = new MemoryStream(File.ReadAllBytes($"{TEST_FILE_BASE_PATH}\\unprotected.zip"));
            using ZipFile zip = ZipFile.Read(stream);
            bool result = HelperFunction.IsPasswordProtected(zip);
            Assert.False(result);
        }

        [Fact]
        public void HasHiddenFile_ZipWithHiddenFile_ReturnsTrue()
        {
            using var zip = new ZipFile();
            zip.AddEntry("file.txt", "content");
            zip["file.txt"].Attributes = FileAttributes.Hidden;

            bool result = HelperFunction.HasHiddenFile(zip, out var hiddenFiles);

            Assert.True(result);
            Assert.Single(hiddenFiles);
            Assert.Equal("file.txt", hiddenFiles[0]);
        }

        [Fact]
        public void HasHiddenFile_ZipWithoutHiddenFile_ReturnsFalse()
        {
            using var zip = new ZipFile();
            zip.AddEntry("file.txt", "content");

            bool result = HelperFunction.HasHiddenFile(zip, out var hiddenFiles);

            Assert.False(result);
            Assert.Empty(hiddenFiles);
        }

        [Fact]
        public void IsZipContentExtensionValid_ValidExtensions_ReturnsFalse()
        {
            using var zip = new ZipFile();
            zip.AddEntry("file.dtd", "content");
            zip.AddEntry("file.pdf", "content");

            bool result = HelperFunction.IsZipContentExtensionValid(zip);

            Assert.False(result);
        }

        [Fact]
        public void IsZipContentExtensionValid_InvalidExtensions_ReturnsTrue()
        {
            using var zip = new ZipFile();
            zip.AddEntry("file.exe", "content");

            bool result = HelperFunction.IsZipContentExtensionValid(zip);

            Assert.True(result);
        }

        [Theory]
        [InlineData(null, false, null)]
        [InlineData("", false, null)]
        [InlineData("123", false, null)]
        [InlineData("12345", false, null)]
        [InlineData("abcd", false, null)]
        [InlineData("12a4", false, null)]
        [InlineData("1234", true, "1234")]
        public void TryParseSequenceNum_ShouldReturnExpectedResult(string? input, bool expectedResult, string? expectedOutput)
        {
            var result = HelperFunction.TryParseSequenceNum(input, out var seqNum);
            Assert.Equal(expectedResult, result);
            Assert.Equal(expectedOutput, seqNum);
        }

        [Fact]
        public void TryParseSequenceNum_InvalidSequence_ReturnsFalse()
        {
            bool result = HelperFunction.TryParseSequenceNum("001", out var seqNum);
            Assert.False(result);
            Assert.Null(seqNum);
        }

        [Theory]
        [InlineData("", false, "")]
        [InlineData("x20220101sg0001", false, "x20220101sg0001")]
        [InlineData("e20220101sg", false, "e20220101sg")]
        [InlineData("e20220101sgabcd", false, "e20220101sgabcd")]
        [InlineData("e20220101sg12a4", false, "e20220101sg12a4")]
        [InlineData("e20220101sg0001", true, "e20220101sg0001")]
        [InlineData("e20220132sg0001", false, "e20220132sg0001")]
        [InlineData("e20221301sg0001", false, "e20221301sg0001")]
        public void TryParseEctdID_ShouldReturnExpectedResult(string? input, bool expectedResult, string? expectedOutput)
        {
            var result = HelperFunction.TryParseEctdID(input, out var ectdID);
            Assert.Equal(expectedResult, result);
            Assert.Equal(expectedOutput, ectdID);
        }

        [Fact]
        public void IsPdfPasswordProtected_PasswordProtectedPdf_ReturnsTrue()
        {
            var pdfStream = new MemoryStream(File.ReadAllBytes($"{TEST_FILE_BASE_PATH}\\basic-text-protected.pdf"));
            bool result = HelperFunction.IsPdfPasswordProtected(pdfStream);
            Assert.True(result);
        }

        [Fact]
        public void IsPdfPasswordProtected_NotPasswordProtectedPdf_ReturnsFalse()
        {
            var pdfStream = new MemoryStream(File.ReadAllBytes($"{TEST_FILE_BASE_PATH}\\basic-text.pdf"));
            bool result = HelperFunction.IsPdfPasswordProtected(pdfStream);
            Assert.False(result);
        }

        [Fact]
        public void XMLtoJson_ValidXml_ReturnsJson()
        {
            string xml = "<root><element>value</element></root>";
            string json = HelperFunction.XMLtoJson(xml);
            Assert.Equal("{\"root\":{\"element\":\"value\"}}", json);
        }

        [Theory]
        [InlineData(ZipValidatorErrorTypes.ContainsHiddenFiles, "Package scan failed. Your .zip package contains hidden file(s). Please remove hidden files from the package.")]
        [InlineData(ZipValidatorErrorTypes.ContainsPassword, "Package scan failed. Your .zip package requires a password to open and/or contains password protected file(s). Please remove all passwords from the package and/or file(s).")]
        [InlineData(ZipValidatorErrorTypes.InvalidFolderStructure, "Package scan failed. Your .zip package folder structure is incorrect. Please check folder structure again and refer to the Singapore eCTD Specification for guidance.")]
        [InlineData(ZipValidatorErrorTypes.ZipPackageEmpty, "Package scan failed. Your .zip package is empty. Please check your .zip package again.")]
        [InlineData(ZipValidatorErrorTypes.NotZipFormat, "Package scan failed. Your package is either in unknown format or damaged. Please check your package again and ensure that you upload in .zip format only.")]
        [InlineData(ZipValidatorErrorTypes.ContainsMalicious, "Package scan failed. Your .zip package has been flagged as potentially having malicious content. Please conduct a virus and malicious file scan locally, correct the issue and upload a clean package.")]
        [InlineData(ZipValidatorErrorTypes.ContainsMoreThanOneSequence, "Package scan failed. Your .zip package contains more than one sequence. Please upload only one sequence per package.")]
        [InlineData(ZipValidatorErrorTypes.CheckSumMismatch, "Package scan failed. Checksum mismatch in .zip package after the pre-transmission malicious scan. You may attempt to resubmit once. Otherwise, kindly contact HSA for assistance and reference the Transmission ID.")]
        [InlineData(ZipValidatorErrorTypes.ContainsMoreThanOneApplicationFolder, "Package scan failed. Your .zip package contains more than one application. Please upload only one application per package.")]
        [InlineData(ZipValidatorErrorTypes.EctdIdNotExisted, "Package verification failed. The Application Folder does not exist or is not an eCTD ID. Please check the structure and naming of your Application Folder.")]
        [InlineData(ZipValidatorErrorTypes.EctdIdAndEntityIdNotLinked, "Package verification failed. You are not authorized to submit the package. Please check that your CorpPass login is for the correct company and that the Application Folder is a valid eCTD ID (requested and issued through the Portal).")]
        [InlineData(ZipValidatorErrorTypes.EctdIdHasDisabled, "Package verification failed. The eCTD ID has been disabled. Please ensure you are using the correct eCTD ID or contact HSA for assistance.")]
        [InlineData(ZipValidatorErrorTypes.UnknownScanningError, "Package scan failed. An unknown error has occurred during Package Check, please try again later. If the problem persists, please contact HSA for assistance.")]
        public void GetErrorMessage_ShouldReturnCorrectMessage(ZipValidatorErrorTypes errorType, string expectedMessage)
        {
            var result = HelperFunction.GetErrorMessage(errorType);
            Assert.Equal(expectedMessage, result);
        }

        [Fact]
        public void GetErrorMessage_ShouldThrowNotSupportedException_ForUnsupportedErrorType()
        {
            var unsupportedErrorType = (ZipValidatorErrorTypes)9999;
            Assert.Throws<NotSupportedException>(() => HelperFunction.GetErrorMessage(unsupportedErrorType));
        }

        [Fact]
        public void ListOfPathsToXML_ValidPaths_ReturnsXml()
        {
            var listOfPaths = new List<string> { "dir1/file1.txt", "dir1/file2.txt" };
            using var zip = new ZipFile();
            zip.AddEntry("dir1/file1.txt", "content");
            zip.AddEntry("dir1/file2.txt", "content");

            string xml = HelperFunction.ListOfPathsToXML(listOfPaths, zip);

            Assert.Contains("<directory depth=\"0\" json:Array=\"true\"><name>dir1</name>", xml);
            Assert.Contains("<file json:Array=\"true\" encrypted=\"False\" last-modified=", xml);
        }

        [Theory]
        [InlineData(@"{ }", false)]
        [InlineData(@"{ ""root"": [] }", false)]
        [InlineData(@"{ ""root"": [ { ""directory"": ""example"" } ] }", false)]
        [InlineData(@"{ ""root"": [ { ""file"": ""example.txt"" } ] }", true)]
        public void IsFileExistAtRootLevel_ShouldReturnExpectedResult(string json, bool expectedResult)
        {
            var jObject = JObject.Parse(json);
            var result = HelperFunction.IsFileExistAtRootLevel(jObject);
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(new[] { "file.txt", "subdir/file2.txt" }, true)]
        [InlineData(new[] { "subdir/file.txt" }, false)]
        [InlineData(new[] { "anotherfile.txt" }, true)]
        public void IsFileExistAtRootLevel_FileInZip_ShouldReturnExpectedResult(string[] filePaths, bool expectedResult)
        {
            using var zip = new ZipFile();
            foreach (var filePath in filePaths)
                zip.AddEntry(filePath, "content");

            var listOfPaths = zip.Select(e => e.FileName).ToList();
            var xml = HelperFunction.ListOfPathsToXML(listOfPaths, zip);
            var json = HelperFunction.XMLtoJson(xml);

            var result = HelperFunction.IsFileExistAtRootLevel(JObject.Parse(json));
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void IsFileExistAtRootLevel_FileDoesNotExistAtRoot_ReturnsFalse()
        {
            using var zip = new ZipFile();
            zip.AddEntry("subdir/file.txt", "content");
            var xml = HelperFunction.ListOfPathsToXML(zip.Select(e => e.FileName).ToList(), zip);
            bool result = HelperFunction.IsFileExistAtRootLevel(JObject.Parse(HelperFunction.XMLtoJson(xml)));
            Assert.False(result);
        }

        [Fact]
        public void IsFileExistAtRootLevel_FileInZip_ReturnsTrue()
        {
            using var zip = new ZipFile();
            zip.AddEntry("anotherfile.txt", "content");
            var xml = HelperFunction.ListOfPathsToXML(zip.Select(e => e.FileName).ToList(), zip);
            bool result = HelperFunction.IsFileExistAtRootLevel(JObject.Parse(HelperFunction.XMLtoJson(xml)));
            Assert.True(result);
        }
    }
}
