param([Parameter(Mandatory=$true)][string]$Jdk)
$ErrorActionPreference='Stop'
$Jdk=(Resolve-Path -LiteralPath $Jdk).Path
Push-Location -LiteralPath $PSScriptRoot
try {
    node tools/export-fixtures.mjs
    if($LASTEXITCODE -ne 0){throw 'Fixture export failed.'}
    & (Join-Path $Jdk 'bin/javac.exe') -encoding UTF-8 -d 'build/java-tests' 'android/app/src/main/java/com/naifrog/pet/PetEngine.java' 'tests/PetEngineTests.java' 'android/app/src/main/java/com/naifrog/pet/FaceMotion.java' 'tests/FaceMotionTests.java' 'android/app/src/main/java/com/naifrog/pet/PetPlacement.java' 'tests/PetPlacementTests.java'
    if($LASTEXITCODE -ne 0){throw 'Java test compilation failed.'}
    & (Join-Path $Jdk 'bin/java.exe') -cp 'build/java-tests' PetEngineTests 'build/engine-fixtures.properties'
    if($LASTEXITCODE -ne 0){throw 'Java core tests failed.'}
    & (Join-Path $Jdk 'bin/java.exe') -cp 'build/java-tests' FaceMotionTests
    if($LASTEXITCODE -ne 0){throw 'Java facial tests failed.'}
    & (Join-Path $Jdk 'bin/java.exe') -cp 'build/java-tests' PetPlacementTests
    if($LASTEXITCODE -ne 0){throw 'Android placement tests failed.'}
    & (Join-Path $Jdk 'bin/javac.exe') -encoding UTF-8 -d 'build/java-tests' 'android/app/src/main/java/com/naifrog/pet/MouthSurface.java' 'tests/MouthSurfaceTests.java'
    if($LASTEXITCODE -ne 0){throw 'Mouth test compilation failed.'}
    & (Join-Path $Jdk 'bin/java.exe') -cp 'build/java-tests' MouthSurfaceTests
    if($LASTEXITCODE -ne 0){throw 'Mouth tests failed.'}
    & (Join-Path $Jdk 'bin/javac.exe') -encoding UTF-8 -d 'build/java-tests' 'tools/ApkAssembler.java' 'tests/ApkAssemblerTests.java'
    if($LASTEXITCODE -ne 0){throw 'APK regression test compilation failed.'}
    & (Join-Path $Jdk 'bin/java.exe') -cp 'build/java-tests' ApkAssemblerTests
    if($LASTEXITCODE -ne 0){throw 'APK packaging regression tests failed.'}
    & (Join-Path $Jdk 'bin/javac.exe') -encoding UTF-8 -d 'build/java-tests' 'android/app/src/main/java/com/naifrog/pet/FartReaction.java' 'tests/FartReactionTests.java'
    if($LASTEXITCODE -ne 0){throw 'Fart Java compilation failed.'}
    & (Join-Path $Jdk 'bin/java.exe') -cp 'build/java-tests' FartReactionTests
    if($LASTEXITCODE -ne 0){throw 'Fart Java tests failed.'}
} finally { Pop-Location }
