$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$brand = Join-Path $root 'brand'
$assets = Join-Path $root 'assets'
$dist = Join-Path $root 'dist'
$tools = Join-Path $root 'build_tools'
New-Item -ItemType Directory -Force $brand, $assets, $dist, $tools | Out-Null

# Assets de marca: no se guardan en el repo, se bajan del servidor público.
$eye = Join-Path $brand 'eye-ninja.png'
$loading = Join-Path $brand 'loading.jpg'
if (-not (Test-Path $eye)) {
    Invoke-WebRequest 'https://nso.173-249-31-27.sslip.io/brand/eye-ninja.png' -OutFile $eye -UseBasicParsing
}
if (-not (Test-Path $loading)) {
    Invoke-WebRequest 'https://nso.173-249-31-27.sslip.io/brand/loading.jpg' -OutFile $loading -UseBasicParsing
}

$expected = @{
    'eye-ninja.png' = '72E55684F20742C1633C7A3BCC468F6463341D09761F0991E17A0255225C4CD3'
    'loading.jpg'   = '26905932249C475F502E730A130265BA42E373E3290C74DDEB57599393787DB3'
}
foreach ($name in $expected.Keys) {
    $hash = (Get-FileHash (Join-Path $brand $name) -Algorithm SHA256).Hash
    if ($hash -ne $expected[$name]) {
        throw "Hash inesperado para $name`: $hash"
    }
}

python (Join-Path $root 'make_assets.py') $root
if ($LASTEXITCODE -ne 0) { throw "make_assets.py fallo con codigo $LASTEXITCODE" }

# 7-Zip para extraer el juego en el equipo del usuario
$sevenZip = Join-Path $env:ProgramFiles '7-Zip\7z.exe'
if (-not (Test-Path $sevenZip)) {
    $cmd = Get-Command 7z.exe -ErrorAction SilentlyContinue
    if ($cmd) { $sevenZip = $cmd.Source }
}
if (-not (Test-Path $sevenZip)) { throw "No se encontro 7-Zip (7z.exe)" }
$sevenZipDir = Split-Path $sevenZip -Parent
Copy-Item $sevenZip (Join-Path $tools '7z.exe') -Force
Copy-Item (Join-Path $sevenZipDir '7z.dll') (Join-Path $tools '7z.dll') -Force

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $dist 'NinjaSagaPvP-Launcher.exe'

$cscArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    "/out:$out"
    "/win32icon:$(Join-Path $assets 'profile.ico')"
    "/resource:$(Join-Path $assets 'profile.png'),profile.png"
    "/resource:$loading,loading.jpg"
    "/resource:$(Join-Path $tools '7z.exe'),sevenzip.exe"
    "/resource:$(Join-Path $tools '7z.dll'),sevenzip.dll"
    '/r:System.dll'
    '/r:System.Core.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
    '/r:System.Web.Extensions.dll'
    '/r:System.Net.Http.dll'
    (Join-Path $root 'Launcher.cs')
)

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc fallo con codigo $LASTEXITCODE" }

$item = Get-Item $out
Write-Output ("EXE: {0} ({1:N2} MB)" -f $item.FullName, ($item.Length / 1MB))
(Get-FileHash $out -Algorithm SHA256).Hash
