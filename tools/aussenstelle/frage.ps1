<#
Fragt vor einem schweren Auftrag, ob die Außenstelle den PC nutzen darf.
Beispiel: powershell -NoProfile -ExecutionPolicy Bypass -File frage.ps1 -Aufgabe "Klicktest" -Minuten 15
Ausgang: 0 = erlaubt, 1 = abgelehnt oder keine Antwort, 2 = KI läuft, 3 = gesperrt.
#>
param(
    [Parameter(Mandatory = $true)][string]$Aufgabe,
    [int]$Minuten = 10
)

. (Join-Path $PSScriptRoot 'gemeinsam.ps1')

$leerlaufGrenzeSekunden = 600
$wartezeitSekunden = 180
$kopf = "Aufgabe: $Aufgabe | etwa $Minuten Min."

# 1. Läuft eine KI auf dem PC, startet die Außenstelle nichts.
$ki = @(Hole-LaufendeKi)
if ($ki.Count -gt 0) {
    $namen = $ki -join ', '
    Schreibe-Log "KI läuft ($namen) | $kopf"
    Write-Output "KI läuft: $namen. Nichts starten."
    exit 2
}

# 2. Notschalter.
if (Ist-Gesperrt) {
    Schreibe-Log "gesperrt | $kopf"
    Write-Output 'Gesperrt (Sperrdatei STOPP). Nichts starten.'
    exit 3
}

# 3. Leerlaufzeit von Windows (letzte Eingabe an Maus oder Tastatur).
if (-not ('LeerlaufHelfer' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LeerlaufHelfer {
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    // Sekunden seit der letzten Eingabe, -1 wenn nicht ermittelbar.
    public static long Sekunden() {
        LASTINPUTINFO info = new LASTINPUTINFO();
        info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        if (!GetLastInputInfo(ref info)) { return -1; }
        uint jetzt = unchecked((uint)Environment.TickCount);
        return (long)(unchecked(jetzt - info.dwTime) / 1000);
    }
}
'@
}

$leerlauf = -1
try { $leerlauf = [LeerlaufHelfer]::Sekunden() } catch { $leerlauf = -1 }
if ($leerlauf -ge $leerlaufGrenzeSekunden) {
    $min = [int][math]::Floor($leerlauf / 60)
    Schreibe-Log "erlaubt (Leerlauf $min Min., ohne Frage) | $kopf"
    Write-Output "Erlaubt: PC seit $min Minuten unbenutzt."
    exit 0
}

# 4. Nutzer fragen.
$script:Ergebnis = 'Zeitablauf'
try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [System.Windows.Forms.Application]::EnableVisualStyles()

    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'Reichsgilde – Anfrage'
    $form.TopMost = $true
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.ControlBox = $false
    $form.MinimizeBox = $false
    $form.MaximizeBox = $false
    $form.ClientSize = New-Object System.Drawing.Size(520, 220)

    $text = New-Object System.Windows.Forms.Label
    $text.Text = "Der Teamleiter von Reichsgilde möchte den PC nutzen: $Aufgabe, etwa $Minuten Minuten."
    $text.Font = New-Object System.Drawing.Font('Segoe UI', 11)
    $text.Location = New-Object System.Drawing.Point(20, 20)
    $text.Size = New-Object System.Drawing.Size(480, 90)
    $form.Controls.Add($text)

    $zaehler = New-Object System.Windows.Forms.Label
    $zaehler.Font = New-Object System.Drawing.Font('Segoe UI', 11, [System.Drawing.FontStyle]::Bold)
    $zaehler.Location = New-Object System.Drawing.Point(20, 115)
    $zaehler.Size = New-Object System.Drawing.Size(480, 28)
    $form.Controls.Add($zaehler)

    $erlauben = New-Object System.Windows.Forms.Button
    $erlauben.Text = 'Erlauben'
    $erlauben.Location = New-Object System.Drawing.Point(90, 160)
    $erlauben.Size = New-Object System.Drawing.Size(150, 40)
    $erlauben.Add_Click({ $script:Ergebnis = 'Erlaubt'; $form.Close() })
    $form.Controls.Add($erlauben)

    $ablehnen = New-Object System.Windows.Forms.Button
    $ablehnen.Text = 'Ablehnen'
    $ablehnen.Location = New-Object System.Drawing.Point(280, 160)
    $ablehnen.Size = New-Object System.Drawing.Size(150, 40)
    $ablehnen.Add_Click({ $script:Ergebnis = 'Abgelehnt'; $form.Close() })
    $form.Controls.Add($ablehnen)

    # Die Eingabetaste lehnt ab: ein versehentlicher Tastendruck erlaubt nichts.
    $form.AcceptButton = $ablehnen
    $form.CancelButton = $ablehnen

    $script:Rest = $wartezeitSekunden
    $zaehler.Text = "Ohne Antwort wird abgelehnt in: {0}:{1:00}" -f [int][math]::Floor($script:Rest / 60), ($script:Rest % 60)

    $klingeln = {
        try { [System.Media.SystemSounds]::Exclamation.Play() } catch { }
        try { [Console]::Beep(880, 250) } catch { }
    }

    $uhr = New-Object System.Windows.Forms.Timer
    $uhr.Interval = 1000
    $uhr.Add_Tick({
        $script:Rest--
        $zaehler.Text = "Ohne Antwort wird abgelehnt in: {0}:{1:00}" -f [int][math]::Floor($script:Rest / 60), ($script:Rest % 60)
        if ($script:Rest -le 0) {
            $script:Ergebnis = 'Zeitablauf'
            $form.Close()
        } elseif ($script:Rest % 20 -eq 0) {
            & $klingeln
        }
    })

    $form.Add_Shown({
        $form.Activate()
        $uhr.Start()
        & $klingeln
    })

    [void]$form.ShowDialog()
    $uhr.Stop()
    $form.Dispose()
} catch {
    $script:Ergebnis = 'Fehler'
    Schreibe-Log "Fehler beim Fenster: $($_.Exception.Message) | $kopf"
}

Schreibe-Log "$($script:Ergebnis) | $kopf"
if ($script:Ergebnis -eq 'Erlaubt') {
    Write-Output 'Erlaubt.'
    exit 0
}
Write-Output "Nicht erlaubt ($($script:Ergebnis))."
exit 1
