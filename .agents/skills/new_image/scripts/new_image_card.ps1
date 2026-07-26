<#
.SYNOPSIS
Generate RetroLOTR card art in a single gpt-image-2 images.edit call.

.DESCRIPTION
This wrapper samples 3 shipped card references and sends them, alongside the
art brief and RetroLOTR style block, to gpt-image-2's images.edit endpoint in
one call — no separate sketch/B&W/colorize round-trip.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Out,

    [Parameter(Mandatory = $true)]
    [string]$Prompt,

    [string]$CardName,
    [string]$Model = "gpt-image-2",
    [string]$Size = "1024x1024",
    [string]$Quality = "high",
    [string]$ReferenceRoot,
    [int]$ReferenceCount = 3,
    [int]$UploadMaxDim = 512,
    [switch]$DryRun,
    [switch]$Force
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pythonScript = Join-Path $scriptDir "new_image_card.py"

$scriptArgs = @(
    "--out", $Out,
    "--prompt", $Prompt,
    "--model", $Model,
    "--size", $Size,
    "--quality", $Quality,
    "--reference-count", $ReferenceCount,
    "--upload-max-dim", $UploadMaxDim
)

if ($CardName) {
    $scriptArgs += @("--card-name", $CardName)
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
