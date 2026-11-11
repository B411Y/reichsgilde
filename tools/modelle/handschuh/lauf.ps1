# lauf.ps1 -Skript <skript.py> -Argumente "<args, Leerzeichen getrennt>" [-Blend <datei.blend>] [-Sek 1500]
# Umgebungsvariablen: BLENDER = Pfad zu blender.exe (5.2.x), RG_MPFB = MPFB-Benutzerprofil (BLENDER_USER_RESOURCES), nur fuer v2_bau/v2_retopo noetig.
# Blender laeuft mit -b (ohne Fenster) auf Idle-Prioritaet. Pfade in -Skript/-Argumente/-Blend absolut uebergeben (Blender startet nicht im Skriptordner).
param([string]$Skript,[string]$Argumente,[string]$Blend="",[int]$Sek=1500)
if($env:RG_MPFB){ $env:BLENDER_USER_RESOURCES=$env:RG_MPFB }
$blender = if($env:BLENDER){ $env:BLENDER } else { "blender" }
$o="$env:TEMP\hs_lauf.txt"
$a=@("-b"); if($Blend){$a+=$Blend}; $a+=@("--python",$Skript,"--")+($Argumente -split ' ' | ? {$_})
$p=Start-Process $blender -ArgumentList $a -RedirectStandardOutput $o -RedirectStandardError "$o.e" -WindowStyle Hidden -PassThru
$p.PriorityClass="Idle"; $p.WaitForExit($Sek*1000)|Out-Null; Get-Content $o | ? {$_ -notmatch '^(INFO|\[INFO|Info:)'}; Get-Content "$o.e" -Tail 15
