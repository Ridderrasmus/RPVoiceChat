param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$buildRoot = Join-Path ([IO.Path]::GetTempPath()) ('rpvoicechat-preview-' + [Guid]::NewGuid().ToString('N'))
$sourceRoot = Join-Path $buildRoot 'source'
$packageRoot = Join-Path $buildRoot 'package'
New-Item -ItemType Directory -Path $buildRoot | Out-Null

# Export only Git source plus local changes: ignored files left by other branch
# layouts must not become implicit C# compile inputs.
git -C $repoRoot archive --format=zip -o "$buildRoot/source.zip" HEAD
if ($LASTEXITCODE -ne 0) { throw 'Git source export failed.' }
Expand-Archive -LiteralPath "$buildRoot/source.zip" -DestinationPath $sourceRoot
$changed = @(git -C $repoRoot diff HEAD --name-only --diff-filter=ACMRT)
$untracked = @(git -C $repoRoot ls-files --others --exclude-standard)
foreach ($relative in ($changed + $untracked)) {
    $target = Join-Path $sourceRoot $relative
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot $relative) -Destination $target
}
foreach ($relative in (git -C $repoRoot diff HEAD --name-only --diff-filter=D)) {
    $target = [IO.Path]::GetFullPath((Join-Path $sourceRoot $relative))
    if (!$target.StartsWith([IO.Path]::GetFullPath($sourceRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Deletion outside exported source refused.'
    }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
}

$version = (Get-Content -LiteralPath "$sourceRoot/modinfo.json" -Raw | ConvertFrom-Json).version
$suffix = if ($Configuration -eq 'Debug') { '-debug' } else { '' }
$releasePath = Join-Path $repoRoot "Releases/rpvoicechat_$version$suffix.zip"
if (Test-Path -LiteralPath $releasePath) { throw "Already exists: $releasePath" }
dotnet publish "$sourceRoot/RPVoiceChat.csproj" -c $Configuration -o $packageRoot --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath "$sourceRoot/modicon.png" -Destination "$packageRoot/modicon.png"
Get-ChildItem -LiteralPath "$packageRoot/assets" -Recurse -Filter '*.json' | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json | Out-Null
}
New-Item -ItemType Directory -Path (Split-Path $releasePath) -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $releasePath)
Write-Output "Release: $releasePath"
Write-Output "Build source and binaries: $buildRoot"
