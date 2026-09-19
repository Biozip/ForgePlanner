<#
    Кладёт собранный плагин в игру.

    Отдельный скрипт, а не строчка в README, по одной причине: пока Valheim
    запущен, BepInEx держит Forgeplan.dll открытым, и копирование падает с
    «Permission denied». Ошибка выглядит как проблема с правами на Program
    Files и уводит не туда, поэтому здесь она проверяется явно.

        .\install.ps1                 поставить
        .\install.ps1 -SelfTest       поставить и включить самопроверку
        .\install.ps1 -NoSelfTest     поставить и выключить её
        .\install.ps1 -SelfTest -Launch   ... и запустить игру
        .\install.ps1 -Wait           дождаться выхода из игры и поставить
#>
param(
    [string] $ValheimDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [switch] $SelfTest,
    [switch] $NoSelfTest,
    [switch] $Launch,
    [switch] $Wait
)

$ErrorActionPreference = 'Stop'

$dll = Join-Path $PSScriptRoot 'bin\Release\Forgeplan.dll'
if (-not (Test-Path $dll)) {
    throw "Не собрано: $dll. Сначала dotnet build -c Release"
}
$plugins = Join-Path $ValheimDir 'BepInEx\plugins'
if (-not (Test-Path $plugins)) {
    throw "BepInEx не установлен: нет $plugins. Распакуйте BepInExPack_Valheim в корень игры."
}

# Игра держит плагин открытым, подменить его на лету нельзя.
$proc = Get-Process valheim -ErrorAction SilentlyContinue
if ($proc) {
    if (-not $Wait) {
        throw "Valheim запущен (pid $($proc.Id)). Закройте игру — или запустите с -Wait."
    }
    Write-Host "Жду выхода из игры (pid $($proc.Id))..."
    $proc.WaitForExit()
    # Unity отпускает файлы не мгновенно.
    Start-Sleep -Seconds 2
}

Copy-Item $dll (Join-Path $plugins 'Forgeplan.dll') -Force
$size = (Get-Item (Join-Path $plugins 'Forgeplan.dll')).Length
Write-Host "Forgeplan.dll установлен ($size байт)"

# Старый лог описывает предыдущую сборку. Если его оставить, поиск по нему
# найдёт отчёт от прошлого запуска и выдаст его за новый — так уже было.
$log = Join-Path $ValheimDir 'BepInEx\LogOutput.log'
if (Test-Path $log) {
    Move-Item $log (Join-Path $ValheimDir 'BepInEx\LogOutput.prev.log') -Force
    Write-Host "Старый лог отложен в LogOutput.prev.log"
}

# SelfTest стоит времени и сотен строк в лог при каждом входе в мир —
# держать её включённой постоянно незачем.
if ($SelfTest -or $NoSelfTest) {
    $want = if ($SelfTest) { 'true' } else { 'false' }
    $cfg = Join-Path $ValheimDir 'BepInEx\config\dev.forgeplanner.forgeplan.cfg'
    if (Test-Path $cfg) {
        $text = Get-Content $cfg -Raw -Encoding UTF8
        if ($text -match '(?m)^\s*SelfTest\s*=') {
            $text = $text -replace '(?m)^\s*SelfTest\s*=.*$', "SelfTest = $want"
        } else {
            $text = $text.TrimEnd() + "`r`n`r`n[Debug]`r`n`r`nSelfTest = $want`r`n"
        }
        [System.IO.File]::WriteAllText($cfg, $text, (New-Object System.Text.UTF8Encoding $false))
    } else {
        # Конфига ещё нет — плагин не запускался ни разу. Создаём минимальный:
        # остальные ключи BepInEx допишет сам со значениями по умолчанию.
        $dir = Split-Path $cfg
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
        [System.IO.File]::WriteAllText($cfg,
            "[Debug]`r`n`r`nSelfTest = $want`r`n",
            (New-Object System.Text.UTF8Encoding $false))
    }
    if ($SelfTest) {
        Write-Host "SelfTest включена: отчёт появится в BepInEx\LogOutput.log при входе в мир"
    } else {
        Write-Host "SelfTest выключена"
    }
}

if ($Launch) {
    # ТОЛЬКО через Steam. Запускать valheim.exe напрямую нельзя, даже с
    # steam_appid.txt рядом: персонажи и миры лежат в Steam Cloud (Connected
    # Storage), и мимо клиента игра видит их список, но не может прочитать
    # содержимое. На экране это выглядит как «персонаж и мир пропали».
    # Ничего при этом не теряется, но испуг стоит дороже удобства.
    #
    # Раньше здесь был прямой запуск — его добавили из-за того, что steam://
    # один раз молча не поднял игру. Правильный ответ на это — проверка ниже,
    # а не обход Steam.
    Start-Process 'steam://rungameid/892970'
    Start-Sleep -Seconds 10
    if (Get-Process valheim -ErrorAction SilentlyContinue) {
        Write-Host "Игра запущена через Steam"
    } else {
        Write-Warning ("Steam не поднял игру за 10 секунд. Запустите её из библиотеки " +
                       "Steam вручную — напрямую valheim.exe запускать нельзя, " +
                       "облачные сохранения тогда не читаются.")
    }
}
