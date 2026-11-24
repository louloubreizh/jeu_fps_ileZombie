param([switch]$SeedVendorAssets)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$version = (Select-String -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$unity = $env:UNITY_EDITOR_PATH
if (!$unity) {
    $unity = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if (!(Test-Path -LiteralPath $unity -PathType Leaf)) {
    throw "Unity $version not found at $unity."
}

if ($SeedVendorAssets) {
    $cache = $env:ISLAND_ASSETS_CACHE
    if (!$cache) {
        $cache = Join-Path $env:LOCALAPPDATA 'IleUnity/VendorAssets'
    }
    foreach ($folder in @('Cowsins', 'NatureManufacture Assets', 'TextMesh Pro')) {
        $source = Join-Path $cache $folder
        $destination = Join-Path (Join-Path $project Assets) $folder
        if (!(Test-Path -LiteralPath "$source.meta" -PathType Leaf)) {
            throw "Missing $source.meta in the runner cache."
        }
        robocopy.exe $source $destination /E /XO /R:2 /W:2 /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -ge 8) {
            throw "Could not copy $folder from the runner cache."
        }
        Copy-Item -LiteralPath "$source.meta" -Destination "$destination.meta" -Force
    }
}

$logDirectory = Join-Path $project 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$log = Join-Path $logDirectory 'WebGL.log'
$env:ISLAND_WEBGL_OUTPUT = Join-Path $project 'Builds/WebGL'
$method = 'IslandBuild.BuildWeb'
if ($SeedVendorAssets) {
    $method = 'IslandBuild.BuildFromSource'
}
$arguments = '-batchmode -nographics -quit -projectPath "{0}" -buildTarget WebGL -executeMethod {1} -logFile "{2}"' -f $project, $method, $log
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $log) {
        Select-String -LiteralPath $log -Pattern 'error CS\d+|BuildFailedException|Shader error|Failed running|wasm-ld: error|emcc: error' | Select-Object -Last 25
    }
    throw "Unity exited with code $($process.ExitCode). See $log on the runner."
}
if (!(Test-Path -LiteralPath "$env:ISLAND_WEBGL_OUTPUT/index.html" -PathType Leaf)) {
    throw 'Unity did not generate the WebGL entry point.'
}
$bytes = (Get-ChildItem -LiteralPath $env:ISLAND_WEBGL_OUTPUT -Recurse -File | Measure-Object Length -Sum).Sum
if ($bytes -gt 1000000000) {
    throw "The WebGL site exceeds the GitHub Pages limit ($bytes bytes)."
}
Write-Output "WebGL ready for GitHub Pages ($bytes bytes)."
exit 0
