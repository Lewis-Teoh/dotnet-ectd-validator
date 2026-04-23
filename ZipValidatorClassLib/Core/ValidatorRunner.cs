using Ionic.Zip;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;

namespace ZipValidatorClassLib
{
    public class ValidatorRunner : IValidatorRunner
    {
        private const string FileInfoMessage = "File Info: {0}";
        private readonly string _filePath;
        private readonly ILogger<ValidatorRunner> _logger;
        private readonly IValidatorProgressWriter? _progressWriter;
        private readonly ValidatorOptions _options;

        private long _fileSizeInByte;
        private readonly List<string> _eCTDID = [];
        private string? _sequenceNumber;
        private string? _packageFileName;

        public long ZipFileSize => _fileSizeInByte;
        public string? SequenceNum => _sequenceNumber;
        public string? PackageFilename => _packageFileName;
        public IReadOnlyList<string> EctdIds => _eCTDID.AsReadOnly();

        public ValidatorRunner(string filePath, ILogger<ValidatorRunner> logger, ValidatorOptions? options = null, IValidatorProgressWriter? progressWriter = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new ValidatorOptions();
            _progressWriter = progressWriter;
        }

        // 22 bytes is empty zip (https://superuser.com/questions/1205503/empty-zip-file-size-shows-22-bytes-size)
        private ZipValidatorErrorTypes? PreValidation(string contentType)
        {
            if (!HelperFunction.IsZipContentType(contentType))
            {
                _logger.LogInformation(Util.ZIP_IS_EMPTY_LOGGER_MSG, _options.EventGuid);
                return ZipValidatorErrorTypes.NotZipFormat;
            }
            if (_fileSizeInByte == 0 || _fileSizeInByte == 22)
            {
                _logger.LogInformation(Util.NOT_A_ZIP_LOGGER_MSG, _options.EventGuid);
                return ZipValidatorErrorTypes.ZipPackageEmpty;
            }
            return null;
        }

        public IReadOnlyCollection<string> GetPdfEntries()
        {
            using FileStream fs = File.OpenRead(_filePath);
            using ZipFile zip = ZipFile.Read(fs);
            return zip.Entries
                .Where(e => Path.GetExtension(e.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && !e.UsesEncryption)
                .Select(x => x.FileName)
                .ToList();
        }

        private void LogFileInfo(FileInfo fileInfo)
        {
            var fileInfoJson = new
            {
                fileInfo.Name,
                fileInfo.FullName,
                fileInfo.Length,
                ContentType = GetContentType(fileInfo),
                fileInfo.CreationTime,
                fileInfo.LastAccessTime,
                fileInfo.LastWriteTime
            };
            string json = JsonConvert.SerializeObject(fileInfoJson, Formatting.Indented);
            _progressWriter?.WriteLine(FileInfoMessage, json);
            _logger.LogInformation(FileInfoMessage, json);
        }

        private static string GetContentType(FileInfo fileInfo)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fileInfo.Name, out string? contentType))
                contentType = "application/octet-stream";
            return contentType;
        }

        public async Task<List<ZipValidatorErrorTypes>> Validate(CancellationToken stoppingToken = default)
        {
            if (_options.EventGuid == Guid.Empty)
                throw new ArgumentException("EventGuid must not be empty.", nameof(_options));

            _logger.LogInformation("Reading eCTD package from: '{filePath}'", _filePath);
            List<ZipValidatorErrorTypes> result = [];
            ZipValidatorErrorTypes? preValidationResult = null;

            if (Path.GetExtension(_filePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                _packageFileName = Path.GetFileName(_filePath);

            try
            {
                using (FileStream fs = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    FileInfo fileInfo = new(fs.Name);
                    string contentType = GetContentType(fileInfo);
                    LogFileInfo(fileInfo);
                    _fileSizeInByte = fileInfo.Length;
                    preValidationResult = PreValidation(contentType);

                    if (preValidationResult.HasValue)
                        result.Add(preValidationResult.Value);
                    else
                        result.AddRange(PerformStructureValidator(fs, stoppingToken));
                }

                if (!preValidationResult.HasValue)
                {
                    if (!_options.CheckPasswordEncryptedPdf)
                    {
                        _logger.LogInformation("Skip pdf password validation.");
                        return result;
                    }

                    ZipValidatorErrorTypes? pdfPwValidationResult;
                    if (_options.ParallelExtract)
                    {
                        IReadOnlyCollection<string> pdfEntries = GetPdfEntries();
                        pdfPwValidationResult = ParallelCheckOpenPasswordPdfInZip(pdfEntries, stoppingToken);
                    }
                    else
                    {
                        using FileStream fs = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using ZipFile zip = ZipFile.Read(fs);
                        List<ZipEntry> pdfZipEntries = zip.Entries
                            .Where(e => Path.GetExtension(e.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && !e.UsesEncryption)
                            .ToList();
                        pdfPwValidationResult = SequentialCheckOpenPasswordPdfInZip(pdfZipEntries, stoppingToken);
                    }

                    if (pdfPwValidationResult.HasValue)
                        result.Add(pdfPwValidationResult.Value);
                }
            }
            catch (InvalidDataException ide)
            {
                _logger.LogInformation("{EventId} | Zip file is corrupted. {ErrorMessage}", _options.EventGuid, ide.Message);
            }
            catch (BadReadException bre)
            {
                result.Add(ZipValidatorErrorTypes.NotZipFormat);
                _logger.LogError("{EventId} | Bad read exception : {ErrorMessage}", _options.EventGuid, bre.Message);
            }
            catch (ZipException zipEx)
            {
                result.Add(ZipValidatorErrorTypes.NotZipFormat);
                _logger.LogError("{EventId} | Zip exception : {ErrorMessage}", _options.EventGuid, zipEx.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }

            return result.DistinctBy(x => x).ToList();
        }

        private ZipValidatorErrorTypes? SequentialCheckOpenPasswordPdfInZip(List<ZipEntry> pdfZipEntries, CancellationToken stoppingToken = default)
        {
            _progressWriter?.WriteLine("Total number of pdf(s) found: {0}. Execute encrypted pdfs validation.", pdfZipEntries.Count);
            IProgressHandle? bar = _progressWriter?.WriteProgressBar();
            int scanned = 0;

            try
            {
                foreach (ZipEntry entry in pdfZipEntries)
                {
                    if (stoppingToken.IsCancellationRequested) return null;

                    _logger.LogInformation(Util.SCANNING_PWD_LOGGER_MSG, _options.EventGuid, entry.FileName);
                    using var pdfStream = new MemoryStream();
                    entry.Extract(pdfStream);

                    if (HelperFunction.IsPdfPasswordProtected(pdfStream))
                    {
                        _logger.LogInformation(Util.ONE_OF_PDF_CONTAIN_PWD_LOGGER_MSG, _options.EventGuid);
                        return ZipValidatorErrorTypes.ContainsPassword;
                    }

                    bar?.SetValue(++scanned * 100 / pdfZipEntries.Count);
                }

                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                throw;
            }
        }

        private List<ZipValidatorErrorTypes> PerformStructureValidator(Stream stream, CancellationToken stoppingToken = default)
        {
            List<ZipValidatorErrorTypes> errTypes = [];

            using ZipFile zip = ZipFile.Read(stream);

            if (HelperFunction.IsPasswordProtected(zip))
            {
                _logger.LogInformation(Util.ZIP_IS_ENCRPYTED_LOGGER_MSG, _options.EventGuid);
                errTypes.Add(ZipValidatorErrorTypes.ContainsPassword);
            }

            if (HelperFunction.HasHiddenFile(zip, out List<string> hiddenFiles))
            {
                _logger.LogInformation(Util.ZIP_CONSIST_HIDDEN_FILE, _options.EventGuid, string.Join(",", hiddenFiles));
                errTypes.Add(ZipValidatorErrorTypes.ContainsHiddenFiles);
            }

            if (HelperFunction.HasZipInZip(zip))
            {
                _logger.LogInformation(Util.ZIP_IN_ZIP_IS_NOT_PERMITTED_LOGGER_MSG, _options.EventGuid);
                errTypes.Add(ZipValidatorErrorTypes.InvalidFolderStructure);
            }

            errTypes.AddRange(ValidateECTDStructure(zip));
            return errTypes;
        }

        private ZipValidatorErrorTypes? ParallelCheckOpenPasswordPdfInZip(IReadOnlyCollection<string> entries, CancellationToken stoppingToken = default)
        {
            var archive = new ParallelZipArchive(_filePath, _logger, _progressWriter) { EventId = _options.EventGuid };
            Dictionary<string, bool> result = archive.Extract(entries, _options.MaxDegreeOfParallelism, _options.MaxFilesPerThread, stoppingToken);
            archive.Dispose();
            return result.Values.Any(x => x) ? ZipValidatorErrorTypes.ContainsPassword : null;
        }

        private List<ZipValidatorErrorTypes> ValidateECTDStructure(ZipFile zip)
        {
            _progressWriter?.WriteLine("Execute eCTD file structure validation.");

            List<ZipValidatorErrorTypes> errTypes = [];
            List<string> listOfPaths = zip.Select(e => e.FileName).ToList();
            string xml = HelperFunction.ListOfPathsToXML(listOfPaths, zip);
            string json = HelperFunction.XMLtoJson(xml);
            JObject fileJObject = JObject.Parse(json);

            var validator = new FolderStructureValidator();
            validator.Validate(fileJObject);

            if (validator.Errors.Count > 0)
            {
                _logger.LogInformation("Validation errors: {Errors}", string.Join(", ", validator.Errors));

                if (validator.Errors.Contains(ValidationErrorCode.SequenceLevelMustHaveExactlyOneFolder))
                    errTypes.Add(ZipValidatorErrorTypes.ContainsMoreThanOneSequence);

                if (validator.Errors.Contains(ValidationErrorCode.RootMustHaveExactlyOneFolder))
                    errTypes.Add(ZipValidatorErrorTypes.ContainsMoreThanOneApplicationFolder);

                errTypes.Add(ZipValidatorErrorTypes.InvalidFolderStructure);
            }

            if (!string.IsNullOrEmpty(validator.ApplicationFolderName))
                _eCTDID.Add(validator.ApplicationFolderName);
            if (!string.IsNullOrEmpty(validator.SequenceFolderName))
                _sequenceNumber = validator.SequenceFolderName;

            _progressWriter?.WriteLine("Completed eCTD file structure validation.");
            return errTypes;
        }
    }
}
