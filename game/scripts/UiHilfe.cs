using Godot;

namespace Reichsgilde;

/// <summary>Kleine Helfer für die Oberfläche.</summary>
public static class UiHilfe
{
    /// <summary>
    /// Regel „Befehle nur über den Boten“: Jede Tafel schickt ihre Befehle so an den Kern – nie mit <c>Stadtwirtschaft.Ausfuehren</c>. Im Einzelspiel
    /// wirkt der Befehl sofort und der Rückruf läuft vor der Rückkehr; im Mehrspieler erst im Ziel-Tick, und die Tafel kann bis dahin geschlossen
    /// oder ersetzt sein. Darum läuft der Rückruf nur, wenn <paramref name="besitzer"/> (die Tafel) noch gültig ist. Was vom Ergebnis abhängt
    /// (Meldung, Neuaufbau, Schließen), gehört in den Rückruf, nicht dahinter.
    /// </summary>
    public static void Befehlen(Core.Simulation? simulation, Core.Wirtschaft.Konto? absender, Core.Wirtschaft.Befehl befehl, GodotObject besitzer,
        Action<Core.Wirtschaft.Befehlsergebnis> rueckruf)
    {
        if (simulation is null || absender is null) return;
        simulation.Bote.Einreihen(absender, befehl, e =>
        {
            if (GodotObject.IsInstanceValid(besitzer)) rueckruf(e);
        });
    }

    /// <summary>Die übliche Meldung zu einem Ergebnis: Text bei Erfolg, sonst mit „✗“ davor.</summary>
    public static string MeldungText(Core.Wirtschaft.Befehlsergebnis e) => e.Erfolg ? e.Meldung : "✗ " + e.Meldung;

    /// <summary>
    /// Entfernt alle Kinder eines Knotens – SOFORT aus dem Baum, gelöscht wird am Ende des Bildes.
    ///
    /// Warum nicht nur QueueFree()? QueueFree löscht erst am Ende des Bildes. Bis dahin zählen die alten
    /// Kinder noch bei der Größe mit: Baut man im selben Bild neue Zeilen auf, ist ein Container kurz
    /// doppelt so hoch – und wer genau dann Größe oder Position berechnet, rechnet falsch.
    /// </summary>
    public static void KinderEntfernen(Node eltern)
    {
        foreach (var kind in eltern.GetChildren())
        {
            eltern.RemoveChild(kind);
            kind.QueueFree();
        }
    }

    /// <summary>
    /// Ein Hausname mit kleinem Wappen davor (Rangliste, Rat, Gericht). Ohne Wappen bleibt der Platz frei,
    /// damit die Namen in einer Spalte bündig stehen. Gibt das Label zurück (z. B. für einen Tooltip).
    /// </summary>
    public static Label MitWappen(Container eltern, Core.Dynastie.Wappen? wappen, string text, int groesse, Color? farbe = null)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        eltern.AddChild(zeile);
        zeile.AddChild(WappenBild.Neu(wappen, (int)MathF.Round(groesse * 1.6f)));
        var label = Hauptmenue.Text(zeile, text, groesse, farbe);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    /// <summary>Was ein Betrieb ohne Ware tut – für Kontor und Liegenschaftsfenster.</summary>
    public static string Taetigkeit(string beruf) => beruf switch
    {
        "Dieb" => "Taschendiebe",
        "Geldleiher" => "Geldgeschäfte",
        _ => "Wachdienst",
    };

    /// <summary>
    /// Hängt die Aktionspunkte-Kosten an einen Knopf („Werben · 1 AP") und sperrt ihn, wenn der Vorrat nicht reicht.
    /// Die Kosten hängen nur von der Art des Befehls ab – Betrag oder Ziel spielen keine Rolle.
    /// </summary>
    /// <summary>Völkerbonus als Zusatz für Berufsnamen: „ (+5 %)", „ (−5 %)" oder leer.</summary>
    public static string VolksZusatz(Reichsgilde.Core.Wirtschaft.Stadtwirtschaft w, Reichsgilde.Core.Wirtschaft.Konto konto, string beruf)
    {
        var bonus = w.VolksBonus(konto, beruf);
        return Math.Abs(bonus) < 1e-9 ? "" : $" ({(bonus > 0 ? "+" : "−")}{Reichsgilde.Core.Zahlenformat.Ganz(Math.Abs(bonus) * 100)} %)";
    }

    public static void ApKosten(Button knopf, Reichsgilde.Core.Wirtschaft.Stadtwirtschaft w, Reichsgilde.Core.Wirtschaft.Konto konto,
        Reichsgilde.Core.Wirtschaft.Befehl befehl)
    {
        var kosten = w.ApKosten(konto, befehl);
        if (kosten <= 0) return;
        knopf.Text += $" · {kosten} AP";
        if (konto.Aktionspunkte.Reicht(kosten) || knopf.Disabled) return;
        knopf.Disabled = true;
        knopf.TooltipText = $"Nicht genug Aktionspunkte: {kosten} nötig, {konto.Aktionspunkte.Vorrat} übrig. " +
                            $"Zum Jahreswechsel kommen {w.AktionspunkteJeRunde} dazu.";
    }
}
