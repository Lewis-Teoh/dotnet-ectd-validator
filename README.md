# dotnet-ectd-validator

A .NET 8 class library for validating Singapore eCTD (electronic Common Technical Document) ZIP packages submitted to the Health Sciences Authority (HSA).

## Overview

This library validates the integrity, structure, and security of eCTD submission packages. It enforces the Singapore HSA eCTD specification, detecting structural issues, prohibited content, and security risks before submission.

## Features

- **Format validation** — confirms the file is a valid, non-corrupted ZIP
- **Security checks** — detects password-protected ZIPs and PDFs, hidden files, and nested ZIPs
- **File extension enforcement** — only `.dtd`, `.pdf`, `.xsl`, `.xsd`, `.xml`, `.txt` are permitted
- **Folder structure validation** — enforces the Singapore eCTD hierarchy (application folder, sequence folder, modules, `util/dtd`, `util/style`, required files)
- **Metadata extraction** — parses the eCTD ID and sequence number from the package
- **Parallel PDF scanning** — optional multi-threaded PDF password detection for large packages

## Installation

The library is published as a NuGet package to GitHub Packages.

```bash
dotnet add package ZipValidatorClassLib
```

## Usage

### Basic validation

```csharp
using Microsoft.Extensions.DependencyInjection;
using ZipValidatorClassLib.Abstractions;
using ZipValidatorClassLib.Core;

var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<IValidatorRunnerFactory, ValidatorRunnerFactory>();

var provider = services.BuildServiceProvider();
var factory  = provider.GetRequiredService<IValidatorRunnerFactory>();

var options = new ValidatorOptions
{
    EventGuid                 = Guid.NewGuid(),
    CheckPasswordEncryptedPdf = true,
    ParallelExtract           = false,
};

IValidatorRunner runner = factory.Create(options);
IReadOnlyList<ZipValidatorErrorTypes> errors = await runner.ValidateAsync("/path/to/submission.zip");

if (errors.Count == 0)
    Console.WriteLine("Package is valid.");
else
    foreach (var error in errors)
        Console.WriteLine(HelperFunction.GetErrorMessage(error));
```

### Parallel PDF scanning

Enable parallel scanning for packages that contain many PDF files:

```csharp
var options = new ValidatorOptions
{
    EventGuid                 = Guid.NewGuid(),
    CheckPasswordEncryptedPdf = true,
    ParallelExtract           = true,
    MaxDegreeOfParallelism    = 4,   // threads
    MaxFilesPerThread         = 10,  // PDFs per batch
};
```

## Configuration

| Property | Type | Default | Description |
|---|---|---|---|
| `EventGuid` | `Guid` | — | Correlation ID for log entries |
| `CheckPasswordEncryptedPdf` | `bool` | `false` | Scan PDFs for password protection |
| `ParallelExtract` | `bool` | `false` | Enable parallel PDF extraction |
| `MaxDegreeOfParallelism` | `int` | `4` | Maximum concurrent threads |
| `MaxFilesPerThread` | `int` | `10` | Files processed per thread batch |

## Expected Package Structure

```
submission.zip
└── e[YYYYMMDD]sg[NNNN]/          ← Application folder (exactly one)
    └── [NNNN]/                   ← Sequence folder (exactly one, 4-digit)
        ├── m1/
        │   └── sg/
        │       └── sg-regional.xml
        ├── m2/ … m5/             ← Optional
        ├── util/
        │   ├── dtd/              ← .dtd / .xsd files only
        │   └── style/            ← .xsl / .xml files only
        ├── index.xml
        ├── index-md5.txt
        └── index.html            ← Optional
```

## Error Codes

| Code | Enum | Description |
|---|---|---|
| 1001 | `HiddenFiles` | Archive contains hidden files |
| 1002 | `PasswordProtected` | ZIP or PDF is password-protected |
| 1003 | `InvalidFolderStructure` | Folder hierarchy does not match the eCTD spec |
| 1004 | `EmptyPackage` | ZIP is empty (22 bytes) |
| 1006 | `NotZipFormat` | File is not a valid ZIP |
| 1007 | `MaliciousContent` | Prohibited content detected (e.g. nested ZIP) |
| 1008 | `MultipleSequences` | More than one sequence folder found |
| 1009 | `ChecksumMismatch` | File integrity check failed |
| 1010 | `MultipleApplications` | More than one application folder found |

Human-readable messages for each code are returned by `HelperFunction.GetErrorMessage(ZipValidatorErrorTypes)`.

## Building

```bash
dotnet restore
dotnet build --configuration Release
```

## Testing

```bash
dotnet test ZipValidatorClassLib.Tests/ZipValidatorClassLib.Tests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName!~HugeECTD"
```

The test project includes 37+ tests covering structure validation, security checks, format errors, and edge cases. Large "HugeECTD" tests are excluded from CI by default due to size.

## CI/CD

| Workflow | Trigger | Action |
|---|---|---|
| `tests.yml` | Push / PR | Runs the test suite |
| `publish.yml` | Push to `main` (ZipValidatorClassLib changes) | Publishes NuGet package to GitHub Packages |

## License

See [LICENSE](LICENSE) for details.
