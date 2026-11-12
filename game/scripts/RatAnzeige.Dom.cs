using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Ratskammer, Teil 3 (Prediger und Dom, PR 3): der Domprediger. Wer das Amt hat und bis wann, die Bewerbungen, der Knopf „Bewerben“
/// (gesperrt mit dem Grund des Kerns), die Kandidaten der nächsten Wahl mit ihren Punkten und die eigene Stimme vorab. Alles liest
/// <see cref="Reichsgilde.Core.Kirche.Domamt"/>; Bewerbung und Stimme gehen als Befehle (<see cref="DomBewerben"/>, <see cref="DomStimme"/>).
/// </summary>
public partial class RatAnzeige
{
    /// <summary>Die Punkte eines Kandidaten im Schnitt über alle Ratsherren der Stadt (Ansehen, Meinung, Rhetorik, Amtsbonus). Null ohne Ratsherren.</summary>
    internal static double? PunkteImSchnitt(Stadtwirtschaft w, Wahlkandidat k)
    {
        if (k.Haus is null) return null;
        var herren = w.Rat.Aemter.Where(a => a.Ratsherr is not null).Select(a => a.Ratsherr!).ToList();
        return herren.Count == 0 ? null : herren.Average(r => w.Domamt.Punkte(r, k.Haus));
    }

    /// <summary>Wer Domprediger ist, bis wann und mit welcher Konfession (eine Zeile; auch für die Kirchenstube).</summary>
    internal static string DomAmtZeile(Stadtwirtschaft w)
    {
        var dom = w.Domamt;
        return dom.Inhaber is { } inhaber
            ? $"Domprediger: {inhaber.Inhaber} ({dom.Konfession?.Gemeinde()}), Amtszeit bis Ende {dom.Bis}. Der Dom nimmt {Zahlenformat.Ganz(dom.Daten.Anteil * 100)} % der Gemeinde, die Kollekte geht an ihn."
            : "Domprediger: Das Amt ist frei. Der Rat wählt unter den Bewerbern (Prediger mit eigener Kirche) für " + dom.Daten.Amtsjahre + " Jahre; kein Gehalt, nur die Kollekte des Doms.";
    }

    private void DomAbschnitt(Stadtwirtschaft w)
    {
        var dom = w.Domamt;
        if (!dom.IstAktiv) return;
        _inhalt.AddChild(new HSeparator());
        Hauptmenue.Text(_inhalt, "Domprediger", 16);
        Hauptmenue.Text(_inhalt, DomAmtZeile(w), 14, Gold, umbruchBreite: 670);
        if (dom.Inhaber is { } amt && w.Domamt.Vorjahr is { } vorjahr && vorjahr.Glaeubige > 0)
            Hauptmenue.Text(_inhalt, $"Vorjahr im Dom: {Zahlenformat.Ganz(vorjahr.Glaeubige)} Gläubige, Domkollekte {Zahlenformat.Ganz(vorjahr.Kollekte)} {w.Daten.Allgemein.Waehrung} für {amt.Inhaber}.", 12, Grau, umbruchBreite: 670);

        var bewerber = dom.Bewerbungen;
        if (bewerber.Count > 0)
            Hauptmenue.Text(_inhalt, "Beworben haben sich: " + string.Join(", ", bewerber.Select(b => b.Inhaber)) + ".", 13, null, umbruchBreite: 670);

        // Bewerben: gesperrt mit dem Grund des Kerns
        var verboten = dom.BewerbenVerboten(_spieler!);
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(zeile);
        var bewerben = new Button { Text = "Als Domprediger bewerben", FocusMode = FocusModeEnum.None, Disabled = verboten is not null };
        bewerben.Pressed += () => Befehlen(new DomBewerben());
        UiHilfe.ApKosten(bewerben, w, _spieler!, new DomBewerben());
        if (verboten is not null) bewerben.TooltipText = verboten;
        zeile.AddChild(bewerben);
        if (verboten is not null) Hauptmenue.Text(zeile, verboten, 12, Grau, umbruchBreite: 520);

        // Kandidaten der nächsten Wahl mit Punkten
        var kandidaten = dom.Kandidaten();
        if (kandidaten.Count == 0)
        {
            Hauptmenue.Text(_inhalt, dom.IstWahljahr(w.Jahr) ? "Zur Wahl am Jahresende hat sich noch niemand gestellt." : $"Die nächste Wahl ist im Jahr {dom.Bis}.", 12, Grau, umbruchBreite: 670);
            return;
        }
        Hauptmenue.Text(_inhalt, "Kandidaten der Wahl am Jahresende (Punkte im Schnitt über die Ratsherren: Ansehen, Meinung, Rhetorik, Amtsbonus):", 13, null, umbruchBreite: 670);
        foreach (var k in kandidaten)
        {
            var p = PunkteImSchnitt(w, k);
            Hauptmenue.Text(_inhalt, $"• {k.Name}{(k.IstAmtsinhaber ? " (bisheriger Domprediger)" : "")}{(p is { } x ? $" – {Zahlenformat.Kommazahl(x)} Punkte" : "")}", 13, null, umbruchBreite: 670);
        }

        // Die eigene Stimme vorab
        var grund = dom.StimmeVerboten(_spieler!);
        if (grund is not null)
        {
            Hauptmenue.Text(_inhalt, "Eure Stimme: " + grund, 13, Grau, umbruchBreite: 670);
            return;
        }
        var stimmzeile = new HBoxContainer();
        stimmzeile.AddThemeConstantOverride("separation", 10);
        _inhalt.AddChild(stimmzeile);
        Hauptmenue.Text(stimmzeile, "Eure Stimme:", 14);
        var auswahl = new OptionButton { FocusMode = FocusModeEnum.None };
        auswahl.AddItem($"Vorgabe: {dom.VorgabeStimme()?.Name}");
        foreach (var k in kandidaten) auswahl.AddItem(k.IstAmtsinhaber ? $"{k.Name} (bisher)" : k.Name);
        var gewaehlt = dom.StimmeVon(_spieler!);
        auswahl.Selected = gewaehlt is null ? 0 : kandidaten.ToList().FindIndex(k => k.Name == gewaehlt.Name) + 1;
        auswahl.TooltipText = "Ihr legt die Stimme vorab fest und ändert sie bis zur Ratssitzung am Jahresende. Ohne Stimme gilt die Vorgabe: der bisherige Domprediger, sonst der erste Bewerber.";
        auswahl.ItemSelected += index => Befehlen(new DomStimme(index == 0 ? null : kandidaten[(int)index - 1].Name));
        stimmzeile.AddChild(auswahl);
    }
}
