using Ionic.Zip;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ZipValidatorClassLib
{
    [ExcludeFromCodeCoverage]
    public class ParallelZipArchive : IDisposable
    {
        private readonly string _filePath;
        private readonly ILogger _logger;
        private readonly IValidatorProgressWriter? _progressWriter;
        private readonly object _syncRoot = new();

        public Guid EventId { get; set; }

        public ParallelZipArchive(string filePath, ILogger logger, IValidatorProgressWriter? progressWriter = null)
        {
            _filePath = filePath;
            _logger = logger;
            _progressWriter = progressWriter;
        }

        public Dictionary<string, bool> Extract(IEnumerable<string> entries, int maxDop, int maxFilesPerThread, CancellationToken cancellationToken)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDop, CancellationToken = cancellationToken };
            var result = new ConcurrentDictionary<string, bool>();
            int totalItems = entries.Count();

            if (_progressWriter != null)
            {
                _progressWriter.WriteLine("Total number of pdf(s) found: {0}. Execute encrypted pdfs validation.", totalItems);
            }

            IProgressHandle? bar = _progressWriter?.WriteProgressBar(100);
            var batches = entries.Chunk(maxFilesPerThread);

            try
            {
                Parallel.ForEach(batches, parallelOptions, (batch, _) =>
                {
                    ExtractSequentially(batch, result, cancellationToken);
                    lock (_syncRoot)
                    {
                        int currentProgress = totalItems > 0 ? result.Keys.Count * 100 / totalItems : 100;
                        _logger.LogInformation("Scanned percentage {Percentage}", currentProgress);
                        bar?.SetValue(currentProgress);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogTrace("zip extraction cancelled");
            }

            return new Dictionary<string, bool>(result);
        }

        private void ExtractSequentially(IEnumerable<string> entries, ConcurrentDictionary<string, bool> result, CancellationToken cancellationToken)
        {
            using FileStream fs = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using ZipFile zip = ZipFile.Read(fs);

            foreach (string entry in entries)
            {
                if (cancellationToken.IsCancellationRequested) return;

                _logger.LogInformation(Util.SCANNING_PWD_LOGGER_MSG, EventId, entry);
                ZipEntry pdfZipEntry = zip.Entries.Single(x => x.FileName == entry);
                using var pdfStream = new MemoryStream();
                pdfZipEntry.Extract(pdfStream);
                result.TryAdd(entry, HelperFunction.IsPdfPasswordProtected(pdfStream));
            }
        }

        public void Dispose() { }
    }
}
