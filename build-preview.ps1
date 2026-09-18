param([switch]$VoiceDiagnostics)

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
$suffix = if ($VoiceDiagnostics) { '-diagnostics' } else { '' }
$releasePath = Join-Path $repoRoot "Releases/rpvoicechat_$version$suffix.zip"
if (Test-Path -LiteralPath $releasePath) { throw "Already exists: $releasePath" }
$diagnosticValue = $VoiceDiagnostics.IsPresent.ToString().ToLowerInvariant()
dotnet publish "$sourceRoot/RPVoiceChat.csproj" -c Release "-p:VoiceDiagnostics=$diagnosticValue" -o $packageRoot --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath "$sourceRoot/modicon.png" -Destination "$packageRoot/modicon.png"
if ($VoiceDiagnostics) {
    Set-Content -LiteralPath "$packageRoot/voice-diagnostics-build.txt" -Value @"
RPVoiceChat $version diagnostic build
Built with -p:VoiceDiagnostics=true
Look for [RPVC-Diagnostics] in client-main.log / server-main.log.
Summaries every 10 seconds contain cumulative totals and peak milliseconds.
playback-drained and capture-gap also include normal speech pauses.
No audio or player identities are recorded. Normal builds omit the counters.
"@
}
Get-ChildItem -LiteralPath "$packageRoot/assets" -Recurse -Filter '*.json' | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json | Out-Null
}
New-Item -ItemType Directory -Path (Split-Path $releasePath) -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $releasePath)
Write-Output "Release: $releasePath"
Write-Output "Build source and binaries: $buildRoot"
