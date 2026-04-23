namespace ZipValidatorClassLib
{
    public interface IValidatorRunner
    {
        long ZipFileSize { get; }
        string? SequenceNum { get; }
        string? PackageFilename { get; }
        IReadOnlyList<string> EctdIds { get; }
        Task<List<ZipValidatorErrorTypes>> Validate(CancellationToken stoppingToken = default);
        IReadOnlyCollection<string> GetPdfEntries();
    }
}
