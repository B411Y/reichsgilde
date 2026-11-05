using System.Linq;
using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// <b>Meldungen</b> (Benachrichtigungen, Stufe 1) und die <b>Hilfe</b> (PR #197) – ein Teil der <c>partial class Hauptszene</c>.
///
/// Der Kern meldet nur Text (<c>Stadtwirtschaft.Meldung</c>). <see cref="Protokollieren"/> ordnet jede Meldung ein (<see cref="Meldungsordnung"/>, Zuordnung in
/// <c>game/daten/meldungen.json</c>) und verteilt sie nach <see cref="Meldestufe"/>: <b>Verlauf</b> (Liste zum Nachlesen im Bericht, über alle Jahre der Sitzung),
/// <b>Siegel</b> (Ecke unten rechts, <see cref="Siegelecke"/>, ein Siegel je Thema mit Zähler) oder <b>Brief</b> (<see cref="BriefAnzeige"/>, hält nie die Zeit an).
/// Die Wahl je Art steht in den Einstellungen (<see cref="Meldungseinstellungen"/>). Alle Meldungen kommen nach rechts unten, auch abgelehnte Befehle („✗“).
///
/// Damit nichts überflutet:
///   • <b>Jahreswechsel</b>: Gericht, Rat, Steuer, Stand, Personal, Bank und Spitzel der Jahresabrechnung werden zu <i>einem</i> Brief „Jahresbericht“; einzeln bleiben
///     nur ein Todesfall und eine Anklage gegen das eigene Haus.
///   • <b>Stillstand</b> („Es fehlt …“, „Lager voll“): ein Siegel je Betrieb, erst nach etwa einer Spielstunde Dauer, es aktualisiert sich und geht mit dem Stillstand.
///   • <b>Nacht</b>: Einbrüche und Überfälle zwischen 20 und 23 Uhr und das Nachtgeschehen der Jahresabrechnung kommen am Morgen als ein Brief „Die Nacht“.
///   • <b>Rivalen</b> (Züge der KI-Häuser) nur im Verlauf; <b>fremde Unterwelt und fremde Spitzel</b> (der Kern meldet sie an alle) werden verworfen.
/// Nichts davon pausiert (Mehrspieler).
///
/// Hilfe: die Steuerung (früher im Kasten oben links) in einem Fenster in der Bildmitte – F1, Eintrag im Spielmenü, beim allerersten Spiel einmal von selbst.
/// </summary>
public partial class Hauptszene
{
    /// <summary>Zeit in Spielminuten, die ein Stillstand anhalten muss, bevor ein Siegel erscheint.</summary>
    private const int StillstandMinuten = 60;
    private const int VerlaufHoechstens = 3000;
    private const double FehlerSekunden = 25;

    private Siegelecke _ecke = null!;
    private BriefAnzeige _brief = null!;
    /// <summary>Alle Meldungen der Sitzung, die neueste zuerst (Reiter „Verlauf“ im Bericht).</summary>
    private readonly List<Meldungsposten> _verlauf = new();
    private readonly List<Meldungsposten> _jahresSammler = new(), _nachtSammler = new();
    private long _nachtTag;
    /// <summary>Betriebe mit Mangel: seit wann (Spielminute), letzter Grund, Anzahl der Meldungen; <c>Probe</c> = Vorführung (kein Abgleich mit dem Betrieb).</summary>
    private readonly Dictionary<string, (long Seit, string Grund, int Zahl, bool Probe)> _stillstaende = new();
    private double _meldungsUhr;

    private PanelContainer _hilfe = null!;
    private Label _hilfeKopf = null!;

    private void MeldungenBauen()
    {
        _ecke = new Siegelecke();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_ecke);
        _ecke.Angeklickt += SiegelGelesen;
        _brief = new BriefAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_brief);
        _brief.ZielGewaehlt += BriefZiel;
    }

    private Meldungsordnung Ordnung => Meldungseinstellungen.Ordnung;

    /// <summary>Ist es eine Warnung? Abgelehnte Befehle („✗“) und Gefahren („⚠“).</summary>
    private static bool IstWarnung(string meldung) => meldung.StartsWith('✗') || meldung.StartsWith('⚠');

    private void Protokollieren(string meldung)
    {
        // Ein Befehl meldet sein Ergebnis, die Stadt dieselbe Nachricht für alle – nur einmal zeigen.
        if (_verlauf.Count > 0 && _verlauf[0].Original == meldung) return;
        var w = _simulation?.Wirtschaft;
        Einstufung e;
        if (w is not null && _spieler is not null) e = Ordnung.Einstufen(w, _spieler, meldung);
        else e = new Einstufung(Ordnung.Einordnen(meldung), Betroffenheit.Stadt, Meldungsordnung.Ohne(meldung), IstWarnung(meldung), false, false);
        if (e.Verwerfen) return; // Geheimnisse anderer Häuser: der Kern meldet sie an alle, der Spieler soll sie nicht lesen
        var stufe = Meldungsordnung.Stufe(e.Art, e.Bezug, e.Text, Meldungseinstellungen.Wahl(e.Art.Id));
        if (stufe == Meldestufe.Aus) return;
        if (w is not null) KlangOberflaeche.Meldung(meldung, w.Daten.Allgemein.Waehrung, glocke: false); // Holzklopfer („✗“) oder Münzen; Siegel und Briefe haben eigene Töne

        var posten = new Meldungsposten
        {
            Zeit = _simulation?.Zeit.UhrzeitText ?? "", Jahr = _simulation?.Zeit.Jahr ?? 0, Text = e.Text, Original = meldung, Art = e.Art, Bezug = e.Bezug, Stufe = stufe,
            Warnung = e.Warnung, RivalenZug = e.RivalenZug,
            Betrieb = w is null || _spieler is null ? null : w.Betriebe.Where(b => b.Konto == _spieler && e.Text.Contains(b.Name, StringComparison.Ordinal))
                .OrderByDescending(b => b.Name.Length).Select(b => b.Name).FirstOrDefault(),
        };
        _verlauf.Insert(0, posten);
        if (_verlauf.Count > VerlaufHoechstens) _verlauf.RemoveAt(_verlauf.Count - 1);
        if (stufe >= Meldestufe.Siegel) Verteilen(posten, stufe);
    }

    private bool IstJahreswechsel => _simulation?.Zeit is { } z && z.VergangeneMinuten > 0 && (z.MinuteImTag == 0 || z.MinuteImTag >= Spielzeit.MinutenProTag - 2);
    /// <summary>Zwischen 20 und 23 Uhr und in der Jahresabrechnung (dort läuft das „Nachtgeschehen“ der Unterwelt) ist Nacht.</summary>
    private bool IstNacht => _simulation?.Zeit is { } z && (z.UhrzeitInMinuten >= 20 * 60 || IstJahreswechsel);
    private long Tag => _simulation is null ? 0 : _simulation.Zeit.VergangeneMinuten / Spielzeit.MinutenProTag;

    /// <summary>Stillstände, Nacht und Jahreswechsel werden gesammelt; alles andere wird gleich ein Siegel oder ein Brief.</summary>
    private void Verteilen(Meldungsposten posten, Meldestufe stufe)
    {
        var art = posten.Art;
        if (art.Sammeln == "stillstand" && posten.Bezug == Betroffenheit.Eigen && Meldungsordnung.Stillstandsbetrieb(posten.Text) is { } name)
        {
            var grund = posten.Text[(name.Length + 1)..].Trim();
            if (!Meldungsordnung.IstMangel(grund)) return; // Umbau: gewollt, kein Siegel
            var seit = _simulation.Zeit.VergangeneMinuten;
            _stillstaende[name] = _stillstaende.TryGetValue(name, out var alt) ? (alt.Seit, grund, alt.Zahl + 1, alt.Probe) : (seit, grund, 1, false);
            return;
        }
        if (art.Sammeln == "nacht" && stufe == Meldestufe.Brief && IstNacht)
        {
            _nachtSammler.Add(posten);
            _nachtTag = IstJahreswechsel ? Tag - 1 : Tag; // der Brief kommt am Morgen, also mit dem nächsten Tag (beim Jahreswechsel gleich)
            return;
        }
        if (art.Sammeln == "jahresbericht" && IstJahreswechsel && !art.Einzeln.Any(t => posten.Text.Contains(t, StringComparison.Ordinal) && posten.Bezug == Betroffenheit.Eigen))
        {
            _jahresSammler.Add(posten);
            return;
        }
        if (stufe == Meldestufe.Brief) BriefEinreihen(EinzelBrief(posten), gefahr: art.Thema == "gefahr");
        else SiegelAnlegen(posten);
    }

    // ── Siegel ───────────────────────────────────────────────────────────────

    private void SiegelAnlegen(Meldungsposten posten)
    {
        var thema = Ordnung.Thema(posten.Art.Thema);
        var fehler = posten.Art.Id == "Fehler";
        var neu = _ecke.Hinzufuegen("thema:" + thema.Id, thema, posten, SiegelBrief, fehler ? FehlerSekunden : 0);
        if (neu || !fehler) KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungSiegel);
        _ecke.TippsAuffrischen();
    }

    /// <summary>Der Brief zu einem Siegel: bei einem Stapel alle Meldungen (neueste oben), bei einem Stillstand der Stand von jetzt.</summary>
    private Brief SiegelBrief(Siegel s)
    {
        var thema = Ordnung.Thema(s.ThemaId);
        if (s.Schluessel.StartsWith("still:", StringComparison.Ordinal))
        {
            var name = s.Schluessel[6..];
            var b = _simulation.Wirtschaft?.Betriebe.FirstOrDefault(x => x.Name == name);
            var grund = b?.Stillstand ?? (_stillstaende.TryGetValue(name, out var st) ? st.Grund : "");
            var seit = _stillstaende.TryGetValue(name, out var m) ? (int)((_simulation.Zeit.VergangeneMinuten - m.Seit) / 60) : 0;
            return new Brief
            {
                Schluessel = s.Schluessel, Titel = "Die Arbeit ruht", Absender = "Euer Vorarbeiter", Farbe = Color.FromHtml(thema.Farbe), Thema = thema.Id, Ziel = "betrieb", Betrieb = name,
                Zeilen = { (name, grund != "" ? $"{grund}" : "Die Arbeit läuft wieder.", true) },
                Fuss = seit >= 1 ? $"Seit etwa {seit} Spielstunden." : "Seit gut einer Spielstunde.",
            };
        }
        var art = s.Posten[0].Art;
        var einzeln = s.Posten.Count == 1 || s.Posten.All(p => p.Art == art);
        var brief = new Brief
        {
            Schluessel = s.Schluessel, Titel = einzeln ? art.Titel : thema.Name, Absender = einzeln ? art.Absender : "Euer Haushofmeister", Farbe = Color.FromHtml(thema.Farbe), Thema = thema.Id,
            Ziel = einzeln ? art.Ziel : "", Betrieb = einzeln ? s.Posten[0].Betrieb : null,
        };
        foreach (var p in s.Posten.Take(60)) brief.Zeilen.Add(($"{p.Jahr} · {p.Zeit}" + (einzeln ? "" : " · " + p.Art.Name), p.Art.Id == "Fehler" ? p.Text.TrimStart('✗', ' ') : p.Text, p.Warnung));
        return brief;
    }

    private void SiegelGelesen(Siegel s)
    {
        KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungBrief);
        _brief.Zeigen(s.BriefBauer?.Invoke(s) ?? new Brief { Titel = "Nachricht" });
        if (s.Schluessel.StartsWith("still:", StringComparison.Ordinal)) { s.Gelesen = true; s.QueueRedraw(); } // bleibt, bis der Betrieb wieder läuft
        else _ecke.Entfernen(s);
    }

    // ── Briefe ───────────────────────────────────────────────────────────────

    private Brief EinzelBrief(Meldungsposten p)
    {
        var thema = Ordnung.Thema(p.Art.Thema);
        return new Brief
        {
            Titel = p.Art.Titel, Absender = p.Art.Absender, Farbe = Color.FromHtml(thema.Farbe), Thema = thema.Id, Ziel = p.Art.Ziel, Betrieb = p.Betrieb,
            Zeilen = { ($"{p.Jahr} · {p.Zeit}", p.Text, p.Warnung) },
        };
    }

    private void BriefEinreihen(Brief brief, bool gefahr = false)
    {
        if (gefahr) KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.Meldung); // leise Glocke bei Gefahr
        KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungBrief);
        _brief.Einreihen(brief);
    }

    /// <summary>Die Jahresabrechnung ist durch: Was sich dabei angesammelt hat, kommt als ein Brief „Jahresbericht“.</summary>
    private void JahresberichtAusgeben(int jahr)
    {
        if (_jahresSammler.Count == 0) return;
        var thema = Ordnung.Thema("rat");
        var brief = new Brief { Titel = $"Jahresbericht {jahr}", Absender = "Der Ratsschreiber", Farbe = Color.FromHtml(thema.Farbe), Thema = thema.Id, Ziel = "bericht" };
        if (_spieler?.Vorjahr is { } v && _simulation.Wirtschaft is { } w)
            brief.Zeilen.Add(("Jahresabrechnung", $"Gewinn {Zahlenformat.MitVorzeichen(v.Gewinn)} {w.Daten.Allgemein.Waehrung}, Umsatz {Zahlenformat.Ganz(v.Umsatz)}.", v.Gewinn < 0));
        foreach (var gruppe in _jahresSammler.GroupBy(p => p.Art).OrderBy(g => Ordnung.Arten.ToList().IndexOf(g.Key)))
            foreach (var p in gruppe)
                brief.Zeilen.Add((gruppe.Key.Name, p.Text, p.Warnung));
        _jahresSammler.Clear();
        BriefEinreihen(brief);
    }

    private void NachtBriefAusgeben()
    {
        var thema = Ordnung.Thema("gefahr");
        var brief = new Brief { Titel = "Die Nacht", Absender = "Der Büttel", Farbe = Color.FromHtml(thema.Farbe), Thema = thema.Id };
        foreach (var p in _nachtSammler) brief.Zeilen.Add(($"{p.Zeit}", p.Text, p.Warnung));
        brief.Fuss = _nachtSammler.Count == 1 ? "Das war alles in dieser Nacht." : $"{_nachtSammler.Count} Vorfälle in dieser Nacht.";
        _nachtSammler.Clear();
        BriefEinreihen(brief, gefahr: true);
    }

    /// <summary>Der Knopf unten im Brief: soweit der Text das Ziel hergibt.</summary>
    private void BriefZiel(Brief b)
    {
        switch (b.Ziel)
        {
            case "betrieb":
                if (_simulation.Wirtschaft?.Betriebe.FirstOrDefault(x => x.Name == b.Betrieb && x.Konto == _spieler) is { } betrieb) BetriebAuswaehlen(betrieb, hinfahren: true);
                break;
            case "familie": FamilieOeffnen(); break;
            case "rat": RatOeffnen(); break;
            case "bericht": BerichtOeffnen(BerichtAnzeige.Reiter.Jahresrechnung); break;
        }
    }

    // ── Takt ─────────────────────────────────────────────────────────────────

    /// <summary>Jedes Bild: zweimal je Sekunde die Stillstände abgleichen und den Nacht-Brief am Morgen ausgeben.</summary>
    private void MeldungenAltern(double delta)
    {
        _meldungsUhr += delta;
        if (_meldungsUhr < 0.5 || _simulation?.Wirtschaft is not { } w || _spieler is null) return;
        _meldungsUhr = 0;
        if (_nachtSammler.Count > 0 && Tag > _nachtTag) NachtBriefAusgeben();
        var jetzt = _simulation.Zeit.VergangeneMinuten;
        foreach (var (name, (seit, grund, zahl, probe)) in _stillstaende.ToList())
        {
            var betrieb = w.Betriebe.FirstOrDefault(b => b.Name == name && b.Konto == _spieler);
            var mangel = probe || Meldungsordnung.IstMangel(betrieb?.Stillstand);
            var schluessel = "still:" + name;
            var siegel = _ecke.Finden(schluessel);
            if (!mangel)
            {
                _stillstaende.Remove(name);
                if (siegel is not null)
                {
                    _ecke.Entfernen(siegel);
                    _verlauf.Insert(0, new Meldungsposten
                    {
                        Zeit = _simulation.Zeit.UhrzeitText, Jahr = _simulation.Zeit.Jahr, Text = $"{name}: Die Arbeit läuft wieder.", Original = "", Art = Ordnung.Art("Stillstand"),
                        Bezug = Betroffenheit.Eigen, Stufe = Meldestufe.Verlauf,
                    });
                }
                continue;
            }
            var aktuell = probe ? grund : betrieb!.Stillstand!;
            if (jetzt - seit < StillstandMinuten) continue;
            var posten = new Meldungsposten
            {
                Zeit = _simulation.Zeit.UhrzeitText, Jahr = _simulation.Zeit.Jahr, Text = $"{name}: {aktuell}", Original = "", Art = Ordnung.Art("Stillstand"),
                Bezug = Betroffenheit.Eigen, Stufe = Meldestufe.Siegel, Betrieb = name, Warnung = true,
            };
            if (siegel is null)
            {
                _ecke.Hinzufuegen(schluessel, Ordnung.Thema("handel"), posten, SiegelBrief);
                KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.MeldungSiegel);
                siegel = _ecke.Finden(schluessel)!;
            }
            siegel.Posten[0] = posten;
            siegel.Anzahl = zahl;
        }
        _ecke.TippsAuffrischen();
    }

    /// <summary>Startoption <c>--meldungen=demo|brief</c>: legt Siegel (und mit <c>brief</c> einen Brief) mit echten Wortlauten für Klicktests an.</summary>
    private void MeldungenVorfuehren(string wahl)
    {
        if (_simulation.Wirtschaft is not { } w || _spieler is null) return;
        var haus = w.Familien.Von(_spieler);
        var name = haus?.Nachname ?? "Brendel";
        var betrieb = w.Betriebe.FirstOrDefault(b => b.Konto == _spieler)?.Name ?? "Werkstatt";
        var jahr = _simulation.Zeit.Jahr;
        foreach (var text in new[]
        {
            $"Anna {name} ist krank: Fieber, der Medicus ist unterwegs.",
            $"Karren von {betrieb} (Holz) am Markt: Handelsauftrag erledigt.",
            $"Ratssitzung {jahr}: Antrag von {_spieler.Inhaber} – Steuer auf 12 % – ANGENOMMEN (3 dafür, 1 dagegen).",
            $"Einbruchsversuch in {betrieb} – die Diebe wurden verscheucht (der Wächter war wachsam).",
            $"Frühling {jahr}: die Erdwege sind aufgeweicht, Karren fahren dort langsamer.",
            "✗ Dafür fehlen noch 200 Gulden.",
        })
            Protokollieren(text);
        _stillstaende[betrieb] = (_simulation.Zeit.VergangeneMinuten - 3 * 60, "Es fehlt Holz – die Arbeit ruht.", 2, true);
        _meldungsUhr = 1;
        MeldungenAltern(0);
        if (wahl == "brief")
        {
            Protokollieren($"Trauer im Haus {name}: Großvater {name} ist gestorben. Das Haus trauert, der Rat bekundet sein Beileid.");
            Protokollieren($"Freude im Haus {name}: ein gesundes Kind, ein Sohn.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hilfe
    // ═════════════════════════════════════════════════════════════════════════

    private void HilfeBauen()
    {
        var (tafel, spalte) = Hauptmenue.Tafel(620);
        _hilfe = tafel;
        var mitte = Hauptmenue.Mitte();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(mitte);
        mitte.AddChild(tafel);
        Hauptmenue.Text(spalte, "Hilfe · Steuerung", 24, new Color(0.95f, 0.82f, 0.45f), zentriert: true);
        _hilfeKopf = Hauptmenue.Text(spalte, "", 13, new Color(0.75f, 0.75f, 0.75f), zentriert: true, umbruchBreite: 560);
        var raster = new GridContainer { Columns = 2 };
        raster.AddThemeConstantOverride("h_separation", 18);
        raster.AddThemeConstantOverride("v_separation", 4);
        spalte.AddChild(raster);
        foreach (var (taste, was) in new[]
        {
            ("Leertaste", "Pause / weiter"),
            ("1 – 4", "Tempo (5 – 6: Testtempo, orange)"),
            ("Mausrad", "Zoom"),
            ("Rechte Maustaste, W A S D", "Karte verschieben"),
            ("Q / E", "drehen (drinnen: Raum wechseln)"),
            ("Alt (halten)", "alle Namen zeigen"),
            ("Klick", "Gebäude auswählen – rechts erscheint seine Tafel"),
            ("Doppelklick", "hineingehen (Werkstatt, Wohnhaus, Rathaus, Dom)"),
            ("Esc", "einen Schritt zurück, zuletzt das Spielmenü"),
            ("F", "Familienbuch"),
            ("H", "Hinterzimmer"),
            ("R", "Ratskammer (im Baumodus: Umriss drehen)"),
            ("M", "Marktplatz"),
            ("B", "Bauen"),
            ("L", "Lexikon"),
            ("X", "alle Fenster schließen (Esc: das oberste)"),
            ("Titelrand ziehen", "Fenster verschieben; die Lage wird gemerkt"),
            ("Verweilen, T oder mittlere Maustaste", "Tooltip festheften; Verweise im Text öffnen weitere"),
            ("F1", "diese Hilfe"),
            ("F11", "Vollbild"),
        })
        {
            Hauptmenue.Text(raster, taste, 14, new Color(0.95f, 0.82f, 0.45f));
            Hauptmenue.Text(raster, was, 14);
        }
        Hauptmenue.Text(spalte,
            "Oben links steht euer Haus (ein Klick öffnet das Wohnhaus), oben in der Mitte die Zeit. Unten die Betriebsleiste: ein Klick fährt zum Betrieb und " +
            "zeigt seine Kurzübersicht, ein Doppelklick geht hinein – drinnen stellt ihr Produktion, Personal, Lager, Karren und Einrichtung ein. " +
            "Ein Klick auf das Rathaus zeigt Rat, Ämter, Statut und Gericht, ein Klick auf den Markt die Preise. Meldungen kommen als Siegel unten rechts über der Leiste (ein Klick liest den Brief) " +
            "oder als Brief; alles steht im Bericht unter „Verlauf“. Was gemeldet wird, stellt ihr in den Einstellungen unter „Meldungen“ ein.", 13, new Color(0.85f, 0.85f, 0.85f), umbruchBreite: 560);
        var zu = Hauptmenue.Knopf(spalte, "Schließen", HilfeSchliessen);
        zu.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        tafel.Hide();
    }

    private void HilfeOeffnen()
    {
        _hilfeKopf.Text = $"1 Spieljahr = {_simulation.Takt.RundenDauerSekunden / 60:0} Min. · {_simulation.Einstellungen.SiegbedingungText} · Schwierigkeit {_simulation.Einstellungen.Schwierigkeit}";
        if (_einstellungen.Tafel.Visible) EinstellungenSchliessen();
        if (_spielmenue.Visible) SpielmenueSchliessen(); // die Pause wie vor dem Menü
        _hilfe.Show();
    }

    private void HilfeSchliessen()
    {
        _hilfe.Hide();
        Fenster.HilfeGesehen = true;
    }
}
