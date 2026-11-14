#Requires -Version 7.3
# (System.Formats.Tar, which packs the versions for Linux and macOS, needs PowerShell 7.3 or later)

# Builds the release versions of Blyant in the directory "publish":
#   portable    The lightweight version for all platforms. It needs the .NET 10 runtime to be installed,
#               and is started with:  dotnet Blyant.dll
#   win-x64 ... A self-contained single file for each platform. The .NET runtime is included in the file,
#               so nothing has to be installed to run it.
#   languages   The language file pack: a zip with the language files (lang) and a read-me, to be downloaded
#               on its own
# Each version is also packed for a release in "publish/release", named by the version of Blyant: a zip for
# Windows and the portable version (Blyant-1.5.0-win-x64.zip), and a tar.gz for Linux and macOS
# (Blyant-1.5.0-linux-x64.tar.gz), where the program is marked as one that can be run. Each has a folder "Blyant"
# with the files in it. The language pack is Blyant-languages-1.5.0.zip
# The user manual is made from the guides first (tools/manual.py, which needs Python, and Typst for the PDF): the
# Markdown version (Blyant-User-Manual.md) goes in every archive next to the program, and the PDF is a download of
# its own (Blyant-User-Manual-1.5.0.pdf)
# Last, Blyant-1.5.0-checksums.txt lists the SHA256 checksum of each file of the release, so a download can be
# checked: sha256sum -c (Linux), shasum -a 256 -c (macOS), or Get-FileHash (Windows)
# The tests are run first (nothing is published when one fails), and the version must be the same in all the
# places it is set: Version, AssemblyVersion and FileVersion in Blyant.csproj, and DialogText.ApplicationVersion.
# The archives of a version that is published again are replaced, also those of an earlier version of Blyant
# Usage:  ./publish.ps1                     (all versions)
#         ./publish.ps1 portable,win-x64    (one or more versions)
#         ./publish.ps1 -SkipTests          (without running the tests first)
param(
    [string[]]$Platforms = @("portable", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-arm", "osx-x64", "osx-arm64", "languages"),
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "Blyant/Blyant.csproj"
$output = Join-Path $PSScriptRoot "publish"
$release = Join-Path $output "release"
$known = @("portable", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-arm", "osx-x64", "osx-arm64", "languages")

# "portable,win-x64" as one argument (as when started with pwsh -File) is a list too
$Platforms = @($Platforms | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($platform in $Platforms) {
    if ($known -notcontains $platform) { throw "Unknown version: '$platform' (known: $($known -join ', '))" }
}

# The version of Blyant (1.5.0), for the names of the archives: the same in all four places
[xml]$csproj = Get-Content $project
function Get-Property([string]$name) {
    $csproj.Project.PropertyGroup | ForEach-Object { $_.$name } | Where-Object { $_ } | Select-Object -First 1
}
$version = Get-Property "Version"
$shown = [regex]::Match((Get-Content (Join-Path $PSScriptRoot "Blyant/DialogText.cs") -Raw),
    'ApplicationVersion\s*=\s*"v([^"]+)"').Groups[1].Value
$versions = [ordered]@{
    "Version" = $version
    "AssemblyVersion" = (Get-Property "AssemblyVersion") -replace '\.0$', ''
    "FileVersion" = (Get-Property "FileVersion") -replace '\.0$', ''
    "DialogText.ApplicationVersion" = $shown
}
if (-not $version -or ($versions.Values | Where-Object { $_ -ne $version })) {
    throw "The version is not the same everywhere: $(($versions.GetEnumerator() | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', ')"
}

if (-not $SkipTests) {
    Write-Host "Running the tests..."
    dotnet test (Join-Path $PSScriptRoot "Blyant.sln") --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "The tests failed: nothing was published" }
}

# Each folder is emptied first, so files left from earlier versions are not released. A data folder (made when a
# version was tried there) holds settings and lists: it is never deleted, nothing is published. All folders
# are checked before any is emptied
foreach ($platform in $Platforms) {
    if (Test-Path (Join-Path $output $platform "data")) {
        throw "$(Join-Path $output $platform) contains a data folder: move it away first (nothing was deleted)"
    }
}

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

# The user manual, made from the guides: the Markdown version for the archives, and the PDF for the release (the PDF
# of an earlier version is replaced)
$manual = Join-Path $output "manual"
if ($Platforms | Where-Object { $_ -ne "languages" }) {
    Write-Host "Making the user manual..."
    python (Join-Path $PSScriptRoot "tools/manual.py") --pdf --out $manual
    if ($LASTEXITCODE -ne 0) { throw "The user manual could not be made: nothing was published" }
    New-Item -ItemType Directory -Force $release | Out-Null
    Get-ChildItem $release -Filter "Blyant-User-Manual-*.pdf" -File | Remove-Item
    Copy-Item (Join-Path $manual "Blyant-User-Manual.pdf") (Join-Path $release "Blyant-User-Manual-$version.pdf")
}

# Pack the files of a version for a release, in a folder "Blyant": a tar.gz for Linux and macOS (the program
# marked as one that can be run, which a zip made on Windows cannot tell), else a zip
function New-ReleaseArchive([string]$platform, [string]$folder) {
    New-Item -ItemType Directory -Force $release | Out-Null
    $files = Get-ChildItem $folder -Recurse -File
    if ($platform -like "linux-*" -or $platform -like "osx-*") {
        $archive = Join-Path $release "Blyant-$version-$platform.tar.gz"
        if (Test-Path $archive) { Remove-Item $archive }
        $stream = [System.IO.File]::Create($archive)
        try {
            $gzip = [System.IO.Compression.GZipStream]::new($stream, [System.IO.Compression.CompressionLevel]::Optimal)
            $tar = [System.Formats.Tar.TarWriter]::new($gzip, [System.Formats.Tar.TarEntryFormat]::Pax, $false)
            foreach ($file in $files) {
                $name = [System.IO.Path]::GetRelativePath($folder, $file.FullName).Replace('\', '/')
                $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, "Blyant/$name")
                # rwxr-xr-x for the program, rw-r--r-- for the others
                $entry.Mode = [System.IO.UnixFileMode]$(if ($name -eq "Blyant") { 493 } else { 420 })
                $entry.ModificationTime = [DateTimeOffset]$file.LastWriteTimeUtc
                $data = $file.OpenRead()
                try {
                    $entry.DataStream = $data
                    $tar.WriteEntry($entry)
                }
                finally { $data.Dispose() }
            }
            $tar.Dispose()
            $gzip.Dispose()
        }
        finally { $stream.Dispose() }
    }
    else {
        $archive = Join-Path $release "Blyant-$version-$platform.zip"
        if (Test-Path $archive) { Remove-Item $archive }
        $zip = [System.IO.Compression.ZipFile]::Open($archive, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in $files) {
                $name = [System.IO.Path]::GetRelativePath($folder, $file.FullName).Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, "Blyant/$name",
                    [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        }
        finally { $zip.Dispose() }
    }
}

foreach ($platform in $Platforms) {
    Write-Host "Publishing $platform..."
    $folder = Join-Path $output $platform
    if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
    # The archives of this version made before (also of an earlier version of Blyant), so they are not released
    if (Test-Path $release) {
        $old = if ($platform -eq "languages") { "Blyant-languages-*.zip" } else { "Blyant-*-$platform.*" }
        Get-ChildItem $release -Filter $old -File | Remove-Item
    }
    if ($platform -eq "languages") {
        # The zip is named by the version of Blyant (Blyant-languages-1.5.0.zip)
        $lang = Join-Path $PSScriptRoot "Blyant/Lang"
        $staging = Join-Path ([System.IO.Path]::GetTempPath()) ("blyant-languages-" + [guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Force (Join-Path $staging "lang") | Out-Null
        Copy-Item (Join-Path $lang "External/*.txt") (Join-Path $staging "lang")
        Copy-Item (Join-Path $lang "Pack/README.txt") $staging
        New-Item -ItemType Directory -Force $folder | Out-Null
        $pack = Join-Path $folder "Blyant-languages-$version.zip"
        Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $pack
        Remove-Item -Recurse -Force $staging
        New-Item -ItemType Directory -Force $release | Out-Null
        Copy-Item $pack $release -Force
        continue
    }
    if ($platform -eq "portable") {
        dotnet publish $project -c Release `
            -p:UseAppHost=false `
            -p:DebugType=none `
            -o (Join-Path $output $platform) --nologo -v quiet
    }
    else {
        dotnet publish $project -c Release -r $platform --self-contained true `
            -p:PublishSingleFile=true `
            -p:EnableCompressionInSingleFile=true `
            -p:DebugType=none `
            -o (Join-Path $output $platform) --nologo -v quiet
    }
    if ($LASTEXITCODE -ne 0) { throw "Publishing $platform failed" }
    # The user manual next to the program
    Copy-Item (Join-Path $manual "Blyant-User-Manual.md") $folder
    New-ReleaseArchive $platform $folder
}

# The SHA256 checksums of the files of the release of this version, one line each in the form sha256sum reads
# ("<hash>  <name>", lowercase, LF), made again from the files there now (the file of an earlier version is removed)
if (Test-Path $release) {
    Get-ChildItem $release -Filter "Blyant-*-checksums.txt" -File | Remove-Item
    $released = Get-ChildItem $release -File | Where-Object {
        $_.Name -like "Blyant-$version-*" -or $_.Name -eq "Blyant-languages-$version.zip" -or
        $_.Name -eq "Blyant-User-Manual-$version.pdf"
    } | Sort-Object Name
    $lines = $released | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + "  " + $_.Name }
    if ($lines) {
        $sums = Join-Path $release "Blyant-$version-checksums.txt"
        [System.IO.File]::WriteAllText($sums, (($lines -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))
        Write-Host "Checksums: $sums"
    }
}

Get-ChildItem $output -Recurse -File |
    Select-Object @{ n = "File"; e = { $_.FullName.Substring($output.Length + 1) } },
                  @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
