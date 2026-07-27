<#
.SYNOPSIS
Turn a reference image into a textured 3D model via the Tripo3D API.

.DESCRIPTION
Wrapper around tripo_image_to_3d_model.py. Runs the pipeline: Smart Topology
mesh generation (Tripo model=P1-20260311, smart_low_poly) at a fixed triangle budget
with a texture derived from the input image, then a final export with the
texture repacked at 2K. Requires TRIPO_API_KEY to be set in the environment
for anything other than -DryRun.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$Image,

    [string]$Out,
    [string]$ModelVersion = "P1-20260311",
    [int]$FaceLimit = 5000,
    [string]$TextureQuality = "detailed",
    [int]$TextureSize = 2048,
    [string]$Format = "FBX",
    [switch]$SkipConvert,
    [string]$BaseUrl,
    [double]$PollInterval = 3.0,
    [double]$Timeout = 600.0,
    [switch]$DryRun,
    [switch]$Force
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pythonScript = Join-Path $scriptDir "tripo_image_to_3d_model.py"

$scriptArgs = @(
    "--name", $Name,
    "--image", $Image,
    "--model-version", $ModelVersion,
    "--face-limit", $FaceLimit,
    "--texture-quality", $TextureQuality,
    "--texture-size", $TextureSize,
    "--format", $Format,
    "--poll-interval", $PollInterval,
    "--timeout", $Timeout
)

if ($Out) {
    $scriptArgs += @("--out", $Out)
}

if ($BaseUrl) {
    $scriptArgs += @("--base-url", $BaseUrl)
}

if ($SkipConvert) {
    $scriptArgs += "--skip-convert"
}

if ($DryRun) {
    $scriptArgs += "--dry-run"
}

if ($Force) {
    $scriptArgs += "--force"
}

& python $pythonScript @scriptArgs
exit $LASTEXITCODE
