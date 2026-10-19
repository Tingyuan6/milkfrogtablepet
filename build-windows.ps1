param([switch]$Test, [switch]$NativeTest)
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 is required.' }
New-Item -ItemType Directory -Path 'build','dist/windows/assets' -Force | Out-Null
if (!(Test-Path -LiteralPath 'assets/derived/manifest.json')) { throw 'Run pnpm install --frozen-lockfile and pnpm run assets first.' }
$sources = @(Get-ChildItem -LiteralPath 'src/Core','src/Graphics','src/Windows' -Filter '*.cs' -Recurse | ForEach-Object FullName)
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll')
& $compiler /nologo /optimize+ /platform:x64 /target:winexe '/out:dist/windows/奶蛙.exe' /win32manifest:app.manifest /win32icon:assets/naifrog.ico @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Windows compilation failed.' }
$manifest = Get-Content -LiteralPath 'assets/derived/manifest.json' -Raw -Encoding UTF8 | ConvertFrom-Json
$keep = New-Object 'System.Collections.Generic.HashSet[string]'
[void]$keep.Add('manifest.json')
foreach($pose in $manifest.poses.PSObject.Properties) { [void]$keep.Add($pose.Value.file) }
foreach($clip in $manifest.clips) { foreach($file in $clip.files) { [void]$keep.Add($file) } }
for($gx=0;$gx -lt 5;$gx++){for($gy=0;$gy -lt 5;$gy++){[void]$keep.Add("gaze/$($gx)_$($gy).png")}}
foreach($relative in $keep) { if([IO.Path]::IsPathRooted($relative) -or $relative.Split('/') -contains '..'){throw 'Unsafe asset path.'} }
foreach($folder in @('assets/derived','dist/windows/assets')) {
    $resolved=(Resolve-Path -LiteralPath $folder).Path
    if(!$resolved.StartsWith($PSScriptRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Asset folder outside project.'}
    foreach($file in Get-ChildItem -LiteralPath $resolved -File -Recurse){$relative=$file.FullName.Substring($resolved.Length+1).Replace('\','/');if(!$keep.Contains($relative)){Remove-Item -LiteralPath $file.FullName}}
}
foreach($relative in $keep){$destination=Join-Path 'dist/windows/assets' $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null;Copy-Item -LiteralPath (Join-Path 'assets/derived' $relative) -Destination $destination -Force}
if (Test-Path -LiteralPath 'assets/naifrog.ico') { Copy-Item -LiteralPath 'assets/naifrog.ico' -Destination 'dist/windows/naifrog.ico' }
Copy-Item -LiteralPath 'README.md' -Destination 'dist/windows/使用说明.md' -ErrorAction SilentlyContinue
if ($Test) {
    $shared = @(Get-ChildItem -LiteralPath 'src/Core','src/Graphics' -Filter '*.cs' -Recurse | ForEach-Object FullName)
    & $compiler /nologo /optimize+ /platform:x64 /target:exe /out:build/CoreTests.exe @references @shared (Join-Path $PSScriptRoot 'tests/CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & 'build/CoreTests.exe' (Join-Path $PSScriptRoot 'assets/derived')
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
}
if ($NativeTest) {
    $probe = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist/windows/奶蛙.exe') -ArgumentList @('--smoke-test','--data-dir',('"' + (Join-Path $PSScriptRoot 'build/native-test') + '"')) -WindowStyle Hidden -PassThru
    $probe.WaitForExit()
    if ($probe.ExitCode -ne 0) { throw 'Native window tests failed; see build/native-test.' }
}
Write-Host 'Windows build ready: dist/windows/奶蛙.exe'
} finally { Pop-Location }
