#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$unity = Join-Path $root "Unity"
$plugins = Join-Path $unity "Assets\Plugins\PlayRuntime"
$stage = Join-Path $unity "_plugin_stage"

dotnet publish (Join-Path $root "MusePSona.Play.Runtime\MusePSona.Play.Runtime.csproj") -c Release -f netstandard2.1 -o $stage --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

foreach ($dir in @(
    (Join-Path $unity "Assets\MuseDashChart.Core"),
    (Join-Path $unity "Assets\MusePSona.Play")
  )) {
  if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
}

New-Item -ItemType Directory -Force -Path $plugins | Out-Null
Get-ChildItem $plugins -File -ErrorAction SilentlyContinue | Remove-Item -Force

$keep = @(
  "MuseDashChart.Core.dll",
  "MusePSona.Play.Runtime.dll",
  "System.Text.Json.dll",
  "System.Text.Encodings.Web.dll",
  "Microsoft.Bcl.AsyncInterfaces.dll",
  "System.IO.Compression.ZipFile.dll",
  "System.Runtime.CompilerServices.Unsafe.dll",
  "System.Memory.dll",
  "System.Buffers.dll",
  "System.Threading.Tasks.Extensions.dll",
  "System.Numerics.Vectors.dll"
)
Get-ChildItem $stage -File -Filter *.dll | ForEach-Object {
  if ($keep -contains $_.Name) {
    Copy-Item $_.FullName (Join-Path $plugins $_.Name) -Force
  }
}

$utf8 = New-Object System.Text.UTF8Encoding $false
$md5 = [System.Security.Cryptography.MD5]::Create()
function Write-PluginMeta([string]$dllPath) {
  $name = [IO.Path]::GetFileName($dllPath)
  $guid = [BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes("musepsona-plugin:" + $name))).Replace("-","").ToLowerInvariant()
  $lines = @(
    "fileFormatVersion: 2",
    "guid: $guid",
    "PluginImporter:",
    "  externalObjects: {}",
    "  serializedVersion: 2",
    "  iconMap: {}",
    "  executionOrder: {}",
    "  defineConstraints: []",
    "  isPreloaded: 0",
    "  isOverridable: 0",
    "  isExplicitlyReferenced: 0",
    "  validateReferences: 0",
    "  platformData:",
    "  - first:",
    "      Any: ",
    "    second:",
    "      enabled: 1",
    "      settings: {}",
    "  - first:",
    "      Editor: Editor",
    "    second:",
    "      enabled: 1",
    "      settings:",
    "        DefaultValueInitialized: true",
    "  userData: ",
    "  assetBundleName: ",
    "  assetBundleVariant: "
  )
  [IO.File]::WriteAllText($dllPath + ".meta", (($lines -join "`n") + "`n"), $utf8)
}
Get-ChildItem $plugins -Filter *.dll | ForEach-Object { Write-PluginMeta $_.FullName }

$csc = "-r:Assets/Plugins/PlayRuntime/MuseDashChart.Core.dll`n-r:Assets/Plugins/PlayRuntime/MusePSona.Play.Runtime.dll`n"
[IO.File]::WriteAllText((Join-Path $unity "Assets\csc.rsp"), $csc.Replace("`r`n","`n"), $utf8)

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Unity plugins + valid .meta"
Get-ChildItem $plugins -Filter *.dll | ForEach-Object { Write-Host ("  " + $_.Name) }
