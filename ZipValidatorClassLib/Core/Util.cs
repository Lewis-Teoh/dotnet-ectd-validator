namespace ZipValidatorClassLib
{
	internal class Util
	{
		public const string NOT_A_ZIP_LOGGER_MSG = "{EventId} | Package submitted is not a zip file.";
		public const string ZIP_IS_EMPTY_LOGGER_MSG = "{EventId} | Zip package is empty.";
		public const string ZIP_IS_ENCRPYTED_LOGGER_MSG = "{EventId} | Zip file is encrpyted.";
		public const string ZIP_CONSIST_HIDDEN_FILE = "{EventId} | Zip file consist of hidden file. > {HiddenFiles}";
		public const string SCANNING_PWD_LOGGER_MSG = "{EventId} | Scanning pdf password... {Filename}";
		public const string ONE_OF_PDF_CONTAIN_PWD_LOGGER_MSG = "{EventId} | One of the PDF file contain password.";
		public const string APPLICATION_FOLDER_NOT_FOUND_LOGGER_MSG = "{EventId} | Application folder not found.";
		public const string NO_FILES_IS_PERMITTED_IN_ROOT_LEVEL_LOGGER_MSG = "{EventId} | No file is permitted in root level.";
        public const string NO_FILES_IS_PERMITTED_IN_SEQUENCE_LEVEL_LOGGER_MSG = "{EventId} | No file is permitted in sequence level.";
        public const string SEQUENCE_FOLDER_NOT_FOUND_LOGGER_MSG = "{EventId} | Sequence folder not found.";
		public const string SHOULD_CONTAIN_SINGLE_SEQUENCE_LOGGER_MSG = "{EventId} | Package should only contain single sequence number.";
		public const string EMPTY_SEQUENCE_FOLDER_LOGGER_MSG = "{EventId} | Empty sequence folder.";
		public const string UTIL_NOT_FOUND_LOGGER_MSG = "{EventId} | Util not found.";
		public const string DTD_OR_STYLE_NOT_FOUND_LOGGER_MSG = "{EventId} | Directory dtd/style not found.";
		public const string ONLY_M1_M5_PERMITTED_LOGGER_MSG = "{EventId} | Only m1-m5 is permitted.";
		public const string M1_IS_NOT_FOUND_LOGGER_MSG = "{EventId} | Directory m1 is not found.";
		public const string M_DIRECTORY_NOT_FOUND_LOGGER_MSG = "{EventId} | M directory not found.";
		public const string ONLY_SG_DIRECTORY_IS_PERMITTED_LOGGER_MSG = "{EventId} | Only SG folder is permitted.";
		public const string SG_DIRECTORY_NOT_FOUND_LOGGER_MSG = "{EventId} | SG directory not found.";
		public const string REGIONAL_XML_NOT_FOUND_LOGGER_MSG = "{EventId} | Regional XML not found.";
		public const string ZIP_IN_ZIP_IS_NOT_PERMITTED_LOGGER_MSG = "{EventId} | Zip in zip is not permitted.";
    }
}
