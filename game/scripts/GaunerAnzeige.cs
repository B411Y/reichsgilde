using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Hinterzimmer als „Schwarzes Buch“ (Schritt 1: die 2D-Tafel im Buch-Aufbau, noch ohne Buchmodell): eine Doppelseite, rechts daneben
/// vier Lesebänder als Kapitel – „Heute Nacht“, „Die Häuser“, „Eigene Leute“, „Wachen, Briefe“. Im Kapitel „Die Häuser“ hat jedes Haus
/// seine eigene Doppelseite (Register darüber, Blättern am Fuß): links Steckbrief, Betriebe und Wohnhaus, rechts Oberhaupt, Ruf und Erpressung.
/// Jede Tat ist eine Zeile mit festen Spalten: Name und Folge, Chance als Balken, Kosten, Knopf.
///
/// Was gerade gar nicht geht (fehlender Raum „Hinterzimmer“, Kerker), steht <b>oben</b> in der Sperrleiste, mit Grund und nächstem Schritt;
/// die Zeilen darunter sind dann sichtbar gesperrt. Auch die Rückmeldung des letzten Befehls steht oben.
///
/// Wie alle Bedienfelder ändert die Tafel nichts selbst, sondern schickt Befehle an die Stadtwirtschaft. Aussehen steht nur im Thema
/// (<see cref="HinterzimmerStil"/>, <c>hinterzimmer_thema.tres</c>). Aufbau der Kapitel: <c>GaunerAnzeige.Haeuser.cs</c>, <c>GaunerAnzeige.Kapitel.cs</c>.
/// </summary>
public partial class GaunerAnzeige : PanelContainer
{
    /// <summary>Die Kapitel des Buchs (Lesebänder).</summary>
    public enum Kapitel { Nacht, Haeuser, Leute, Schutz }

    private Simulation? _simulation;
    private Konto? _spieler;
    private HBoxContainer _inhalt = null!;

    /// <summary>Aufgeschlagenes Kapitel und Haus (Inhaber) – bleiben über jeden Neuaufbau und jedes Öffnen erhalten.</summary>
    private Kapitel _kapitel = Kapitel.Haeuser;

    /// <summary>Das aufgeschlagene Kapitel (das 3D-Buch auf dem Kartentisch blättert um, wenn es wechselt; <c>Hauptszene.Buecher.cs</c>).</summary>
    public Kapitel KapitelJetzt => _kapitel;
    private string? _ziel;

    /// <summary>Rückmeldung des letzten Befehls (steht oben, bis die Tafel neu geöffnet wird).</summary>
    private string _meldung = "";
    private bool _meldungGut;

    // Nur während eines Aufbaus gültig: was jede Zeile braucht.
    private Stadtwirtschaft _w = null!;
    private string _waehrung = "";
    private string? _kerker;

    /// <summary>Gewünschtes Schweigegeld je Haus – nur der Wert wird gemerkt, das Feld entsteht bei jedem Aufbau neu.</summary>
    private readonly Dictionary<string, double> _schweigegeld = new();

    /// <summary>Gewünschtes Schutzgeld und Lösegeld je Haus, gewählte Geisel je Haus (Nummer der Person), gewählter Ratsherr je Haus.</summary>
    private readonly Dictionary<string, double> _schutzgeld = new();
    private readonly Dictionary<string, double> _loesegeld = new();
    private readonly Dictionary<string, int> _geisel = new();
    private readonly Dictionary<string, string> _ratsherr = new();

    /// <summary>Ergebnis eines Befehls – für das Protokoll.</summary>
    public event Action<string>? Gemeldet;

    /// <summary>Knopf „Zum Wohnhaus“ der Sperrleiste: Die Hauptszene öffnet die Tafel des eigenen Wohnhauses.</summary>
    public event Action? WohnhausGewuenscht;

    public override void _Ready()
    {
        // Die Tafel ist der Einband (Leder), darauf eine Prägelinie; innen links das Buch, rechts am Buchrand die Lesebänder.
        HinterzimmerStil.Anwenden(this);
        ThemeTypeVariation = "HzTafel";
        var rand = HinterzimmerStil.Container<MarginContainer>(this, "HzRand");
        var praegung = HinterzimmerStil.Container<PanelContainer>(rand, "HzPraegung");
        _inhalt = HinterzimmerStil.Container<HBoxContainer>(praegung, "HzBlaetter");
        Rollbereich.Einhuellen(this);
        Fensterrahmen.Anmelden(this, "Hinterzimmer", festhaltbar: true);
        Hide();
    }

    public void Verbinden(Simulation simulation, Konto spieler)
    {
        _simulation = simulation;
        _spieler = spieler;
        _ziel = null;
        _kapitel = Kapitel.Haeuser;
    }

    public void Oeffnen()
    {
        _meldung = "";
        Aufbauen();
        Show();
    }

    /// <summary>Schlägt ein Kapitel auf (Lesebänder, Verweise, Klicktest).</summary>
    public void Aufschlagen(Kapitel kapitel)
    {
        _kapitel = kapitel;
        Aufbauen();
    }

    private void Befehlen(Befehl befehl)
    {
        UiHilfe.Befehlen(_simulation, _spieler, befehl, this, ergebnis =>
        {
            _meldung = UiHilfe.MeldungText(ergebnis);
            _meldungGut = ergebnis.Erfolg;
            Gemeldet?.Invoke(_meldung);
            Aufbauen();
        });
    }

    private static string Prozent(double anteil) => $"{Zahlenformat.Ganz(anteil * 100)} %";

    private string Gulden(double betrag) => $"{Zahlenformat.Ganz(betrag)} {_waehrung}";

    /// <summary>Preis in der Kosten-Spalte: die Zahl, oder „Lager“, wenn die Ware aus dem eigenen Lager kommt.</summary>
    private static string Preis(double preis) => preis > 0 ? Zahlenformat.Ganz(preis) : "Lager";

    /// <summary>
    /// Warum dieser Befehl gerade gar nicht geht – Kerker oder fehlender Raum (<c>Wohnungswesen.BefehlOhneRaum</c>, dieselbe Prüfung wie bei
    /// der Annahme des Befehls im Kern). Der Grund steht einmal oben in der Sperrleiste; die Zeile zeigt nur „gesperrt“.
    /// </summary>
    private string? Hauptsperre(Befehl muster) => _kerker ?? _w.Wohnen.BefehlOhneRaum(_spieler!, muster);

    /// <summary>Die Arten von Befehlen des Hinterzimmers, mit dem Namen für die Sperrleiste („Gesperrt (Banden, Erpresserbriefe): …“).</summary>
    private static readonly (string Was, Befehl Muster)[] Befehlsarten =
    {
        ("Banden", new Beauftragen("", Gaunerstueck.Diebstahl)),
        ("Erpresserbriefe", new Erpressen("", 0)),
        ("Giftanschlag", new Giftanschlag("", "")),
        ("Hetzschrift", new HetzschriftVerbreiten("")),
        ("Zwietracht", new ZwietrachtSaeen("", "")),
        ("eigene Leute", new Auskundschaften("")),
    };

    /// <summary>Baut den Inhalt neu auf – beim Öffnen, nach jedem Befehl, zur Nacht und zum Jahreswechsel.</summary>
    public void Aufbauen()
    {
        if (_simulation?.Wirtschaft is not { } w || _spieler is null) return;
        UiHilfe.KinderEntfernen(_inhalt);
        _w = w;
        _waehrung = w.Daten.Allgemein.Waehrung;
        var unterwelt = w.Gauner;
        if (!unterwelt.IstAktiv)
        {
            var leer = HinterzimmerStil.Container<VBoxContainer>(HinterzimmerStil.Container<MarginContainer>(_inhalt, "HzBandkopf"), "HzSpalte");
            HinterzimmerStil.Text(leer, "Hinterzimmer", "HzEinbandTitel");
            HinterzimmerStil.Text(leer, "In dieser Stadt gibt es keine Unterwelt (Spielstand von vor den Gaunern).", "HzEinbandText");
            return;
        }
        var gericht = w.Justiz;
        _kerker = gericht.IstAktiv && gericht.ImKerker(_spieler)
            ? $"Euer Oberhaupt sitzt bis Ende {gericht.KerkerBis(_spieler)} im Kerker – aus der Zelle heraus heuert niemand eine Bande an."
            : null;
        var haeuser = w.Konten.Where(k => k != _spieler && k.IstAktiv).ToList();
        var ziel = haeuser.FirstOrDefault(h => h.Inhaber == _ziel) ?? haeuser.FirstOrDefault();
        _ziel = ziel?.Inhaber;
        var versteck = w.Diebe.IstAktiv ? w.Diebe.Versteck(_spieler) : null;
        if (_kapitel == Kapitel.Leute && versteck is null) _kapitel = Kapitel.Haeuser;
        var bestellt = Bestelltes().ToList();
        var post = BriefeAnEuch();
        var g = unterwelt.Daten;

        // ── Links: über dem Buch die Zettel (Sperre, Post, Rückmeldung), dann Register und Doppelseite ──
        var buchSpalte = HinterzimmerStil.Container<VBoxContainer>(_inhalt, "HzSpalte");
        Sperrleiste(buchSpalte);
        if (post > 0 && _kapitel != Kapitel.Schutz)
        {
            var leiste = HinterzimmerStil.Container<PanelContainer>(buchSpalte, "HzPost");
            var zeile = HinterzimmerStil.Container<HBoxContainer>(leiste, "HzZeile");
            HinterzimmerStil.Text(zeile, post == 1 ? "Ein Brief an euch wartet auf Antwort." : $"{post} Briefe an euch warten auf Antwort.", "HzText")
                .SizeFlagsHorizontal = SizeFlags.ExpandFill;
            HinterzimmerStil.Knopf(zeile, "Zu den Briefen", () => Aufschlagen(Kapitel.Schutz));
        }
        if (_meldung != "")
            HinterzimmerStil.Text(HinterzimmerStil.Container<PanelContainer>(buchSpalte, "HzZettel"), _meldung, _meldungGut ? "HzGut" : "HzSchlecht", "kopf_breite");

        var block = HinterzimmerStil.Container<VBoxContainer>(buchSpalte, "HzDicht");
        var register = HinterzimmerStil.Container<HBoxContainer>(block, "HzRegisterzeile");
        register.CustomMinimumSize = new Vector2(0, HinterzimmerStil.Mass("register_hoehe")); // auch ohne Register: das Buch springt nicht
        register.Alignment = BoxContainer.AlignmentMode.End;
        var buch = HinterzimmerStil.Container<PanelContainer>(block, "HzBuch");
        var blaetter = HinterzimmerStil.Container<HBoxContainer>(buch, "HzBlaetter");
        var links = Blatt(blaetter, "HzBlatt");
        HinterzimmerStil.Container<PanelContainer>(blaetter, "HzFalz").ZIndex = 1; // der Schatten des Falzes liegt über beiden Seiten
        var rechts = Blatt(blaetter, "HzBlattRechts");

        // ── Rechts auf dem Einband: Titel, Aktionspunkte, Bestelltes (neben ✕ und Nadel), darunter die Lesebänder am Buchrand ──
        var bandSpalte = HinterzimmerStil.Container<VBoxContainer>(_inhalt, "HzBandspalte");
        bandSpalte.AddChild(new Control { CustomMinimumSize = new Vector2(0, HinterzimmerStil.Mass("kopf_frei")) });
        var kopf = HinterzimmerStil.Container<VBoxContainer>(HinterzimmerStil.Container<MarginContainer>(bandSpalte, "HzBandkopf"), "HzDicht");
        HinterzimmerStil.Text(kopf, "Hinterzimmer", "HzEinbandTitel",
            tipp: $"Bei Nacht und Nebel: Bestellt wird jetzt, ausgeführt heute um 20 Uhr, bezahlt im Voraus. Jedes Gaunerstück hinterlässt eine Spur fürs Gericht, " +
                  $"und mit {Prozent(g.ZeugenChance)} Chance (bei einem Wächter {Prozent(g.ZeugenChanceWaechter)}) erkennt euch ein Zeuge. Der Hehler zahlt {Prozent(g.HehlerAnteil)} des Marktwerts.");
        HinterzimmerStil.Text(kopf, $"Aktionspunkte: {_spieler.Aktionspunkte.Vorrat}", "HzEinbandText",
            tipp: $"Viele Taten kosten Aktionspunkte. Zum Jahreswechsel kommen {w.AktionspunkteJeRunde} dazu.");
        HinterzimmerStil.Text(kopf, $"Bestellt: {bestellt.Count}", bestellt.Count > 0 ? "HzEinbandGold" : "HzEinbandText",
            tipp: "Was ihr für diese Nacht bestellt habt – die Liste steht im Kapitel „Heute Nacht“.");
        bandSpalte.AddChild(new Control { CustomMinimumSize = new Vector2(0, HinterzimmerStil.Mass("band_oben")) });
        Band(bandSpalte, 1, "mond_hell", Kapitel.Nacht, "Heute Nacht", bestellt.Count, "Was für diese Nacht bestellt ist und was zuletzt geschah.");
        Band(bandSpalte, 2, "sack_hell", Kapitel.Haeuser, "Die Häuser", 0, "Eine Doppelseite je Haus: alles, was ihr ihm antun könnt.");
        Band(bandSpalte, 3, "leute_hell", Kapitel.Leute, "Eigene Leute", 0, versteck is null
            ? $"Eigene Leute hat nur, wer einen Betrieb als {w.Diebe.Daten.Beruf} führt."
            : "Eure Diebe: ausspähen, Schutzgeld verlangen, entführen.", gesperrt: versteck is null);
        Band(bandSpalte, 4, "schild_hell", Kapitel.Schutz, "Wachen, Briefe", post, "Wächter für eure Betriebe, Briefe an euch und eure eigenen Forderungen.");

        switch (_kapitel)
        {
            case Kapitel.Nacht: NachtSeiten(links, rechts, bestellt, post); break;
            case Kapitel.Leute: Register(register, haeuser, ziel); LeuteSeiten(links, rechts, versteck!, ziel); break;
            case Kapitel.Schutz: SchutzSeiten(links, rechts); break;
            default: Register(register, haeuser, ziel); HausSeiten(links, rechts, haeuser, ziel, versteck); break;
        }
    }

    /// <summary>
    /// Die Sperrleiste ganz oben, ein Zettel über dem Buch: Kerker oder fehlender Raum – in zwei Zeilen, was betroffen ist mit dem Grund
    /// des Kerns, darunter der nächste Schritt und der Knopf „Zum Wohnhaus“. Ohne Sperre entsteht nichts.
    /// Den nächsten Schritt nennt der Kern (<c>Wohnungswesen.RaumAnbauenVerboten</c>); angebaut wird in der Tafel des Wohnhauses.
    /// </summary>
    private void Sperrleiste(Container eltern)
    {
        var gesperrt = Befehlsarten.Select(b => (b.Was, Grund: Hauptsperre(b.Muster), b.Muster)).Where(b => b.Grund is not null).ToList();
        if (gesperrt.Count == 0) return;
        var leiste = HinterzimmerStil.Container<PanelContainer>(eltern, "HzSperre");
        var spalte = HinterzimmerStil.Container<VBoxContainer>(leiste, "HzDicht");
        var was = gesperrt.Count == Befehlsarten.Length ? "Gesperrt: " : $"Gesperrt ({string.Join(", ", gesperrt.Select(b => b.Was))}): ";
        HinterzimmerStil.Text(spalte, was + gesperrt[0].Grund, "HzSperrtitel", "kopf_breite");
        var zeile = HinterzimmerStil.Container<HBoxContainer>(spalte, "HzZeile");
        if (_kerker is not null)
        {
            HinterzimmerStil.Text(zeile, "Nächster Schritt: abwarten. Briefe an euch könnt ihr weiter beantworten, die Betriebe laufen weiter.", "HzText", "schritt_breite");
            return;
        }
        var wohnen = _w.Wohnen;
        var raum = wohnen.RaumFuerBefehl(gesperrt[0].Muster)!;
        var hindernis = wohnen.RaumAnbauenVerboten(_spieler!, raum.Kennung);
        var schritt = hindernis is null
            ? $"Am Wohnhaus den Raum „{raum.Name}“ anbauen ({Gulden(_w.Baupreis(_spieler!, raum.Preis))}, endgültig)."
            : wohnen.FreiePlaetze(_spieler!) == 0 ? $"das Wohnhaus ausbauen. {hindernis}" : hindernis;
        var oder = raum.BerufStattRaum is { } beruf ? $"Oder einen Betrieb als {beruf} führen – von dort geht es auch ohne den Raum." : "";
        HinterzimmerStil.Text(zeile, "Nächster Schritt: " + schritt, "HzText", "schritt_breite", oder).SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HinterzimmerStil.Knopf(zeile, "Zum Wohnhaus", () => WohnhausGewuenscht?.Invoke(), tipp: "Öffnet die Tafel eures Wohnhauses: Ausbau und Räume.")
            .SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    /// <summary>Eine Buchseite aus Pergament („HzBlatt“ links, „HzBlattRechts“ rechts) mit fester Mindestgröße, darin die Spalte für den Inhalt.</summary>
    private static VBoxContainer Blatt(Container blaetter, string variante)
    {
        var blatt = HinterzimmerStil.Container<PanelContainer>(blaetter, variante);
        blatt.CustomMinimumSize = new Vector2(HinterzimmerStil.Mass("seite_breite"), HinterzimmerStil.Mass("seite_hoehe"));
        return HinterzimmerStil.Container<VBoxContainer>(blatt, "HzSeite");
    }

    /// <summary>Ein Leseband am Buchrand (Farbe <paramref name="nummer"/> 1–4): schlägt sein Kapitel auf; das aufgeschlagene steht weiter heraus. <paramref name="zahl"/> &gt; 0 steht dahinter.</summary>
    private void Band(Container eltern, int nummer, string zeichen, Kapitel kapitel, string name, int zahl, string tipp, bool gesperrt = false)
    {
        var band = HinterzimmerStil.Knopf(eltern, zahl > 0 ? $"{name} ({zahl})" : name, () => Aufschlagen(kapitel), $"HzBand{nummer}", tipp, gesperrt);
        band.ToggleMode = true;
        band.ButtonPressed = kapitel == _kapitel;
        band.Alignment = HorizontalAlignment.Left;
        band.Icon = HinterzimmerStil.Zeichen(zeichen);
        band.ExpandIcon = true;
        band.CustomMinimumSize = new Vector2(HinterzimmerStil.Mass("band_breite"), 0);
        band.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    }

    /// <summary>Das Register über der rechten Seite: ein Reiter je Haus, dahinter die Zahl dessen, was heute Nacht gegen es bestellt ist.</summary>
    private void Register(Container register, List<Konto> haeuser, Konto? ziel)
    {
        foreach (var haus in haeuser)
        {
            var zahl = Bestelltes().Count(b => b.Ziel == haus);
            var inhaber = haus.Inhaber;
            var reiter = HinterzimmerStil.Knopf(register, zahl > 0 ? $"{inhaber} ({zahl})" : inhaber, () => Blaettern(inhaber), "HzRegister",
                $"{inhaber} · {_w.StandVon(haus).Name} · {Gulden(haus.Reinvermoegen)}");
            reiter.ToggleMode = true;
            reiter.ButtonPressed = haus == ziel;
            reiter.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        }
        register.AddChild(new Control { CustomMinimumSize = new Vector2(HinterzimmerStil.Mass("register_rechts"), 0) });
    }

    /// <summary>Blättert zur Doppelseite eines Hauses.</summary>
    private void Blaettern(string inhaber)
    {
        _ziel = inhaber;
        Aufbauen();
    }

    /// <summary>Überschrift einer Gruppe von Taten (mit Tintenlinie) und die Spalte ihrer Zeilen.</summary>
    private static VBoxContainer Gruppe(Container seite, string titel, string tipp = "")
    {
        if (seite.GetChildCount() > 0) HinterzimmerStil.Container<HSeparator>(seite, "HzAbstand");
        HinterzimmerStil.Text(seite, titel, "HzGruppe", tipp: tipp);
        HinterzimmerStil.Container<HSeparator>(seite, "HzGruppenlinie");
        return HinterzimmerStil.Container<VBoxContainer>(seite, "HzDicht");
    }

    /// <summary>
    /// Eine Tat als Zeile mit festen Spalten: Zeichen (<paramref name="zeichen"/>, Name im Thema), Name (mit Marke „Kerker“ bei schweren
    /// Vergehen) und darunter die Folge, Chance als Balken, Kosten (Preis und Aktionspunkte), Knopf. Zwischen zwei Zeilen einer Gruppe steht
    /// eine gepunktete Linie. Zustände:
    ///   • <paramref name="bestellt"/> gesetzt: der Text steht golden statt der Folge, statt des Knopfs <paramref name="bestelltKurz"/>;
    ///   • Hauptsperre (Kerker, Raum fehlt – Grund oben in der Sperrleiste): Zeile grau, statt des Knopfs „gesperrt“;
    ///   • <paramref name="grund"/> gesetzt oder zu wenig Aktionspunkte: der Grund steht rot statt der Folge, der Knopf ist gesperrt.
    /// <paramref name="muster"/> ist ein Befehl derselben Art (für Raum und Aktionspunkte zählt nur die Art), <paramref name="feld"/> ein
    /// Eingabefeld unter dem Namen (Betrag, Auswahl), das nur bei bestellbarer Tat erscheint.
    /// </summary>
    private void Tat(VBoxContainer raster, string zeichen, string name, string folge, Befehl muster, Action bestellen, double? chance = null, string preis = "",
        string? bestellt = null, string bestelltKurz = "bestellt", string? grund = null, string tipp = "", bool schwer = false, Control? feld = null,
        string knopf = "Bestellen")
    {
        var haupt = Hauptsperre(muster);
        var ap = _w.ApKosten(_spieler!, muster);
        if (bestellt is null && haupt is null && grund is null && ap > 0 && !_spieler!.Aktionspunkte.Reicht(ap))
            grund = $"Nicht genug Aktionspunkte: {ap} nötig, {_spieler.Aktionspunkte.Vorrat} übrig.";
        if (bestellt is not null || haupt is not null) grund = null;
        var zu = bestellt is null && (haupt is not null || grund is not null);

        if (raster.GetChildCount() > 0) raster.AddChild(new HinterzimmerPunktlinie());
        var zeile = HinterzimmerStil.Container<HBoxContainer>(raster, "HzTatzeile");
        HinterzimmerStil.ZeichenBild(zeile, zeichen, zu);
        var tatSpalte = HinterzimmerStil.Container<VBoxContainer>(zeile, "HzDicht");
        tatSpalte.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var kopf = HinterzimmerStil.Container<HBoxContainer>(tatSpalte, "HzEng");
        HinterzimmerStil.Text(kopf, name, zu ? "HzGesperrt" : "HzText", tipp: tipp);
        if (schwer) HinterzimmerStil.Text(kopf, "Kerker", "HzMarke", tipp: "Schweres Vergehen: Fliegt es auf, droht vor Gericht der Kerker.").SizeFlagsVertical = SizeFlags.ShrinkCenter;
        if (bestellt is not null) HinterzimmerStil.Text(tatSpalte, bestellt, "HzGold", "tat_breite");
        else if (grund is not null) HinterzimmerStil.Text(tatSpalte, grund, "HzGrund", "tat_breite");
        else if (folge != "") HinterzimmerStil.Text(tatSpalte, folge, "HzKlein", "tat_breite", tipp);
        if (feld is not null)
        {
            if (zu || bestellt is not null) feld.QueueFree();
            else tatSpalte.AddChild(feld);
        }

        var chanceSpalte = HinterzimmerStil.Container<VBoxContainer>(zeile, "HzDicht");
        chanceSpalte.CustomMinimumSize = new Vector2(HinterzimmerStil.Mass("chance_breite"), 0);
        if (chance is { } c)
        {
            HinterzimmerStil.Text(chanceSpalte, Prozent(c), zu ? "HzGesperrt" : "HzText", tipp: "Erfolgschance. Wächter und Einrichtung des Ziels senken sie.");
            HinterzimmerStil.Chance(chanceSpalte, c, zu);
        }

        var kostenSpalte = HinterzimmerStil.Container<VBoxContainer>(zeile, "HzDicht");
        kostenSpalte.CustomMinimumSize = new Vector2(HinterzimmerStil.Mass("kosten_breite"), 0);
        if (preis != "") HinterzimmerStil.Text(kostenSpalte, preis, zu ? "HzGesperrt" : "HzText", tipp: $"Preis in {_waehrung}, bezahlt im Voraus.");
        if (ap > 0) HinterzimmerStil.Text(kostenSpalte, $"{ap} AP", "HzKlein", tipp: "Aktionspunkte, die diese Tat kostet.");

        var aktion = HinterzimmerStil.Container<VBoxContainer>(zeile, "HzDicht");
        aktion.CustomMinimumSize = new Vector2(HinterzimmerStil.Mass("aktion_breite"), 0);
        if (bestellt is not null) HinterzimmerStil.Text(aktion, bestelltKurz, "HzGold", tipp: bestellt);
        else if (haupt is not null) HinterzimmerStil.Text(aktion, "gesperrt", "HzGesperrt", tipp: haupt);
        else HinterzimmerStil.Knopf(aktion, knopf, bestellen, tipp: grund ?? tipp, gesperrt: grund is not null);
    }

    /// <summary>Ein Betragsfeld, dessen Wert die Tafel über den Neuaufbau hinweg merkt.</summary>
    private Zahlfeld Betragsfeld(Dictionary<string, double> merker, string schluessel, double vorschlag, double min, double max, double schritt)
    {
        var feld = new Zahlfeld
        {
            MinValue = min, MaxValue = max, Step = schritt, Value = merker.GetValueOrDefault(schluessel, vorschlag), Suffix = _waehrung,
            CustomMinimumSize = new Vector2(HinterzimmerStil.Mass("zahlfeld_breite"), 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        feld.ValueChanged += wert => merker[schluessel] = wert;
        return feld;
    }

    /// <summary>Alles, was der Spieler für diese Nacht bestellt hat (Banden, Wegelagerer, Giftdolch, eigene Leute) – Ziel und Text für die Liste.</summary>
    private IEnumerable<(Konto Ziel, string Text)> Bestelltes()
    {
        var unterwelt = _w.Gauner;
        foreach (var a in unterwelt.Auftraege.Where(a => a.Auftraggeber == _spieler))
            yield return (a.Ziel, $"{a.Art.Name()} bei {a.Ziel.Inhaber}{(a.Betrieb is { } b ? $" ({b})" : "")}");
        foreach (var h in unterwelt.Hinterhalte.Where(h => h.Auftraggeber == _spieler))
            yield return (h.Ziel, $"Wegelagerer lauern den Karren von {h.Ziel.Inhaber} auf");
        if (_w.Duft.IstAktiv)
            foreach (var a in _w.Duft.Auftraege.Where(a => a.Auftraggeber == _spieler))
                yield return (a.Ziel, $"{a.Dolch} gegen das Oberhaupt von {a.Ziel.Inhaber}");
        if (_w.Diebe.IstAktiv)
            foreach (var a in _w.Diebe.Auftraege.Where(a => a.Dieb == _spieler))
                yield return (a.Ziel, $"Eure Leute: {a.Art.Name()} bei {a.Ziel.Inhaber}{(a.Betrieb is { } b ? $" ({b})" : "")}");
    }

    /// <summary>Zahl der Briefe an den Spieler, die auf Antwort warten (Erpressung, Lösegeld, Schutzgeld).</summary>
    private int BriefeAnEuch() =>
        _w.Gauner.Forderungen.Count(f => f.Opfer == _spieler) + (_w.Diebe.IstAktiv ? _w.Diebe.Briefe.Count(b => b.Opfer == _spieler) : 0);

    private static string StufenName(Stadtwirtschaft w, int stufe) =>
        w.Daten.Betriebsarten.Values.FirstOrDefault(a => a.Beruf == w.Diebe.Daten.Beruf && a.Stufe == stufe)?.Name ?? $"Stufe {stufe}";
}
