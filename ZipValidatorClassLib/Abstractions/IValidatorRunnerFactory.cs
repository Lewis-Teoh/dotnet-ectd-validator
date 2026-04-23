namespace ZipValidatorClassLib
{
    public interface IValidatorRunnerFactory
    {
        IValidatorRunner Create(string filePath, ValidatorOptions? options = null, IValidatorProgressWriter? progressWriter = null);
    }
}
