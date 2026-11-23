using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// <b>Die Lade-Fenster der Fächer</b> (UI-Umbau „Zunftlade“, Teil 3; Entwurf 3) – ein Teil der <c>partial class Hauptszene</c>.
/// Jedes Fach der Lade öffnet dasselbe Fenster (<see cref="LadeFensterAnzeige"/>) mit seinem Inhalt: eine Zeile Einleitung, Kurzwerte und Befehle
/// mit Kosten (Gulden, AP), Wirkung und – wenn gesperrt – Grund und nächstem Schritt.
///
/// Hier wird nur <b>gelesen und zusammengestellt</b>: Werte und Sperrgründe kommen aus den Abfragen des Kerns, ein Befehl geht als gewöhnlicher
/// Befehl an den Boten (<see cref="UiHilfe.Befehlen"/>) oder öffnet das ausführliche Fenster (Familienbuch, Ratskammer, Personalbuch …).
/// Was der Entwurf zeigt, das Spiel aber nicht kennt, steht nicht hier (Liste in <c>docs/bereiche/oberflaeche.md</c>).
/// </summary>
public partial class Hauptszene
{
    private LadeFensterAnzeige _ladeFenster = null!;

    /// <summary>Einmal beim Aufbau (nach der Lade): das eine Lade-Fenster, in der Mitte über der Lade.</summary>
    private void LadeFensterBauen()
    {
        _ladeFenster = new LadeFensterAnzeige();
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_ladeFenster);
    }

    /// <summary>Ein Fach wurde geklickt: sein Lade-Fenster öffnen (oder schließen, wenn es schon offen ist). Andere Fenster gehen dabei zu.</summary>
    private void FachOeffnen(string fach, Func<Stadtwirtschaft, Konto, LadeInhalt?> inhalt)
    {
        if (_ladeFenster.Visible && _ladeFenster.Fach == fach)
        {
            _ladeFenster.Schliessen();
            return;
        }
        TafelnSchliessen();
        _ladeFenster.Zeigen(fach, () => _simulation.Wirtschaft is { } w && _spieler is { } s ? inhalt(w, s) : null);
    }

    /// <summary>Startoption <c>--fach=&lt;name&gt;</c>: öffnet das Lade-Fenster eines Fachs (drinnen mit <c>--innen=1</c> die Fächer der Werkstatt).</summary>
    private void FachStartoption(string name)
    {
        Func<Stadtwirtschaft, Konto, LadeInhalt?>? inhalt = name.ToLowerInvariant() switch
        {
            "familie" => FamilieInhalt, "betriebe" => BetriebeInhalt, "bauen" => BauenInhalt, "rat" => RatInhalt, "bericht" => BerichtInhalt,
            "lexikon" => LexikonInhalt, "markt" => MarktInhalt, "personal" => PersonalInhalt, "produktion" => ProduktionInhalt, "lager" => LagerInhalt,
            "karren" => KarrenInhalt, "talentbaum" => TalentbaumInhalt, _ => null,
        };
        if (inhalt is null) Protokollieren($"--fach={name}: gemeint ist familie, betriebe, bauen, rat, bericht, lexikon, markt, personal, produktion, lager, karren oder talentbaum.");
        else FachOeffnen(name.ToLowerInvariant(), inhalt);
    }

    /// <summary>Ein Befehl aus dem Lade-Fenster schließt es und öffnet das ausführliche Fenster.</summary>
    private Action Weiter(Action oeffnen) => () =>
    {
        _ladeFenster.Schliessen();
        oeffnen();
    };

    /// <summary>Ein Befehl an den Kern; das Ergebnis steht im Protokoll, das Fenster bleibt offen und frischt sich auf.</summary>
    private Action Befehl(Befehl befehl) => () => UiHilfe.Befehlen(_simulation, _spieler, befehl, _ladeFenster, e => Protokollieren(UiHilfe.MeldungText(e)));

    private static string Gulden(double betrag, string waehrung) => $"{Zahlenformat.Ganz(betrag)} {waehrung}";

    private static string Kosten(string waehrung, double gulden = 0, int ap = 0)
    {
        var teile = new List<string>();
        if (gulden > 0) teile.Add(Gulden(gulden, waehrung));
        if (ap > 0) teile.Add($"{ap} AP");
        return string.Join(" · ", teile);
    }

    // ── Stadt ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private LadeInhalt? FamilieInhalt(Stadtwirtschaft w, Konto s)
    {
        if (w.Familien.Von(s) is not { } f) return null;
        var buch = w.Familien;
        var zeilen = new List<LadeZeile>();
        if (f.Ehepartner is { } partner)
            zeilen.Add(new LadeZeile($"{partner.Vorname}, {partner.Ehepartnertitel}", partner.Lebt ? buch.Verfassung(partner) : "verstorben", w.Diebe.IstGeisel(partner)));
        else zeilen.Add(new LadeZeile("Ehe", "ledig"));
        var kinder = f.Kinder.Where(k => k.Lebt).ToList();
        zeilen.Add(new LadeZeile("Kinder", kinder.Count == 0 ? "keine" : string.Join(", ", kinder.Select(k => k.Vorname))));
        zeilen.Add(new LadeZeile("Erbe", buch.Erbe(f) is { } erbe ? erbe.Vorname + (f.Testament == erbe ? " (Testament)" : "") : "nicht geregelt", buch.Erbe(f) is null));
        var punkte = _lebenswegSumme;
        if (punkte > 0) zeilen.Add(new LadeZeile("Lebensweg", punkte == 1 ? "1 Punkt frei" : $"{punkte} Punkte frei"));

        var befehle = new List<LadeBefehl>();
        foreach (var e in w.Entscheidungen.OffenFuer(s))
        {
            var rest = w.Entscheidungen.RestMinuten(e);
            var nummer = e.Nummer;
            befehle.Add(new LadeBefehl($"Brief „{w.Entscheidungen.Titel(e)}“ öffnen", "", $"Frist: noch {rest / 60}:{rest % 60:00} Std.", null, Weiter(() => _entscheidungSpaeter.Remove(nummer))));
        }
        befehle.Add(new LadeBefehl("Familienbuch", "", "Oberhaupt, Ehe und Brautschau, Kinder, Gesundheit", null, Weiter(() => _familientafel.Oeffnen(FamilienAnzeige.Reiter.Familie))));
        befehle.Add(new LadeBefehl("Stammbaum", "", "alle Häuser der Stadt", null, Weiter(() => _familientafel.Oeffnen(FamilienAnzeige.Reiter.Stammbaum))));
        befehle.Add(new LadeBefehl("Chronik", "", "was dem Haus widerfahren ist", null, Weiter(() => _familientafel.Oeffnen(FamilienAnzeige.Reiter.Chronik))));
        befehle.Add(kinder.Count == 0
            ? new LadeBefehl("Erben bestimmen", "", "", "Gesperrt: Es gibt noch kein Kind.")
            : new LadeBefehl("Erben bestimmen", "", "im Familienbuch bei den Kindern (Testament)", null, Weiter(() => _familientafel.Oeffnen(FamilienAnzeige.Reiter.Familie))));
        return new LadeInhalt("Familie", $"Haus {f.Nachname} · {f.Lebende.Count()} {(f.Lebende.Count() == 1 ? "Person" : "Personen")}", zeilen, befehle);
    }

    private LadeInhalt? BetriebeInhalt(Stadtwirtschaft w, Konto s)
    {
        var eigene = w.Betriebe.Where(b => b.Konto == s).ToList();
        var zeilen = new List<LadeZeile>();
        var befehle = new List<LadeBefehl>();
        foreach (var b in eigene)
        {
            var (kurz, lang, warnung, tafel) = BetriebsUebersicht.Zustand(b);
            zeilen.Add(new LadeZeile(b.Name, warnung ? lang : kurz, warnung));
            var betrieb = b;
            if (b.IstWerkstatt)
                befehle.Add(new LadeBefehl($"In {b.Name} gehen", "", warnung && tafel is not null ? "Innenansicht – öffnet gleich, was den Stillstand behebt" : "Innenansicht mit den Fächern der Werkstatt",
                    null, Weiter(() => BetriebBetreten(betrieb, warnung ? tafel : null))));
            befehle.Add(new LadeBefehl($"{b.Name} zeigen", "", "hinfahren und Kurzübersicht", null, Weiter(() => BetriebAuswaehlen(betrieb, hinfahren: true))));
        }
        return new LadeInhalt("Betriebe", eigene.Count == 1 ? "1 Betrieb" : $"{eigene.Count} Betriebe", zeilen, befehle);
    }

    private LadeInhalt? BauenInhalt(Stadtwirtschaft w, Konto s)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var zeilen = new List<LadeZeile>();
        var befehle = new List<LadeBefehl>();
        if (w.Wohnen.IstAktiv && w.Wohnen.Stufe(s) is { } stufe)
        {
            zeilen.Add(new LadeZeile("Wohnhaus", stufe.Name));
            if (w.Wohnen.FertigAb(s) is { } ab)
            {
                var rest = Math.Max(0, ab - _simulation.Zeit.VergangeneMinuten);
                zeilen.Add(new LadeZeile("Ausbau läuft", $"noch {rest / 60}:{rest % 60:00} Std."));
            }
            else if (w.Wohnen.NaechsteStufe(s) is { } naechste && !w.Bauen.NurNeubau(s.WohnhausStufe + 1))
            {
                var grund = w.Wohnen.AusbauVerboten(s);
                befehle.Add(new LadeBefehl($"Wohnhaus ausbauen zum {naechste.Name}", Kosten(waehrung, naechste.Ausbau),
                    $"Ansehen +{Zahlenformat.Ganz(naechste.Ansehen)} · Unterhalt {Gulden(naechste.Unterhalt, waehrung)} im Jahr", grund is null ? null : $"Gesperrt: {grund}",
                    Befehl(new WohnhausAusbauen())));
            }
        }
        zeilen.Add(new LadeZeile("Bauzonen der Stadt", Zahlenformat.Ganz(w.Karte?.Bauzonen.Count ?? 0)));
        befehle.Add(new LadeBefehl("Gebäude bauen", "", "Wohnhaus oder Werkstatt: Gebäude wählen, dann den Platz in einer Bauzone (B)", null, Weiter(() => BauenStarten(wege: false))));
        befehle.Add(new LadeBefehl("Wege ausbauen", "", "Belag, Stufe und Kosten jedes Weges; ein Klick auf einen Weg öffnet seine Tafel (G)", null, Weiter(() => BauenStarten(wege: true))));
        return new LadeInhalt("Bauen", "Gebaut wird in den farbigen Bauzonen, mit der Vorderseite an einer Straße.", zeilen, befehle);
    }

    private void BauenStarten(bool wege)
    {
        BauenOeffnen();
        if (_bau.Aktiv) _bau.WegeModus = wege;
    }

    private LadeInhalt? RatInhalt(Stadtwirtschaft w, Konto s)
    {
        if (!w.Rat.IstAktiv) return null;
        var amt = w.Rat.AmtVon(s);
        var punkte = w.Rat.Sitzungsvorschau().Count;
        var rest = Math.Max(0, w.Rat.StimmrundeFrist - _simulation.Zeit.VergangeneMinuten);
        var zeilen = new List<LadeZeile>
        {
            new("Nächste Sitzung", $"am Jahresende {w.Jahr} · in {rest / 60}:{rest % 60:00} Std."),
            new("Euer Amt", amt?.Name ?? "keines"),
            new("Punkte der Tagesordnung", Zahlenformat.Ganz(punkte)),
        };
        if (w.Rat.StimmrundeLaeuft) zeilen.Add(new LadeZeile("Stimmrunde", "läuft – eure Stimmen bis 23 Uhr", true));
        var befehle = new List<LadeBefehl>
        {
            new("Ämterbaum ansehen", "", "Ämter, Inhaber, Bewerbung und Wahl", null, Weiter(() => RatOeffnen(RatAnzeige.Reiter.Aemter))),
            new("Anträge", "", "Tagesordnung, Statut, Wegbau", null, Weiter(() => RatOeffnen(RatAnzeige.Reiter.Antraege))),
            new("Stadtglaube", "", "", null, Weiter(() => RatOeffnen(RatAnzeige.Reiter.Stadtglaube))),
            new("Stadtkasse", "", "Steuersatz, Einnahmen und Ausgaben der Stadt", null, Weiter(() => RatOeffnen(RatAnzeige.Reiter.Kasse))),
        };
        return new LadeInhalt("Rat", "Der Rat der Stadt", zeilen, befehle);
    }

    private LadeInhalt? BerichtInhalt(Stadtwirtschaft w, Konto s)
    {
        var l = s.Laufend;
        var zeilen = new List<LadeZeile>();
        void Posten(string name, double wert)
        {
            if (Math.Abs(wert) >= 0.5) zeilen.Add(new LadeZeile(name, Zahlenformat.MitVorzeichen(wert)));
        }
        Posten("Umsatz", l.Umsatz);
        Posten("Zutaten", -l.Zutaten);
        Posten("Löhne", -l.Personal);
        Posten("Handgeld", -l.Handgeld);
        Posten("Unterhalt", -l.Fixkosten);
        Posten("Umsatzsteuer", -l.Steuer);
        Posten("Fahrten", -l.Transport);
        Posten("Schaden", -l.Schaden);
        zeilen.Add(new LadeZeile("Gewinn bisher", Zahlenformat.MitVorzeichen(l.Gewinn), l.Gewinn < -0.5));
        var befehle = new List<LadeBefehl>
        {
            new("Jahresrechnung", "", "dieses Jahr und das Vorjahr", null, Weiter(() => BerichtOeffnen(BerichtAnzeige.Reiter.Jahresrechnung))),
            new("Rivalen", "", "die anderen Häuser und ihr letzter Zug", null, Weiter(() => BerichtOeffnen(BerichtAnzeige.Reiter.Rivalen))),
            new("Umsätze", "", "Verkäufe je Ware", null, Weiter(() => BerichtOeffnen(BerichtAnzeige.Reiter.Umsaetze))),
            new("Briefablage", "", "alle Meldungen und Briefe zum Nachlesen", null, Weiter(() => BerichtOeffnen(BerichtAnzeige.Reiter.Protokoll))),
        };
        return new LadeInhalt("Bericht", $"Jahr {w.Jahr} bisher · Löhne und Unterhalt werden am Jahresende fällig", zeilen, befehle);
    }

    private LadeInhalt? LexikonInhalt(Stadtwirtschaft w, Konto s) => new("Lexikon", "Nachschlagen · jedes unterstrichene Wort führt hierher", new List<LadeZeile>(), new List<LadeBefehl>
    {
        new("Stichwort suchen", "L", "Begriffe und Regeln des Spiels", null, Weiter(() => _lexikon.Oeffnen())),
        new("Hilfe zur Steuerung", "F1", "Tasten und Maus", null, Weiter(HilfeOeffnen)),
    });

    private LadeInhalt? MarktInhalt(Stadtwirtschaft w, Konto s)
    {
        var waehrung = w.Daten.Allgemein.Waehrung;
        var eigene = w.Betriebe.Where(b => b.Konto == s).ToList();
        var waren = eigene.SelectMany(b => b.Zutaten.Keys.Append(b.Produkt)).Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(8).ToList();
        var zeilen = new List<LadeZeile>();
        foreach (var ware in waren)
        {
            var stand = w.Markt[ware];
            var abweichung = stand.Preis / stand.Grundpreis - 1;
            var zusatz = Math.Abs(abweichung) < 0.05 ? "" : $" · {Zahlenformat.MitVorzeichen(abweichung * 100)} % zum Grundpreis";
            zeilen.Add(new LadeZeile(ware, $"{Zahlenformat.Kommazahl(stand.Preis)} {waehrung}{zusatz}"));
        }
        var befehle = new List<LadeBefehl> { new("Markt öffnen", "M", w.VerwalterhandelVerboten(s) is not null ? "alle Waren und Preise, Handel mit dem Karren am Markt" : "alle Waren und Preise, Handelsauftrag für einen Karren", null, Weiter(MarktOeffnen)) };
        return new LadeInhalt("Markt", waren.Count == 0 ? "Der Markt der Stadt" : "Preise der Waren eurer Betriebe", zeilen, befehle);
    }

    // ── Innenansicht ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Der eigene Betrieb, in dem man gerade steht (sonst null: die Fächer der Werkstatt sind dann gesperrt).</summary>
    private Betrieb? EigenerInnenBetrieb(Konto s) => _stadt.InnenBetrieb is { } b && b.Konto == s ? b : null;

    private LadeInhalt? PersonalInhalt(Stadtwirtschaft w, Konto s)
    {
        if (EigenerInnenBetrieb(s) is not { } b) return null;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var zeilen = w.Werkstaetten.Arbeiterliste(b).Select(z => new LadeZeile($"{z.Name} · {z.Arbeitskraft}", $"arbeitet an: {z.Ware} · Laune {Zahlenformat.Ganz(z.Laune)}", z.Laune < 30)).ToList();
        var befehle = new List<LadeBefehl>();
        if (w.Personal.Anfaenger(b) is { } anfaenger)
        {
            var grund = w.Personal.EinstellenVerboten(b, anfaenger);
            var handgeld = w.Personal.Handgeld(anfaenger);
            befehle.Add(new LadeBefehl($"{anfaenger} einstellen", Kosten(waehrung, handgeld), $"Lohn {Gulden(w.Daten.Lohn(anfaenger, b.Art), waehrung)} im Jahr",
                grund is null ? null : $"Gesperrt: {grund}", Befehl(new ArbeiterEinstellen(b.Name, anfaenger))));
        }
        befehle.Add(new LadeBefehl("Personalbuch", "", "befördern, Prämie, Lohnstufe, entlassen", null, Weiter(() => InnenTafelOeffnen("personal"))));
        return new LadeInhalt("Personal", $"{b.Name} · {b.Belegt} von {b.Plaetze} Plätzen besetzt", zeilen, befehle);
    }

    private LadeInhalt? ProduktionInhalt(Stadtwirtschaft w, Konto s)
    {
        if (EigenerInnenBetrieb(s) is not { } b) return null;
        var zeilen = new List<LadeZeile>();
        if (!string.IsNullOrEmpty(b.Produkt)) zeilen.Add(new LadeZeile("Hauptware", b.Produkt));
        foreach (var gruppe in b.Arbeiter.GroupBy(b.WareVon).OrderBy(g => g.Key))
            zeilen.Add(new LadeZeile(gruppe.Key, gruppe.Count() == 1 ? "1 Arbeiter" : $"{gruppe.Count()} Arbeiter"));
        if (b.Stillstand is { } still) zeilen.Add(new LadeZeile("Stillstand", still, true));
        var befehle = new List<LadeBefehl> { new("Auftrag ändern", "", "Hauptware wählen, Arbeiter den Waren zuteilen", null, Weiter(() => InnenTafelOeffnen("produktion"))) };
        return new LadeInhalt("Produktion", b.Name, zeilen, befehle);
    }

    private LadeInhalt? LagerInhalt(Stadtwirtschaft w, Konto s)
    {
        if (EigenerInnenBetrieb(s) is not { } b) return null;
        var waren = b.Zutaten.Keys.Concat(b.Artdaten.Produkte).Append(b.Produkt).Concat(b.Lager.Keys).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var zeilen = waren.Select(x => new LadeZeile(x, $"{b.ImLager(x)} von {b.LagerKapazitaetJeWare}", b.ImLager(x) >= b.LagerKapazitaetJeWare)).ToList();
        var befehle = new List<LadeBefehl> { new("Lager öffnen", "", "Bestand je Ware, Ware zurückhalten", null, Weiter(() => InnenTafelOeffnen("lager"))) };
        if (w.Werkstaetten.Baum(b).Stuecke.FirstOrDefault(e => e.Kennung == "Regale") is { } regale && regale.Zustand != Baumzustand.Gekauft)
            befehle.Add(new LadeBefehl($"Lager ausbauen: {regale.Name}", Kosten(w.Daten.Allgemein.Waehrung, regale.Preis), regale.Wirkung,
                regale.Verboten is null ? null : $"Gesperrt: {regale.Verboten}", Befehl(new EinrichtungKaufen(b.Name, regale.Kennung))));
        return new LadeInhalt("Lager", $"Je Ware passen {b.LagerKapazitaetJeWare} Stück ins Lager", zeilen, befehle);
    }

    private LadeInhalt? KarrenInhalt(Stadtwirtschaft w, Konto s)
    {
        if (EigenerInnenBetrieb(s) is not { } b) return null;
        var zeilen = b.Karren.Select(k => new LadeZeile(k.Name, k.ZustandText(_simulation!.Zeit))).ToList();
        var befehle = new List<LadeBefehl>
        {
            new("Karrenhof", "", "Karren kaufen und verkaufen, Eskorte, Nachtfahrt", null, Weiter(() => InnenTafelOeffnen("karren"))),
        };
        if (w.VerwalterhandelVerboten(s) is not null) // selbst gefahren: kein Handelsauftrag, sondern Transportmenü, Laden und Handeln
            befehle.Add(new("Karren fahren", "", "Karren des Hauses losschicken: zum Markt oder zu einem eigenen Betrieb, dort laden und handeln", null, Weiter(() => { TafelnSchliessen(); _fahrtTafel.Oeffnen(b); })));
        else
            befehle.Add(new("Handel am Markt", "", "Handelsauftrag für einen Karren dieses Betriebs", null, Weiter(() => { TafelnSchliessen(); _markt.Oeffnen(b); })));
        return new LadeInhalt("Karren", b.Karren.Count == 1 ? "1 Karren" : $"{b.Karren.Count} Karren", zeilen, befehle);
    }

    private LadeInhalt? TalentbaumInhalt(Stadtwirtschaft w, Konto s)
    {
        if (EigenerInnenBetrieb(s) is not { } b) return null;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var baum = w.Werkstaetten.Baum(b);
        var gekauft = baum.Stuecke.Count(e => e.Zustand == Baumzustand.Gekauft);
        var zeilen = new List<LadeZeile>
        {
            new("Stufe der Werkstatt", $"{baum.Stufe} von {baum.Reihen}"),
            new("Eingerichtet", $"{gekauft} von {baum.Stuecke.Count} Stücken"),
        };
        var befehle = new List<LadeBefehl>();
        foreach (var e in baum.Stuecke.Where(e => e.Zustand != Baumzustand.Gekauft).OrderBy(e => e.Reihe).ThenBy(e => e.Preis).Take(4))
            befehle.Add(new LadeBefehl(e.Name, Kosten(waehrung, e.Preis), e.Wirkung, e.Zustand == Baumzustand.Kaufbar ? null : $"Gesperrt: {e.Verboten ?? "noch nicht zu haben."}",
                Befehl(new EinrichtungKaufen(b.Name, e.Kennung))));
        befehle.Add(new LadeBefehl("Ganzen Talentbaum ansehen", "", "alle Stücke mit Voraussetzungen", null, Weiter(() => InnenTafelOeffnen("talentbaum"))));
        return new LadeInhalt("Talentbaum", $"Einrichtung der Werkstatt · in der Kasse {Gulden(s.Vermoegen, waehrung)}", zeilen, befehle);
    }

    // ── Zustandszeilen der Fächer (eine Zeile unter dem Namen, wie im Entwurf) ──────────────────────────────────────────────────────

    /// <summary>Viermal in der Sekunde: die Zeile Zustand jedes Fachs aus dem Kern lesen.</summary>
    private void FachZustaendeAnzeigen(Stadtwirtschaft w, Konto s)
    {
        // Familie: eine offene Entscheidung geht vor, sonst freie Punkte des Lebenswegs
        var offen = w.Entscheidungen.OffenFuer(s);
        var punkte = _lebenswegSumme;
        FachZustand(_fachFamilie, offen.Count > 0 ? (offen.Count == 1 ? "1 Brief offen" : $"{offen.Count} Briefe offen") : punkte <= 0 ? "" : punkte == 1 ? "1 Punkt frei" : $"{punkte} Punkte frei",
            offen.Count > 0);
        FachZustand(_fachBauen, w.Wohnen.IstAktiv && w.Wohnen.FertigAb(s) is not null ? "Ausbau läuft" : "", false);
        FachZustand(_fachRat, !w.Rat.IstAktiv ? "" : w.Rat.StimmrundeLaeuft ? "Stimmrunde läuft" : w.Rat.AmtVon(s) is { } amt ? amt.Name : "tagt am Jahresende", w.Rat.IstAktiv && w.Rat.StimmrundeLaeuft);
        var gewinn = s.Laufend.Gewinn;
        FachZustand(_fachBericht, $"Jahr {Zahlenformat.MitVorzeichen(gewinn)}", gewinn < -0.5);
        if (EigenerInnenBetrieb(s) is not { } b)
        {
            foreach (var fach in new[] { _fachPersonal, _fachLager, _fachKarren, _fachTalentbaum }) FachZustand(fach, "", false);
            return;
        }
        FachZustand(_fachPersonal, $"{b.Belegt} von {b.Plaetze}", b.Belegt == 0);
        var waren = b.Zutaten.Keys.Concat(b.Artdaten.Produkte).Append(b.Produkt).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var voll = waren.Any(x => b.ImLager(x) >= b.LagerKapazitaetJeWare);
        FachZustand(_fachLager, voll ? "Lager voll" : $"{waren.Sum(b.ImLager)} Stück", voll);
        var unterwegs = b.Karren.Count(k => k.Zustand != KarrenZustand.AmBetrieb);
        FachZustand(_fachKarren, b.Karren.Count == 0 ? "kein Karren" : unterwegs > 0 ? "unterwegs" : "am Betrieb", false);
        var kaufbar = w.Werkstaetten.Baum(b).Stuecke.Count(e => e.Zustand == Baumzustand.Kaufbar);
        FachZustand(_fachTalentbaum, kaufbar == 0 ? "" : kaufbar == 1 ? "1 Stück zu haben" : $"{kaufbar} Stücke zu haben", false);
    }

    private static void FachZustand((LadeFach Knopf, Label Name, Label Zustand) fach, string text, bool warnung)
    {
        Setzen(fach.Zustand, text);
        LadeStil.Variante(fach.Zustand, warnung ? "LadeTextWarnung" : "LadeTextLeise");
    }
}
