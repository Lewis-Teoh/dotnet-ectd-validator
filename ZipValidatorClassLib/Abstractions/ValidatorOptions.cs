namespace ZipValidatorClassLib
{
    public class ValidatorOptions
    {
        public Guid EventGuid { get; set; } = Guid.NewGuid();
        public bool ParallelExtract { get; set; }
        public int MaxDegreeOfParallelism { get; set; } = 4;
        public int MaxFilesPerThread { get; set; } = 10;
        public bool CheckPasswordEncryptedPdf { get; set; }
    }
}
