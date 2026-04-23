using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using ZipValidatorClassLib;

namespace ZipValidatorClassLib.Tests
{
    public class ValidatorResponse
    {
        public string? PackageFileName { get; set; }
        public List<string> ApplicationNumber { get; set; }
        public string? SequenceNumber { get; set; }
        public long FileSize { get; set; }
        public List<int> ErrorCodesResponses { get; set; }
        public ValidatorResponse()
        {
            ErrorCodesResponses = [];
            ApplicationNumber = [];
        }
    }

    internal record EctdIdRecord(string Id, string CompanyUen, bool IsActive);
    internal record CompanyRecord(string EntityID_RegNo, List<EctdIdRecord> EctdIdentifiers);

    public class ZipValidatorUnitTest(ITestOutputHelper output)
    {
        public const string TEST_FILE_BASE_PATH = ".\\EctdPackageTestFile";
        private readonly ITestOutputHelper _output = output;
        private readonly Dictionary<string, Tuple<bool, string?>> _eCTDIDMapping = new()
        {
            { "e20230801sg1234", new Tuple<bool, string?>(true, "A12345678") },
            { "e20230804sg5678", new Tuple<bool, string?>(true, "C12345679") },
            { "e20201231sg0110", new Tuple<bool, string?>(false, "B12345678") },
            { "e20240306sg0010", new Tuple<bool, string?>(false, "B12345678") },
        };

        private static string GetMimeTypeForFileExtension(string filePath)
        {
            const string DefaultContentType = "application/octet-stream";

            var provider = new FileExtensionContentTypeProvider();

            if (!provider.TryGetContentType(filePath, out string? contentType))
            {
                contentType = DefaultContentType;
            }

            return contentType;
        }

        private ServiceProvider ConfigureService()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging((builder) => builder.AddXUnit(_output));
            serviceCollection.AddScoped<IValidatorRunnerFactory, ValidatorRunnerFactory>();

            return serviceCollection.BuildServiceProvider();
        }

        /// <summary>
        /// This function logic should mock as similar as JobService.cs VerifyEctdIdEntityId
        /// </summary>
        public static async Task<(ZipValidatorErrorTypes? ErrorType, string? VerifiedEctdId)> VerifyEctdIdEntityId(
           string ectdId,
           string entityId)
        {
            List<EctdIdRecord> ectdIdentifierModels =
            [
                new EctdIdRecord("e20230801sg1234", "A12345678", true),
                new EctdIdRecord("e20230804sg5678", "C12345679", true),
                new EctdIdRecord("e20201231sg0110", "B12345678", false),
                new EctdIdRecord("e20240306sg0010", "B12345678", false),
            ];

            List<CompanyRecord> companyModels =
            [
                new("A12345678", [new EctdIdRecord("e20230801sg1234", "A12345678", true)]),
                new("C12345679", [new EctdIdRecord("e20230804sg5678", "C12345679", true)]),
                new("B12345678", [
                    new EctdIdRecord("e20201231sg0110", "B12345678", false),
                    new EctdIdRecord("e20240306sg0010", "B12345678", false)
                ]),
            ];

            EctdIdRecord? ectdIdentifier = ectdIdentifierModels.FirstOrDefault(x => x.Id == ectdId);
            CompanyRecord? company = companyModels.FirstOrDefault(x => x.EntityID_RegNo == entityId);

            if (ectdIdentifier == null)
            {
                return (ZipValidatorErrorTypes.EctdIdNotExisted, null);
            }

            if (!ectdIdentifier.IsActive)
            {
                return (ZipValidatorErrorTypes.EctdIdHasDisabled, null);
            }

            if (company == null || !company.EctdIdentifiers.Any(e => e.Id == ectdId))
            {
                return (ZipValidatorErrorTypes.EctdIdAndEntityIdNotLinked, null);
            }

            // If all checks pass, return the verified eCTD ID
            return await Task.FromResult<(ZipValidatorErrorTypes?, string?)>((null, ectdId));
        }

        private async Task ReadFromLocalFilePath(
            List<string> filePaths,
            ValidatorResponse response,
            string? entityId = null,
            Dictionary<string, Tuple<bool, string?>>? mapping = null,
            bool parallelismCheckPasswordPdf = false)
        {
            var path = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location.Substring(0, Assembly.GetEntryAssembly()!.Location.IndexOf("bin\\")));
            var serviceProvider = ConfigureService();

            foreach (string filePath in filePaths)
            {
                string readFilePath = !Path.IsPathRooted(filePath) ? path + filePath : filePath;
                var factory = serviceProvider.GetRequiredService<IValidatorRunnerFactory>();
                var options = new ValidatorOptions
                {
                    EventGuid = Guid.NewGuid(),
                    ParallelExtract = false,
                    CheckPasswordEncryptedPdf = true
                };
                var runner = factory.Create(filePath, options);

                string contentType = GetMimeTypeForFileExtension(filePath);
                List<ZipValidatorErrorTypes> errs = await runner.Validate(CancellationToken.None);
                response.FileSize = runner.ZipFileSize;
                response.PackageFileName = runner.PackageFilename;
                response.SequenceNumber = runner.SequenceNum;
                response.ApplicationNumber = runner.EctdIds.ToList();

                if (!string.IsNullOrEmpty(entityId))
                {
                    var (errorType, verifiedEctdId) = await VerifyEctdIdEntityId(runner.EctdIds.First(), entityId);
                    if (errorType.HasValue)
                    {
                        errs.Add(errorType.Value);
                    }
                }

                response.ErrorCodesResponses.AddRange(errs.Select(x => (int)x).ToList());
            }
        }

        [Theory]
        [InlineData($"{TEST_FILE_BASE_PATH}\\double-application.zip", "1003:1010", "double-application.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_password.zip", "1002:1003:1008", "e20230801sg1234_password.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\sequence-folder-only.zip", "1003", "sequence-folder-only.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230804sg1234_two.zip", "1010:1003", "e20230804sg1234_two.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_fail.zip", "1008:1003", "e20230801sg1234_fail.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\hiddenfiles.zip", "1001", "hiddenfiles.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\pdfpasswordopen.zip", "1002", "pdfpasswordopen.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230804sg5678.rar", "1006")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\notazipfile.txt", "1006")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\notazipfile.docx", "1006")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\txtaszip.zip", "1006", "txtaszip.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\docxaszip.zip", "1003", "docxaszip.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\rarnotzip.zip", "1006", "rarnotzip.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\sequence-is-empty.zip", "1003", "sequence-is-empty.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-1.zip", "1003", "structure-1.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-2.zip", "1003", "structure-2.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-3.zip", "1003", "structure-3.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-4.zip", "1003", "structure-4.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-5a.zip", "1003", "structure-5a.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-5b.zip", "1003", "structure-5b.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-6a.zip", "1003", "structure-6a.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-6b.zip", "1003", "structure-6b.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-6c.zip", "1003", "structure-6c.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\structure-7.zip", "1003", "structure-7.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20201007sg0111_0001.zip", "1003", "e20201007sg0111_0001.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20000101sg0001.zip", "1003", "e20000101sg0001.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\us-ectd.zip", "1003", "us-ectd.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\doublezip.zip", "1003", "doublezip.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\emptypackage-1.zip", "1004", "emptypackage-1.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\emptypackage-2.zip", "1004", "emptypackage-2.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\zip-in-zip.zip", "1003", "zip-in-zip.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20250227sg7416_consist_file_in_seq_dir.zip", "1003", "e20250227sg7416_consist_file_in_seq_dir.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0001_txt_file_in_dtd.zip", "1003", "e20230801sg1234_0001_txt_file_in_dtd.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0001_txt_file_in_style.zip", "1003", "e20230801sg1234_0001_txt_file_in_style.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0001_dtd_is_empty.zip", "1003", "e20230801sg1234_0001_dtd_is_empty.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0001_style_is_empty.zip", "1003", "e20230801sg1234_0001_style_is_empty.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20250414sg7418-0015.zip", "1003", "e20250414sg7418-0015.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20250414sg7418-0013A.zip", "1003", "e20250414sg7418-0013A.zip")]
        public async Task ECTDPackageTest_Returns_FailedErrorCode(string filePath, string errorCode, string? packageFilename = null)
        {
            List<int> expectedSorted = errorCode.Split(":").Select(x => int.Parse(x)).Order().ToList();
            ValidatorResponse response = new();
            await ReadFromLocalFilePath(new List<string> { filePath }, response);
            List<int> errorCodesArr = errorCode.Split(":").Select(x => int.Parse(x)).ToList();
            Assert.True(response.ErrorCodesResponses.Any());
            var actual = response.ErrorCodesResponses.Select(x => x).Order().ToList();
            Assert.Equal(packageFilename, response.PackageFileName);
            Assert.Equal(expectedSorted, actual);
        }

        [Theory]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0001.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0002.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0003.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0004.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230804sg5678_0001.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\pdfsecurity-signature-form.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\pdfpasswordmodify.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20201231sg0110_0001.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20240306sg0010-0003-early.zip")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20240306sg0010.0003.early.zip")]
        public async Task ECTDPackageTest_Returns_SuccessNoErrorCode(string filePath)
        {
            ValidatorResponse response = new();
            await ReadFromLocalFilePath(new List<string> { filePath }, response);
            Assert.False(response.ErrorCodesResponses.Count != 0);
            Assert.Empty(response.ErrorCodesResponses.Select(x => x).ToList());
            Assert.Equal(filePath.Split("\\").Last(), response.PackageFileName);
        }

        [Theory]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0005.zip", "e20240911sg0002-0005.zip")]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0004.zip", "e20240911sg0002-0004.zip")]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0003.zip", "e20240911sg0002-0003.zip")]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0002.zip", "e20240911sg0002-0002.zip")]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0001.zip", "e20240911sg0002-0001.zip")]
        public async Task HugeECTDPackageParallelExtractTest_Returns_SuccessNoErrorCode(string filePath, string filename)
        {
            ValidatorResponse response = new();
            await ReadFromLocalFilePath([filePath], response, null, mapping: null, true);
            Assert.False(response.ErrorCodesResponses.Count != 0);
            Assert.Empty(response.ErrorCodesResponses.Select(x => x).ToList());
            Assert.Equal(filename, response.PackageFileName);
        }

        [Theory]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0005.zip", "e20240911sg0002-0005.zip")]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0004.zip", "e20240911sg0002-0004.zip")]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0003.zip", "e20240911sg0002-0003.zip")]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0002.zip", "e20240911sg0002-0002.zip")]
        [InlineData($"C:\\Users\\lewis.teoh\\Desktop\\eCTD Test Files\\e20240911sg0002-0001.zip", "e20240911sg0002-0001.zip")]
        public async Task HugeECTDPackageSequentialExtractTest_Returns_SuccessNoErrorCode(string filePath, string filename)
        {
            ValidatorResponse response = new();
            await ReadFromLocalFilePath([filePath], response, null, mapping: null, false);
            Assert.False(response.ErrorCodesResponses.Count != 0);
            Assert.Empty(response.ErrorCodesResponses.Select(x => x).ToList());
            Assert.Equal(filename, response.PackageFileName);
        }

        [Theory]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0001.zip", "A12345678")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0002.zip", "A12345678")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0003.zip", "A12345678")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230801sg1234_0004.zip", "A12345678")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\pdfpasswordmodify.zip", "A12345678")]
        public async Task VerifyEctdIdEntityIdOnEctdPackage_Returns_SuccessNoErrorCode(string filePath, string entityId)
        {
            ValidatorResponse response = new();
            await ReadFromLocalFilePath([filePath], response, entityId, mapping: _eCTDIDMapping, false);
            Assert.Empty(response.ErrorCodesResponses.Select(x => x).ToList());
            Assert.NotNull(response.PackageFileName);
        }

        [Theory]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20201231sg0110_0001.zip", "B12345678", "2003")]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20240306sg0010-0003-early.zip", "B12345678", "2003")]
        public async Task VerifyEctdIdEntityIdOnEctdPackage_Returns_EctdIdIsDisabled(string filePath, string entityId, string errorCode)
        {
            List<int> expected = errorCode.Split(":").Select(x => int.Parse(x)).ToList();
            ValidatorResponse response = new();
            await ReadFromLocalFilePath([filePath], response, entityId, mapping: _eCTDIDMapping, false);
            Assert.NotNull(response.PackageFileName);
            Assert.True(response.ErrorCodesResponses.Count != 0);
            Assert.Equal(expected, response.ErrorCodesResponses);
        }

        [Theory]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20230804sg5678_0001.zip", "A12345679", "2002")]
        public async Task VerifyEctdIdEntityIdOnEctdPackage_Returns_EctdIdNotExist(string filePath, string entityId, string errorCode)
        {
            List<int> expected = errorCode.Split(":").Select(x => int.Parse(x)).ToList();
            ValidatorResponse response = new();
            await ReadFromLocalFilePath([filePath], response, entityId, mapping: _eCTDIDMapping, false);
            Assert.NotNull(response.PackageFileName);
            Assert.True(response.ErrorCodesResponses.Count != 0);
            Assert.Equal(expected, response.ErrorCodesResponses);
        }

        [Theory]
        [InlineData($"{TEST_FILE_BASE_PATH}\\e20000101sg0001.zip", "A12345679", "1003:2001")]
        public async Task VerifyEctdIdEntityIdOnEctdPackage_Returns_EctdIdNotExisted(string filePath, string entityId, string errorCode)
        {
            List<int> expected = errorCode.Split(":").Select(x => int.Parse(x)).ToList();
            ValidatorResponse response = new();
            await ReadFromLocalFilePath([filePath], response, entityId, mapping: _eCTDIDMapping, false);
            Assert.NotNull(response.PackageFileName);
            Assert.True(response.ErrorCodesResponses.Count != 0);
            Assert.Equal(expected, response.ErrorCodesResponses);
        }
    }
}
