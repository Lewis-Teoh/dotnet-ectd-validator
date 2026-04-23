using Microsoft.Extensions.Logging;

namespace ZipValidatorClassLib
{
    public class ValidatorRunnerFactory : IValidatorRunnerFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public ValidatorRunnerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public IValidatorRunner Create(string filePath, ValidatorOptions? options = null, IValidatorProgressWriter? progressWriter = null)
        {
            var logger = _loggerFactory.CreateLogger<ValidatorRunner>();
            return new ValidatorRunner(filePath, logger, options, progressWriter);
        }
    }
}
