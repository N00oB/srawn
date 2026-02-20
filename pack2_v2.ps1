param(
  [string]$Root = (Get-Location).Path,
  [string]$Output = "$(Get-Location)\MdbDiffTool_Sources_$(Get-Date -Format 'yyyyMMdd_HHmmss').zip",
  [switch]$ListOnly,
  [switch]$KeepStaging        # оставить временную папку (на отладку)
)

$Root = (Resolve-Path $Root).Path
$staging = Join-Path $env:TEMP ("MdbDiffTool_staging_" + [Guid]::NewGuid())

# Исключаемые каталоги (нам нужны только корневые папки)
$excludeDirs = @(
  '\.vs\', '\bin\', '\obj\', '\icon\', '\Properties\',
  '\TempPE\', '\\Release\\', '\\Debug\\'
)

# 1) Соберём кандидатов - только файлы .cs из корневых папок MdbDiffTool и MdbDiffTool.Core
$all = Get-ChildItem -Path $Root -Recurse -File | Where-Object {
  $path = $_.FullName
  
  # Проверяем, что файл находится в нужных папках первого уровня
  $relativePath = $_.FullName.Replace($Root, '').TrimStart('\')
  $relativePath = $relativePath.Replace('/', '\')
  $isSpreadsheet = $relativePath -like 'MdbDiffTool\Spreadsheet\*'
  
  # Файл должен быть в корне MdbDiffTool или MdbDiffTool.Core
  if (-not $isSpreadsheet -and $relativePath -match '^MdbDiffTool\\.*[\\/]' -and $relativePath -notmatch '^MdbDiffTool\\[^\\]+$') {
    # Если есть вложенные папки - пропускаем
    return $false
  }
  
  if ($relativePath -match '^MdbDiffTool\\.Core\\.*[\\/]' -and $relativePath -notmatch '^MdbDiffTool\\.Core\\[^\\]+$') {
    # Если есть вложенные папки - пропускаем
    return $false
  }
  
  # Только файлы .cs и .csproj
  $ext = $_.Extension.ToLowerInvariant()
  if ($isSpreadsheet) {
    if ($ext -ne '.cs') { return $false }
  } else {
    if ($ext -ne '.cs' -and $ext -ne '.csproj' -and $ext -ne '.slnx' -and $ext -ne '.config') {
      return $false
    }
  }
  
  # Дополнительная проверка пути
  if (-not (
        $path -like "*\MdbDiffTool\*.cs" -or
        $path -like "*\MdbDiffTool\*.csproj" -or
        $path -like "*\MdbDiffTool\*.slnx" -or
        $path -like "*\MdbDiffTool\*.config" -or
        $path -like "*\MdbDiffTool.Core\*.cs" -or
        $path -like "*\MdbDiffTool.Core\*.csproj" -or
        $path -like "*\MdbDiffTool.Core\*.slnx" -or
        $path -like "*\MdbDiffTool.Core\*.config"
      )) {
    return $false
  }
  
  # Проверяем, что файл действительно в корне нужных папок
  if (-not $isSpreadsheet) {
    $parentDir = Split-Path $_.Directory.Name -Leaf
    if ($parentDir -ne 'MdbDiffTool' -and $parentDir -ne 'MdbDiffTool.Core') {
      return $false
    }
  }
  
  # Исключаем каталоги
  foreach($d in $excludeDirs) { 
    if($path -like ("*" + $d + "*")) { 
      return $false 
    } 
  }
  
  return $true
}

if(-not $all) { 
  Write-Host "Не найдено подходящих файлов." -ForegroundColor Yellow
  Write-Host "Убедитесь, что папки MdbDiffTool и MdbDiffTool.Core находятся в текущей директории:" -ForegroundColor Yellow
  Write-Host "  $Root" -ForegroundColor Yellow
  exit 1 
}

# 2) Показать список (относительные пути)
Push-Location $Root
$relative = $all | ForEach-Object { 
  $relPath = (Resolve-Path -Relative $_.FullName).TrimStart('.\')
  # Корректируем путь для staging
  if ($relPath.StartsWith("MdbDiffTool\")) {
    $relPath
  } elseif ($relPath.StartsWith("MdbDiffTool.Core\")) {
    $relPath
  }
}

if($ListOnly){
  Write-Host "Файлы, которые попадут в архив:" -ForegroundColor Cyan
  $relative | Sort-Object | ForEach-Object { Write-Host "  $_" }
  Pop-Location
  exit 0
}

# 3) Копируем в staging, сохраняя структуру
New-Item -ItemType Directory -Path $staging | Out-Null
foreach($rel in $relative){
  $src = Join-Path $Root $rel
  $dst = Join-Path $staging $rel
  $dstDir = Split-Path $dst -Parent
  New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
  Copy-Item -LiteralPath $src -Destination $dst -Force
}
Pop-Location

# 4) Пакуем staging-папку целиком — структура сохранится
if(Test-Path $Output) { Remove-Item $Output -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $Output -Force

Write-Host "Готово! Архив создан: $Output" -ForegroundColor Green
if(-not $KeepStaging) { 
  Remove-Item $staging -Recurse -Force 
} else { 
  Write-Host "Staging папка сохранена: $staging" -ForegroundColor Yellow
}