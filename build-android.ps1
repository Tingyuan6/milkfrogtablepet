param(
    [Parameter(Mandatory=$true)][string]$Jdk,
    [Parameter(Mandatory=$true)][string]$Platform,
    [Parameter(Mandatory=$true)][string]$BuildTools
)
$ErrorActionPreference='Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
$Jdk=(Resolve-Path -LiteralPath $Jdk).Path
$Platform=(Resolve-Path -LiteralPath $Platform).Path
$BuildTools=(Resolve-Path -LiteralPath $BuildTools).Path
$androidJar=Join-Path $Platform 'android.jar'
function Run-Checked([string]$Program,[string[]]$Arguments) {
    & $Program @Arguments
    if($LASTEXITCODE -ne 0){throw ('Android build command failed: '+(Split-Path -Leaf $Program))}
}
New-Item -ItemType Directory -Path 'build/android/gen','build/android/classes','build/android/dex','dist/android','android/app/src/main/res/drawable' -Force | Out-Null
Copy-Item -LiteralPath 'assets/icon.png' -Destination 'android/app/src/main/res/drawable/naifrog.png'
Run-Checked (Join-Path $BuildTools 'aapt2.exe') @('compile','--dir','android/app/src/main/res','-o','build/android/resources.zip')
$manifestText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'android/app/src/main/AndroidManifest.xml'))
$manifestText=$manifestText.Replace('<manifest xmlns:android=', '<manifest package="com.naifrog.pet" xmlns:android=')
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'build/android/AndroidManifest.xml'),$manifestText,[Text.UTF8Encoding]::new($false))
Run-Checked (Join-Path $BuildTools 'aapt2.exe') @('link','-o','build/android/resources.apk','-I',$androidJar,'--manifest','build/android/AndroidManifest.xml','-A','assets/derived','--java','build/android/gen','--min-sdk-version','26','--target-sdk-version','35','--version-code','4','--version-name','0.2.1','-0','wav','--auto-add-overlay','-R','build/android/resources.zip')
$sourceFiles=@(Get-ChildItem -LiteralPath 'android/app/src/main/java','build/android/gen' -Filter '*.java' -Recurse | ForEach-Object FullName)
$sourceList=($sourceFiles | ForEach-Object { '"'+($_ -replace '\\','/')+'"' }) -join "`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'build/android/sources.txt'),$sourceList,[Text.UTF8Encoding]::new($false))
$bootClasspath=(Join-Path $BuildTools 'core-lambda-stubs.jar')+';'+$androidJar
Run-Checked (Join-Path $Jdk 'bin/javac.exe') @('-source','8','-target','8','-encoding','UTF-8','-bootclasspath',$bootClasspath,'-d','build/android/classes','@build/android/sources.txt')
Run-Checked (Join-Path $Jdk 'bin/jar.exe') @('cf','build/android/classes.jar','-C','build/android/classes','.')
Run-Checked (Join-Path $Jdk 'bin/java.exe') @('-cp',(Join-Path $BuildTools 'lib/d8.jar'),'com.android.tools.r8.D8','--lib',$androidJar,'--min-api','26','--output','build/android/dex','build/android/classes.jar')
Copy-Item -LiteralPath 'build/android/resources.apk' -Destination 'build/android/unpacked.apk' -Force
Run-Checked (Join-Path $Jdk 'bin/jar.exe') @('uf','build/android/unpacked.apk','-C','build/android/dex','classes.dex')
Run-Checked (Join-Path $BuildTools 'zipalign.exe') @('-f','-p','4','build/android/unpacked.apk','build/android/aligned.apk')
if(!(Test-Path -LiteralPath 'build/android/dev-key.p12')){
    Run-Checked (Join-Path $Jdk 'bin/keytool.exe') @('-genkeypair','-keystore','build/android/dev-key.p12','-storetype','PKCS12','-storepass','android','-keypass','android','-alias','naifrogdebug','-dname','CN=Naifrog Local Development','-keyalg','RSA','-keysize','2048','-validity','10000')
}
Run-Checked (Join-Path $Jdk 'bin/java.exe') @('-jar',(Join-Path $BuildTools 'lib/apksigner.jar'),'sign','--ks','build/android/dev-key.p12','--ks-key-alias','naifrogdebug','--ks-pass','pass:android','--key-pass','pass:android','--out','dist/android/奶蛙-0.2.1-测试版.apk','build/android/aligned.apk')
Run-Checked (Join-Path $Jdk 'bin/java.exe') @('-jar',(Join-Path $BuildTools 'lib/apksigner.jar'),'verify','--verbose','dist/android/奶蛙-0.2.1-测试版.apk')
Write-Host 'Android package ready: dist/android/奶蛙-0.2.1-测试版.apk'
} finally { Pop-Location }
