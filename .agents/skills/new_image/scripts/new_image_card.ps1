<#
.SYNOPSIS
Generate RetroLOTR card art with the default generate-then-B/W-postprocess-then-colorify workflow.

.DESCRIPTION
This wrapper samples 3 shipped card references, generates an image, runs the old
strict black-and-white postprocess on that result, then applies a colorify-style
restyle pass to better match the shipped RetroLOTR art.
Use -SinglePass only when you intentionally want to bypass the postprocess and colorify pass.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Out,

    [Parameter(Mandatory = $true)]
    [string]$Prompt,

    [string]$Model = "gpt-5",
    [string]$EditModel = "gpt-image-1.5",
    [string]$Size = "1024x1024",
    [string]$ReferenceRoot,
    [int]$ReferenceCount = 3,
    [switch]$DryRun,
    [switch]$Force,
    [switch]$SinglePass
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pythonScript = Join-Path $scriptDir "new_image_card.py"

$args = @(
    "--out", $Out,
    "--prompt", $Prompt,
    "--model", $Model,
    "--edit-model", $EditModel,
    "--size", $Size,
    "--reference-count", $ReferenceCount
)

if ($ReferenceRoot) {
    $args += @("--reference-root", $ReferenceRoot)
}

if ($DryRun) {
    $args += "--dry-run"
}

if ($Force) {
    $args += "--force"
}

if ($SinglePass) {
    $args += "--single-pass"
}

& python $pythonScript @args
exit $LASTEXITCODE
