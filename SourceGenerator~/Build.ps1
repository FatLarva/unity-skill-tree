param(
    [string] $DotNet = 'dotnet',
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Tests/FatLarva.SkillTree.Generator.Tests.csproj'
$arguments = @('run', '--project', $project, '--configuration', 'Release')
if ($NoRestore) { $arguments += '--no-restore' }

# Tests compile the source generator and exercise its generated code with Roslyn.
& $DotNet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Source-generator build or tests failed. The packaged DLL was not changed.' }

$source = Join-Path $PSScriptRoot 'bin/Release/netstandard2.0/FatLarva.SkillTree.Generator.dll'
$destination = Join-Path $PSScriptRoot '../Runtime/Data/Analyzers/FatLarva.SkillTree.Generator.dll'
$directory = Split-Path -Parent $destination
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
if (!(Test-Path -LiteralPath $destination) -or
    (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
    Copy-Item -LiteralPath $source -Destination $destination
}
Write-Output ('Packaged ' + [System.IO.Path]::GetFullPath($destination))
