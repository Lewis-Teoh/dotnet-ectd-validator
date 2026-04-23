namespace ZipValidatorClassLib
{
	public enum ZipValidatorErrorTypes
	{
		ContainsHiddenFiles = 1001,
		ContainsPassword = 1002,
		InvalidFolderStructure = 1003,
		ZipPackageEmpty = 1004,
		NotZipFormat = 1006,
		ContainsMalicious = 1007,
		ContainsMoreThanOneSequence = 1008,
		CheckSumMismatch = 1009,
		ContainsMoreThanOneApplicationFolder = 1010,

        // error
        UnknownScanningError = 1012,

        // permission related
        EctdIdNotExisted = 2001,
		EctdIdAndEntityIdNotLinked = 2002,
		EctdIdHasDisabled = 2003,
	}
}
