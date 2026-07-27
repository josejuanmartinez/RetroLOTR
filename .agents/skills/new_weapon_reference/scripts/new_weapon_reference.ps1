<#
.SYNOPSIS
Generate a RetroLOTR front-facing, vertical, full-length weapon reference image
in a single gpt-image-2 images.edit call.

.DESCRIPTION
This wrapper samples 3 shipped character card references from
Assets/Art/Cards/Characters and sends them, alongside a prompt describing the
named weapon standing perfectly upright and viewed straight-on with a
transparent background, to gpt-image-2's images.edit endpoint in one call —
no separate sketch/colorize round-trip. If the API still returns an opaque
background, the script falls back to flood-fill alpha keying automatically.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$Description,

    [string]$Out,
    [string]$Model = "gpt-image-2",
    [string]$Size = "576x1536",
    [string]$Quality = "low",
    [string]$ReferenceRoot,
    [int]$ReferenceCount = 3,
    [int]$UploadMaxDim = 512,
    [switch]$DryRun,
    [switch]$Force
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pythonScript = Join-Path $scriptDir "new_weapon_reference.py"

$scriptArgs = @(
    "--name", $Name,
    "--description", $Description,
    "--model", $Model,
    "--size", $Size,
    "--quality", $Quality,
    "--reference-count", $ReferenceCount,
    "--upload-max-dim", $UploadMaxDim
)

if ($Out) {
    $scriptArgs += @("--out", $Out)
}

if ($ReferenceRoot) {
    $scriptArgs += @("--reference-root", $ReferenceRoot)
}

if ($DryRun) {
    $scriptArgs += "--dry-run"
}

if ($Force) {
    $scriptArgs += "--force"
}

& python $pythonScript @scriptArgs
exit $LASTEXITCODE
