$ErrorActionPreference = 'Stop';

if ($isWindows) {
  Write-Host Copy nuget packages
  mkdir "nuget"
  xcopy /Y /I "PYC.Compression\bin\Any CPU\Release\*.nupkg" "nuget\*"
  xcopy /Y /I "PYC.Core\bin\Any CPU\Release\*.nupkg" "nuget\*"
}