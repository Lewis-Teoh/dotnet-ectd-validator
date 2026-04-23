namespace ZipValidatorClassLib
{
    public interface IValidatorProgressWriter
    {
        void WriteLine(string format, params object[] args);
        IProgressHandle WriteProgressBar(int maxValue = 100);
    }
}
