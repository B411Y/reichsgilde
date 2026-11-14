using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die kleine Tafel der <b>Stimmrunde</b> vor der Ratssitzung (Ratssitzung PR 8, Entscheidung F5): In den letzten Minuten des Jahres
/// (<see cref="Stadtrat.StimmrundeLaeuft"/>, 21–23 Uhr am letzten Tag) listet sie die Punkte, zu denen der Spieler noch stimmen darf
/// (<see cref="Stadtrat.Stimmrunde"/>), mit der Stimme, die ohne weiteren Befehl gilt. Die Knöpfe schicken die üblichen Befehle über den Boten
/// (<c>StatutStimme</c>, <c>StadtglaubeStimme</c>, <c>PflasterStimme</c>, <c>RatsantragStimme</c>, <c>WahlStimme</c>, <c>DomStimme</c>) – im Mehrspieler
/// wartet so niemand. Am Jahresende hält der Kern die Sitzung in einem Zug ab; die Szene spielt danach nur noch das feste Ergebnis ab.
/// Aussehen nur aus dem Ratskammer-Thema (<see cref="RatStil"/>).
/// </summary>
public partial class StimmrundeAnzeige : Control
{
    private Simulation? _simulation;
    private Konto? _spieler;
    private VBoxContainer _liste = null!;
    private Label _frist = null!;
    /// <summary>Kennzeichen des gezeigten Inhalts – neu aufgebaut wird nur, wenn sich Punkte oder Stimmen ändern.</summary>
    private string _kennung = "";

    /// <summary>„Fertig“ gedrückt: Die Tafel ist zu (die Stimmen bleiben, wie sie sind).</summary>
    public event Action? Geschlossen;

    /// <summary>Ein Befehl wurde abgelehnt oder ausgeführt – Text fürs Protokoll.</summary>
    public event Action<string>? Gemeldet;

    public override void _Ready()
    {
        RatStil.Anwenden(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        var mitte = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore }; // die Tafel mittig im Bild
        AddChild(mitte);
        mitte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var tafel = RatStil.Container<PanelContainer>(mitte, "RatKurzTafel");
        var rand = RatStil.Container<MarginContainer>(tafel, "RatRand");
        var spalte = RatStil.Container<VBoxContainer>(rand, "RatSpalte");
        RatStil.Text(spalte, "Stimmrunde vor der Ratssitzung", "RatTitel", zentriert: true);
        _frist = RatStil.Text(spalte, "", "RatHinweis", umbrechen: true, breite: "kurz_breite");
        _liste = RatStil.Container<VBoxContainer>(spalte, "RatSpalte");
        RatStil.Amtsknopf(spalte, "Fertig", FertigDruecken, "Die Tafel schließen. Wer nicht abstimmt, behält die angezeigte Stimme.");
    }

    /// <summary>„Fertig“: Die Tafel schließt (die Stimmen bleiben, wie sie sind); auch für die Startoption <c>--stimmrundefertig</c>.</summary>
    public void FertigDruecken()
    {
        if (!Visible) return;
        Visible = false;
        Geschlossen?.Invoke();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
        _kennung = "";
    }

    /// <summary>Zeigt die Tafel (sie baut sich bei jedem Bild nur neu auf, wenn sich etwas geändert hat).</summary>
    public void Zeigen()
    {
        _kennung = "";
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible || _simulation?.Wirtschaft is not { } w || _spieler is null) return;
        var punkte = w.Rat.Stimmrunde(_spieler);
        if (punkte.Count == 0)
        {
            Visible = false;
            Geschlossen?.Invoke();
            return;
        }
        var kennung = string.Join("|", punkte.Select(p => $"{p.Nummer}:{p.Thema}:{p.Text}:{Eigene(p).Ja}:{Eigene(p).Kandidat}:{Eigene(p).Begruendung}"));
        if (kennung == _kennung) return;
        _kennung = kennung;
        Aufbauen(w, punkte);
    }

    private Sitzungsstimme Eigene(Sitzungspunkt p) => p.Stimmen.First(s => s.Haus == _spieler);

    private void Aufbauen(Stadtwirtschaft w, IReadOnlyList<Sitzungspunkt> punkte)
    {
        foreach (var kind in _liste.GetChildren()) kind.QueueFree();
        var rest = w.Rat.StimmrundeFrist - w.Uhr.VergangeneMinuten;
        _frist.Text = $"Bis 23 Uhr (noch {rest / 60}:{rest % 60:00} Stunden) könnt ihr eure Stimmen abgeben. Am Jahresende stimmt der Rat ab; " +
                      "wer nicht abstimmt, behält die Stimme, die hier steht.";
        foreach (var p in punkte)
        {
            var block = RatStil.Container<VBoxContainer>(_liste, "RatSpalte");
            RatStil.Text(block, $"{p.Nummer}. {p.Titel}", "RatAbschnitt");
            RatStil.Text(block, p.Text, "RatText", umbrechen: true, breite: "kurz_breite");
            var eigene = Eigene(p);
            RatStil.Text(block, $"Eure Stimme: {StimmeText(p, eigene)} – {eigene.Begruendung}", "RatHinweis", umbrechen: true, breite: "kurz_breite");
            var knoepfe = RatStil.Container<HFlowContainer>(block, "RatZeile");
            if (p.Thema is Sitzungsthema.Wahl or Sitzungsthema.Domprediger)
            {
                foreach (var k in p.Kandidaten)
                    RatStil.Amtsknopf(knoepfe, k.Name, () => Senden(p.Thema == Sitzungsthema.Wahl ? new WahlStimme(p.Amt!.Name, k.Name) : new DomStimme(k.Name)),
                                      $"Eure Stimme für {k.Name}.");
            }
            else
            {
                RatStil.Amtsknopf(knoepfe, "Ja", () => Senden(AntragsStimme(p, true)), "Dafür stimmen.");
                RatStil.Amtsknopf(knoepfe, "Nein", () => Senden(AntragsStimme(p, false)), "Dagegen stimmen.");
                RatStil.Amtsknopf(knoepfe, "Enthaltung", () => Senden(AntragsStimme(p, null)), "Sich enthalten.");
            }
        }
    }

    private static string StimmeText(Sitzungspunkt p, Sitzungsstimme s) =>
        p.Thema is Sitzungsthema.Wahl or Sitzungsthema.Domprediger ? s.Kandidat ?? "keine"
        : s.Ja switch { true => "Ja", false => "Nein", null => "Enthaltung" };

    private static Befehl AntragsStimme(Sitzungspunkt p, bool? ja) => p.Thema switch
    {
        Sitzungsthema.Statut => new StatutStimme(ja),
        Sitzungsthema.Stadtglaube => new StadtglaubeStimme(ja),
        Sitzungsthema.Pflaster => new PflasterStimme(ja),
        _ => new RatsantragStimme(p.Antragsart!.Value, ja),
    };

    private void Senden(Befehl befehl)
    {
        if (_simulation is null || _spieler is null) return;
        _simulation.Bote.Einreihen(_spieler, befehl, e =>
        {
            GD.Print($"Stimmrunde: {(e.Erfolg ? "" : "abgelehnt: ")}{e.Meldung}"); // für Log und Klicktest
            Gemeldet?.Invoke(e.Meldung);
            _kennung = ""; // neu aufbauen
        });
    }
}
