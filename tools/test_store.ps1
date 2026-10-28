param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
if(!$OutputDirectory){$OutputDirectory=Join-Path $project ('evidence/store-tests-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$source=[IO.File]::ReadAllText((Join-Path $project 'src/LibraryStore.cs'))+[Environment]::NewLine+[IO.File]::ReadAllText((Join-Path $project 'tests/StoreTests.cs')).Replace('using SinglePlayerLibrary;','')
# Keep compilation units separate so using directives remain legal.
$files=@((Join-Path $project 'src/LibraryStore.cs'),(Join-Path $project 'tests/StoreTests.cs'))
Add-Type -Path $files -OutputAssembly (Join-Path $OutputDirectory 'StoreTests.dll')
$asm=[Reflection.Assembly]::LoadFrom((Join-Path $OutputDirectory 'StoreTests.dll'))
$result=$asm.GetType('StoreTests').GetMethod('Run').Invoke($null,([object[]]@([string](Join-Path $OutputDirectory 'data'))))
$result | Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.txt')
$result
