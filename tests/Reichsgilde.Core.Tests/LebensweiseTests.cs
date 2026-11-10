using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für die Lebensweise (Charaktersystem Teil 3a): Daten, Erfahrung, Befehle, Wirkungen, Erbe, KI und Spielstand.</summary>
static class LebensweiseTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    /// <summary>Dieselben Daten ohne den Abschnitt "lebensweise" – wie vor Teil 3a.</summary>
    static readonly Lazy<Wirtschaftsdaten> Ohne = new(() =>
    {
        var knoten = JsonNode.Parse(Json.Value, documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!.AsObject();
        knoten.Remove("lebensweise");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    });

    static Simulation NeueSimulation(int startwert = 1400, Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, Volk volk = Volk.Mensch, double geld = 50_000)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: geld);
        var f = w.Familien.Gruenden(konto, volk, Geschlecht.Mann, alter: 30, glaube: Konfession.Katholisch);
        konto.Aktionspunkte.Setzen(12);
        return (konto, f);
    }

    static void Zuege(Stadtwirtschaft w, Person p, params string[] kennungen)
    {
        p.ZuegeLeeren();
        foreach (var k in kennungen) p.ZugHinzufuegen(w.Daten.Charakterzuege!.Zug(k)!);
    }

    static Person Heiraten(Stadtwirtschaft w, Konto konto)
    {
        var k = w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau);
        w.Familien.Vermaehlen(konto, k, 0);
        return w.Familien.Von(konto)!.Ehepartner!;
    }

    /// <summary>Gibt der Person genug Erfahrung im Pfad des Knotens und nimmt ihn über den Befehl (mit allen Voraussetzungen).</summary>
    static void Nehmen(Stadtwirtschaft w, Konto konto, Person p, params string[] knoten)
    {
        var d = w.Familien.LebensweiseDaten!;
        foreach (var kennung in knoten)
        {
            var pfad = d.Knoten(kennung)!.Value.Pfad;
            p.Lebensweg.ErfahrungSetzen(pfad, d.ErfahrungFuer(9));
            var e = w.Ausfuehren(konto, new KnotenWaehlen(p.Nummer, kennung));
            Pruefe.Wahr(e.Erfolg, $"{kennung}: {e.Meldung}");
        }
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Lebensweise: Daten – fünf Pfade (einer je Talent) mit 3 × 3 Knoten, voller Baum 1.350 Erfahrung, Datenversion 74", () =>
        {
            var d = Daten.Value.Lebensweise!;
            Pruefe.Gleich(81, Daten.Value.Version);
            Pruefe.Gleich(5, d.Pfade.Count);
            Pruefe.Wahr(TalentTexte.Alle.All(d.Pfade.ContainsKey), "jedes Talent hat einen Pfad");
            Pruefe.Gleich(45, d.AlleKnoten.Count());
            foreach (var (t, p) in d.Pfade)
            {
                Pruefe.Gleich(3, p.Spalten.Count, $"{t}: Spalten");
                foreach (var s in p.Spalten)
                    Pruefe.Gleich("1,2,3", string.Join(",", p.Knoten.Values.Where(k => k.Spalte == s).Select(k => k.Reihe).OrderBy(r => r)), $"{t}/{s}");
                Pruefe.Wahr(p.Passend.Count > 0 && p.Hemmend.Count > 0 && p.Taten.Count > 0, $"{t}: Züge und Taten");
            }
            Pruefe.Ungefaehr(1350, d.ErfahrungFuer(9), 1e-9);
            Pruefe.Gleich(3, d.PunkteAus(Talent.Handwerk, 349.9));
            Pruefe.Gleich(4, d.PunkteAus(Talent.Handwerk, 450));
            Pruefe.Gleich(9, d.PunkteAus(Talent.Handwerk, 99_999)); // nie mehr Punkte als Knoten
            Pruefe.Gleich("Feilscher", d.Voraussetzung(Talent.Verhandeln, d.Pfade[Talent.Verhandeln].Knoten["Handelsherr"]));
            Pruefe.Gleich((string?)null, d.Voraussetzung(Talent.Verhandeln, d.Pfade[Talent.Verhandeln].Knoten["Marktschreier"]));
        }),

        ("Lebensweise: Daten werden geprüft – Spalte, Zug, Tat, doppelte Reihe, Wertebereich", () =>
        {
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"spalte\": \"Markt\", \"reihe\": 1", "\"spalte\": \"Hafen\", \"reihe\": 1")),
                "Spalte „Hafen“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"passend\": [ \"Gierig\", \"Klug\" ]", "\"passend\": [ \"Gierig\", \"Bucklig\" ]")),
                "Charakterzug „Bucklig“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"taten\": [ \"KreditAufnehmen\",", "\"taten\": [ \"Kuchenbacken\",")),
                "Befehl „Kuchenbacken“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"spalte\": \"Markt\", \"reihe\": 3", "\"spalte\": \"Markt\", \"reihe\": 2")),
                "doppelt belegt");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"wirkung\": { \"bauFaktor\": 0.9 }", "\"wirkung\": { \"bauFaktor\": 1.9 }")),
                "bauFaktor");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"jagdAbStand\": \"Bürger\"", "\"jagdAbStand\": \"Kaiser\"")),
                "Stand „Kaiser“");
        }),

        ("Lebensweise: Erfahrung am Jahresende – Oberhaupt voll, Ehepartner halb, Kinder nichts; Pfad des stärksten Talents wird festgehalten", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var o = f.Oberhaupt;
            Zuege(w, o);
            var e = Heiraten(w, konto);
            Zuege(w, e);
            var kind = w.Familien.KindAufnehmen(konto, 5);
            var pfad = w.Familien.AktiverPfad(o);
            Pruefe.Gleich(TalentTexte.Alle.OrderByDescending(t => o.Talent(t)).ThenBy(t => t).First(), pfad);
            Pruefe.Gleich((Talent?)null, o.Lebensweg.Pfad);
            w.Familien.LebensweiseImJahreswechsel(f);
            Pruefe.Ungefaehr(30 + 5 * o.Talent(pfad), o.Lebensweg.Erfahrung(pfad), 1e-9);
            Pruefe.Gleich((Talent?)pfad, o.Lebensweg.Pfad);
            var ep = w.Familien.AktiverPfad(e);
            Pruefe.Ungefaehr((30 + 5 * e.Talent(ep)) * 0.5, e.Lebensweg.Erfahrung(ep), 1e-9);
            Pruefe.Wahr(kind.Lebensweg.IstLeer, "das Kind sammelt nichts");
            Pruefe.Wahr(!w.Familien.HatLebensweg(kind), "das Kind hat keine Lebensweise");
        }),

        ("Lebensweise: Züge, Gelehrt und Bibliothek ändern die Erfahrung (passend × 1,2, hemmend × 0,8)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var o = f.Oberhaupt;
            Zuege(w, o, "Gierig", "Klug");
            Pruefe.Ungefaehr(1.44, w.Familien.ErfahrungFaktor(o, Talent.Verhandeln), 1e-12);
            Zuege(w, o, "Grosszuegig", "Gelehrt");
            Pruefe.Ungefaehr(0.88, w.Familien.ErfahrungFaktor(o, Talent.Verhandeln), 1e-12);
            Pruefe.Ungefaehr(1.1, w.Familien.ErfahrungFaktor(o, Talent.Kampf), 1e-12);
            konto.WohnhausEinrichtungHinzufuegen("Bibliothek");
            Pruefe.Ungefaehr(1.21, w.Familien.ErfahrungFaktor(o, Talent.Kampf), 1e-12);
        }),

        ("Lebensweise: Taten – je AP einer Handlung des aktiven Pfads 10 Erfahrung, andere Pfade nichts (Gegenprobe)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var (_, g) = Haus(w, "Haus Gast");
            var o = f.Oberhaupt;
            Zuege(w, o);
            Zuege(w, g.Oberhaupt);
            Pruefe.Wahr(w.Ausfuehren(konto, new LebenswegWaehlen(o.Nummer, Talent.Rhetorik)).Erfolg, "Pfad Rhetorik");
            var zechen = w.Ausfuehren(konto, new Aktivitaet("Zechen", g.Oberhaupt.Kennung));
            Pruefe.Wahr(zechen.Erfolg, zechen.Meldung);
            Pruefe.Ungefaehr(10, o.Lebensweg.Erfahrung(Talent.Rhetorik), 1e-9);
            Pruefe.Wahr(w.Ausfuehren(konto, new LebenswegWaehlen(o.Nummer, Talent.Kampf)).Erfolg, "Pfad Kampf");
            Pruefe.Wahr(w.Ausfuehren(konto, new Aktivitaet("Brettspiel", g.Oberhaupt.Kennung)).Erfolg, "Brettspiel");
            Pruefe.Ungefaehr(0, o.Lebensweg.Erfahrung(Talent.Kampf), 1e-9);
            Pruefe.Ungefaehr(10, o.Lebensweg.Erfahrung(Talent.Rhetorik), 1e-9); // Gesammeltes bleibt beim alten Pfad
            // Üben zählt, wenn es das Talent des Pfads ist
            var ap = konto.Aktionspunkte.Vorrat;
            Pruefe.Wahr(w.Ausfuehren(konto, new TalentTrainieren(Talent.Kampf)).Erfolg, "Üben");
            Pruefe.Ungefaehr(10 * (ap - konto.Aktionspunkte.Vorrat), o.Lebensweg.Erfahrung(Talent.Kampf), 1e-9);
        }),

        ("Lebensweise: Befehle – ohne Punkt, ohne Voraussetzung, doppelt, fremde Person und Kind werden abgelehnt; Wechsel behält alles", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var (fremd, g) = Haus(w, "Haus Fremd");
            var o = f.Oberhaupt;
            var ap = konto.Aktionspunkte.Vorrat;
            var ohnePunkt = w.Ausfuehren(konto, new KnotenWaehlen(o.Nummer, "Sorgfalt"));
            Pruefe.Wahr(!ohnePunkt.Erfolg && ohnePunkt.Meldung.Contains("Kein freier Punkt"), ohnePunkt.Meldung);
            o.Lebensweg.ErfahrungSetzen(Talent.Handwerk, 100);
            var ohneVor = w.Ausfuehren(konto, new KnotenWaehlen(o.Nummer, "Kniffe"));
            Pruefe.Wahr(!ohneVor.Erfolg && ohneVor.Meldung.Contains("Sorgfalt"), ohneVor.Meldung);
            Pruefe.Wahr(w.Ausfuehren(konto, new KnotenWaehlen(o.Nummer, "Sorgfalt")).Erfolg, "Sorgfalt");
            Pruefe.Wahr(!w.Ausfuehren(konto, new KnotenWaehlen(o.Nummer, "Sorgfalt")).Erfolg, "doppelt");
            Pruefe.Wahr(!w.Ausfuehren(konto, new KnotenWaehlen(o.Nummer, "Ordnung")).Erfolg, "der Punkt ist verbraucht");
            Pruefe.Wahr(!w.Ausfuehren(konto, new KnotenWaehlen(o.Nummer, "Erfunden")).Erfolg, "unbekannter Knoten");
            Pruefe.Wahr(!w.Ausfuehren(fremd, new KnotenWaehlen(o.Nummer, "Ordnung")).Erfolg, "fremdes Haus");
            Pruefe.Wahr(!w.Ausfuehren(fremd, new LebenswegWaehlen(o.Nummer, Talent.Kampf)).Erfolg, "fremdes Haus wählt keinen Pfad");
            var kind = w.Familien.KindAufnehmen(konto, 6);
            Pruefe.Wahr(!w.Ausfuehren(konto, new LebenswegWaehlen(kind.Nummer, Talent.Kampf)).Erfolg, "Kind");
            Pruefe.Gleich(ap, konto.Aktionspunkte.Vorrat, "0 AP");
            Pruefe.Wahr(w.Ausfuehren(konto, new LebenswegWaehlen(o.Nummer, Talent.Kampf)).Erfolg, "Wechsel");
            Pruefe.Ungefaehr(100, o.Lebensweg.Erfahrung(Talent.Handwerk), 1e-9);
            Pruefe.Wahr(o.Lebensweg.HatKnoten("Sorgfalt"), "Knoten bleibt");
            var zeilen = w.Familien.Pfade(o);
            Pruefe.Gleich(5, zeilen.Count);
            var hw = zeilen.First(z => z.Pfad == Talent.Handwerk);
            Pruefe.Wahr(hw.Punkte == 1 && hw.Frei == 0 && hw.Knoten == 1 && hw.BisNaechsterPunkt == 100, $"Handwerk {hw}");
            Pruefe.Wahr(zeilen.First(z => z.Pfad == Talent.Kampf).Aktiv, "Kampf aktiv");
        }),

        ("Lebensweise: Baum für die Ansicht – 9 Knoten, Zustand nach Punkt und Voraussetzung, für Fremde nicht nehmbar", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var (fremd, _) = Haus(w, "Haus Fremd");
            var o = f.Oberhaupt;
            o.Lebensweg.ErfahrungSetzen(Talent.Handwerk, 200);
            Pruefe.Wahr(w.Ausfuehren(konto, new KnotenWaehlen(o.Nummer, "Sorgfalt")).Erfolg, "Sorgfalt");
            var baum = w.Familien.Lebensbaum(o, Talent.Handwerk, konto)!;
            Pruefe.Gleich(9, baum.Stuecke.Count);
            Pruefe.Gleich("Werkstatt,Gesinde,Bau", string.Join(",", baum.Spalten));
            Pruefe.Gleich(3, baum.Stufe);
            BaumStueck S(string k) => baum.Stuecke.First(s => s.Kennung == k);
            Pruefe.Gleich(Baumzustand.Gekauft, S("Sorgfalt").Zustand);
            Pruefe.Gleich(Baumzustand.Kaufbar, S("Kniffe").Zustand);
            Pruefe.Gleich(Baumzustand.Kaufbar, S("Lehrherr").Zustand);
            Pruefe.Gleich(Baumzustand.Gesperrt, S("Meisterstueck").Zustand);
            Pruefe.Gleich("Sorgfalt", S("Kniffe").Voraussetzung);
            o.Lebensweg.ErfahrungSetzen(Talent.Handwerk, 100);
            Pruefe.Gleich(Baumzustand.NichtJetzt, w.Familien.Lebensbaum(o, Talent.Handwerk, konto)!.Stuecke.First(s => s.Kennung == "Kniffe").Zustand);
            o.Lebensweg.ErfahrungSetzen(Talent.Handwerk, 200);
            Pruefe.Wahr(w.Familien.Lebensbaum(o, Talent.Handwerk, fremd)!.Stuecke.All(s => s.Zustand != Baumzustand.Kaufbar), "für Fremde nichts nehmbar");
        }),

        ("Lebensweise: Werkstatt-Knoten wirken in allen Betrieben des Hauses wie Einrichtung – nur die des Oberhaupts, Meisterstück erst ab Stufe 3", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var e = Heiraten(w, konto);
            var schmiede = w.BetriebEroeffnen("Schmiede Probe", Betriebsart.Schmiede, konto, startkarren: "");
            var gold = w.BetriebEroeffnen("Goldschmiede Probe", Betriebsart.Goldschmiede, konto, startkarren: "");
            var zeit = schmiede.ZeitFaktor("Dolch");
            var unterhalt = schmiede.Unterhalt;
            var preis = gold.PreisFaktor;
            Nehmen(w, konto, e, "Sorgfalt", "Kniffe");
            Pruefe.Ungefaehr(zeit, schmiede.ZeitFaktor("Dolch"), 1e-12); // Gegenprobe: Knoten des Ehepartners wirken nicht im Betrieb
            Nehmen(w, konto, f.Oberhaupt, "Sorgfalt", "Kniffe", "Meisterstueck", "Ordnung", "Baumeister", "HueterDesHauses");
            Pruefe.Ungefaehr(zeit * 0.97, schmiede.ZeitFaktor("Dolch"), 1e-12);
            Pruefe.Ungefaehr(unterhalt * 0.9, schmiede.Unterhalt, 1e-9);
            Pruefe.Ungefaehr(preis * 1.04, gold.PreisFaktor, 1e-12);
            Pruefe.Ungefaehr(1, schmiede.PreisFaktor, 1e-12); // Stufe 2: ohne Meisterstück
            var neu = w.BetriebEroeffnen("Gießerei Neu", Betriebsart.Giesserei, konto, startkarren: "");
            Pruefe.Ungefaehr(0.97, neu.ZeitFaktor("Silberring"), 1e-12); // auch ein später eröffneter Betrieb
        }),

        ("Lebensweise: Haus-Knoten – Erlös, Zins, Steuer, Ansehen, Wahlpunkte, Gericht, Bau, Jagd; ohne Knoten unverändert (Gegenprobe)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var (anderes, _) = Haus(w, "Haus Anders");
            var o = f.Oberhaupt;
            var vk = w.Familien.VerkaufsFaktor(konto);
            var vkAnders = w.Familien.VerkaufsFaktor(anderes);
            var steuer = w.Befugnisse.SteuersatzMarkt(konto);
            var ansehen = w.Rat.Ansehen(konto);
            var wahl = w.Familien.RhetorikPunkte(konto);
            var plaedoyer = w.Familien.Plaedoyer(konto, anderes);
            var gebuehr = w.Justiz.Gerichtsgebuehr(konto);
            var haushalt = w.Familien.Haushaltskosten(f);
            Nehmen(w, konto, o, "Marktschreier", "Feilscher", "Handelsherr", "Rechenkunst", "Haushaelter", "Steuerkniff", "OffeneHand", "Geschaeftsfreund", "Goenner");
            Pruefe.Ungefaehr(vk + 0.03, w.Familien.VerkaufsFaktor(konto), 1e-12);
            Pruefe.Ungefaehr(steuer * 0.8, w.Befugnisse.SteuersatzMarkt(konto), 1e-12);
            Pruefe.Ungefaehr(ansehen + 2, w.Rat.Ansehen(konto), 1e-9);
            Pruefe.Ungefaehr(haushalt * 0.95, w.Familien.Haushaltskosten(f), 1e-9);
            Pruefe.Ungefaehr(vkAnders, w.Familien.VerkaufsFaktor(anderes), 1e-12); // Gegenprobe: das andere Haus nicht
            Nehmen(w, konto, o, "WohlgesetzteWorte", "Plaedoyer", "Rechtskundig");
            Pruefe.Ungefaehr(wahl + 2, w.Familien.RhetorikPunkte(konto), 1e-12);
            Pruefe.Ungefaehr(plaedoyer + 3, w.Familien.Plaedoyer(konto, anderes), 1e-12);
            Pruefe.Ungefaehr(gebuehr * 0.5, w.Justiz.Gerichtsgebuehr(konto), 1e-9);
            Pruefe.Gleich(2, w.Meinungen.ApKosten(konto, "Jagd"));
            Nehmen(w, konto, o, "Weidmann", "Jagdherr", "Jagdgesellschaft");
            Pruefe.Gleich(1, w.Meinungen.ApKosten(konto, "Jagd"));
            Pruefe.Gleich(2, w.Meinungen.ApKosten(anderes, "Jagd"));
            Pruefe.Gleich(1, w.ApKosten(konto, new Aktivitaet("Jagd", anderes.Inhaber)));
            Pruefe.Ungefaehr(Math.Min(0.95, w.Daten.Justiz!.SpitzelChance), w.Justiz.SpitzelChance(anderes, konto), 1e-12);
            Nehmen(w, konto, o, "OhrAnDerWand");
            Nehmen(w, anderes, w.Familien.Von(anderes)!.Oberhaupt, "WachsamesAuge", "Verschwiegen", "FalscheFaehrte");
            Pruefe.Ungefaehr((w.Daten.Justiz.SpitzelChance + 0.1) * 0.7, w.Justiz.SpitzelChance(konto, anderes), 1e-12);
        }),

        ("Lebensweise: Leise Sohlen – die Taschendiebe des Hauses bringen 5 % mehr Beute; ein Haus ohne den Knoten nicht (Gegenprobe)", () =>
        {
            // Niemand wird am Markt erwischt – die Beute ist dann genau Minuten × Gulden je Minute (wie in DiebTests).
            var daten = Wirtschaftsdaten.AusJson(Json.Value.Replace("\"fangJeStunde\": 0.009", "\"fangJeStunde\": 0"));
            var sim = NeueSimulation(daten: daten);
            var w = sim.Wirtschaft!;
            var (mit, f) = Haus(w, "Haus Elster");
            var (ohne, _) = Haus(w, "Haus Rabe");
            foreach (var k in new[] { mit, ohne })
            {
                var b = w.BetriebEroeffnen($"Schmugglerloch {k.Inhaber}", Betriebsart.Schmugglerloch, k, startkarren: "");
                w.Einstellen(b, "Geselle", b.Plaetze);
            }
            Nehmen(w, mit, f.Oberhaupt, "LeiseSohlen");
            Pruefe.Ungefaehr(1.05, w.Familien.Lebenswirkung(mit).BeuteFaktor, 1e-12);
            for (var i = 0; i < 20 * 60 + 1 - Reichsgilde.Core.Zeit.Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // bis nach der Arbeitszeit
            var ohneBeute = w.Diebe.DiesesJahr(ohne).Beute;
            Pruefe.Ungefaehr(4 * 720 * w.Diebe.WertJeMinute(1), ohneBeute, 1e-6);
            Pruefe.Ungefaehr(ohneBeute * 1.05, w.Diebe.DiesesJahr(mit).Beute, 1e-6);
        }),

        ("Lebensweise: Baumeister – Einrichtung und Ausbau 10 % billiger", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe", geld: 200_000);
            var b = w.BetriebEroeffnen("Gießerei Probe", Betriebsart.Giesserei, konto, startkarren: "");
            var stueck = w.Daten.Werkstatt!.EinrichtungFuer(b.Artdaten.Beruf).First(e => e.AbStufe == 1 && e.Voraussetzung is null);
            Nehmen(w, konto, f.Oberhaupt, "Ordnung", "Baumeister");
            var vorher = konto.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(konto, new EinrichtungKaufen(b.Name, stueck.Kennung)).Erfolg, "Einrichtung");
            Pruefe.Ungefaehr(stueck.Preis * 0.9, vorher - konto.Vermoegen, 1e-6);
        }),

        ("Lebensweise: weitere Knoten – Wohnhaus-Ausbau und Unterhalt, Schutz nur der Karren (nicht der Schiffe), Gnadengesuch, Fürsprecher; Gegenprobe ohne Knoten", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe", geld: 200_000);
            var (anderes, _) = Haus(w, "Haus Anders");
            var o = f.Oberhaupt;
            var schutz = w.Familien.KampfSchutz(konto);
            var busse = w.Justiz.DrohendeBusse(anderes, konto);
            Pruefe.Ungefaehr(0, w.Rat.AntragSchwelleMinus(konto), 1e-12);
            Pruefe.Ungefaehr(schutz, w.Familien.KarrenSchutz(konto), 1e-12);
            Nehmen(w, konto, o, "Ordnung", "Baumeister", "HueterDesHauses", "BewaffneteKnechte", "Plaedoyer", "Rechtskundig", "Gnadengesuch",
                "WohlgesetzteWorte", "Fuersprecher");
            Pruefe.Ungefaehr(schutz + 0.05, w.Familien.KarrenSchutz(konto), 1e-12);
            Pruefe.Ungefaehr(schutz, w.Familien.KampfSchutz(konto), 1e-12); // Schiffe: nur das Talent
            Pruefe.Ungefaehr(busse * 0.75, w.Justiz.DrohendeBusse(anderes, konto), 1e-9);
            Pruefe.Ungefaehr(5, w.Rat.AntragSchwelleMinus(konto), 1e-12);
            Pruefe.Ungefaehr(0, w.Rat.AntragSchwelleMinus(anderes), 1e-12);
            var stufe = w.Wohnen.NaechsteStufe(konto)!;
            var vorher = konto.Vermoegen;
            var ausbau = w.Ausfuehren(konto, new WohnhausAusbauen());
            Pruefe.Wahr(ausbau.Erfolg, ausbau.Meldung);
            Pruefe.Ungefaehr(stufe.Ausbau * 0.9, vorher - konto.Vermoegen, 1e-6);
            w.Wohnen.Minute(new Reichsgilde.Core.Zeit.Spielzeit(konto.WohnhausFertigAb!.Value));
            Pruefe.Gleich(stufe, w.Wohnen.Stufe(konto));
            Pruefe.Ungefaehr(stufe.Unterhalt * 0.9, w.Wohnen.Unterhalt(konto), 1e-9);
            Pruefe.Ungefaehr(w.Wohnen.Stufe(anderes)!.Unterhalt, w.Wohnen.Unterhalt(anderes), 1e-9);
        }),

        ("Lebensweise: persönliche Knoten – Liebenswürdig und Geschäftsfreund heben die Meinung über die Person, Eisern senkt ihre Krankheit", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var (anderes, g) = Haus(w, "Haus Anders");
            var e = Heiraten(w, konto);
            var r = w.Rat.Ratsherren[0];
            var vonRat = w.Meinungen.Meinung(r, e);
            var vonOberhaupt = w.Meinungen.Meinung(g.Oberhaupt, e);
            var krank = w.Familien.KrankheitFaktor(e);
            Nehmen(w, konto, e, "Gastgeber", "Brautwerber", "Liebenswuerdig", "OffeneHand", "Geschaeftsfreund", "Hauswehr", "WehrhafteWerkstatt", "Eisern");
            Pruefe.Ungefaehr(vonRat + 5, w.Meinungen.Meinung(r, e), 1e-9);              // Ratsherr: nur Liebenswürdig
            Pruefe.Ungefaehr(vonOberhaupt + 10, w.Meinungen.Meinung(g.Oberhaupt, e), 1e-9); // Oberhaupt: beides
            Pruefe.Ungefaehr(krank * 0.8, w.Familien.KrankheitFaktor(e), 1e-12);
            Pruefe.Ungefaehr(1, w.Familien.Lebenswirkung(konto).WohnhausFaktor, 1e-12); // Hauswehr wirkt nur fürs Oberhaupt
        }),

        ("Lebensweise: Erbfall – der Erbe behält seine Knoten, bekommt 20 % Vermächtnis im Pfad des Toten (Nutzer 9.11.2026: wieder 20 %, Datenversion 75 hatte 35 %); dessen Knoten wirken nicht mehr", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var o = f.Oberhaupt;
            var erbe = w.Familien.KindAufnehmen(konto, 20, Geschlecht.Mann);
            Pruefe.Wahr(w.Familien.HatLebensweg(erbe), "der Erbe sammelt");
            var schmiede = w.BetriebEroeffnen("Schmiede Probe", Betriebsart.Schmiede, konto, startkarren: "");
            var zeit = schmiede.ZeitFaktor("Dolch");
            Nehmen(w, konto, o, "Sorgfalt", "Kniffe");
            w.Ausfuehren(konto, new LebenswegWaehlen(o.Nummer, Talent.Handwerk));
            o.Lebensweg.ErfahrungSetzen(Talent.Handwerk, 1000);
            Nehmen(w, konto, erbe, "Marktschreier");
            erbe.Lebensweg.ErfahrungSetzen(Talent.Handwerk, 0);
            Pruefe.Ungefaehr(zeit * 0.97, schmiede.ZeitFaktor("Dolch"), 1e-12);
            w.Familien.AnKrankheitSterben(o, "an der Probe");
            Pruefe.Gleich(erbe, f.Oberhaupt);
            Pruefe.Ungefaehr(200, erbe.Lebensweg.Erfahrung(Talent.Handwerk), 1e-9);
            Pruefe.Wahr(erbe.Lebensweg.HatKnoten("Marktschreier") && !erbe.Lebensweg.HatKnoten("Kniffe"), "eigene Knoten, keine des Toten");
            Pruefe.Ungefaehr(zeit, schmiede.ZeitFaktor("Dolch"), 1e-12);
            Pruefe.Ungefaehr(1 + w.Familien.Talent(konto, Talent.Verhandeln) * w.Familien.TalentDaten.Wirkung.Verhandeln + 0.01, w.Familien.VerkaufsFaktor(konto), 1e-12);
        }),

        ("Lebensweise: KI-Häuser wählen Pfad und Knoten (nach Talent und Zügen) – über Befehle, alle Voraussetzungen erfüllt", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 21 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 12; i++) JahrBeenden(sim);
            var d = w.Familien.LebensweiseDaten!;
            var mitKnoten = 0;
            foreach (var r in w.Rivalen)
            {
                if (w.Familien.Von(r.Konto) is not { } f || !r.Konto.IstAktiv) continue;
                var o = f.Oberhaupt;
                Pruefe.Wahr(o.Lebensweg.Pfad is not null, $"{r.Konto.Inhaber}: Pfad gewählt");
                foreach (var k in o.Lebensweg.Knoten)
                {
                    var (pfad, kd) = d.Knoten(k)!.Value;
                    Pruefe.Wahr(d.Voraussetzung(pfad, kd) is not { } v || o.Lebensweg.Knoten.Contains(v), $"{k}: Voraussetzung");
                }
                // Höchstens der Punkt vom letzten Jahresende liegt noch – den nimmt die KI in ihrer nächsten Politikstunde.
                Pruefe.Wahr(TalentTexte.Alle.Sum(t => w.Familien.FreiePunkte(o, t)) <= 1, $"{r.Konto.Inhaber}: kein Punkt bleibt liegen");
                if (o.Lebensweg.Knoten.Count > 0) mitKnoten++;
            }
            Pruefe.Wahr(mitKnoten > 0, "mindestens ein KI-Oberhaupt hat Knoten");
        }),

        ("Lebensweise: Speichern und Laden – Pfad, Erfahrung und Knoten bleiben, die Partie läuft gleich weiter", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 23 },
                Daten.Value, new NeuePartie());
            for (var i = 0; i < 4; i++) JahrBeenden(sim);
            var o = sim.Wirtschaft!.Familien.Von(spieler)!.Oberhaupt;
            Nehmen(sim.Wirtschaft, spieler, o, "Sorgfalt");
            var zeitpunkt = new DateTime(2026, 11, 8, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            Pruefe.Wahr(json.Contains("\"Lebensweg\""), "Lebensweise im Spielstand");
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
            var neu = geladen.Wirtschaft!.Familien.Von(geladenerSpieler!)!.Oberhaupt;
            Pruefe.Wahr(neu.Lebensweg.HatKnoten("Sorgfalt"), "Knoten geladen");
            Pruefe.Ungefaehr(o.Lebensweg.Erfahrung(Talent.Handwerk), neu.Lebensweg.Erfahrung(Talent.Handwerk), 1e-9);
            var b = geladen.Wirtschaft.Betriebe.First(b => b.Konto == geladenerSpieler && b.IstWerkstatt);
            var bOriginal = sim.Wirtschaft.Betriebe.First(x => x.Name == b.Name);
            Pruefe.Ungefaehr(bOriginal.UnfallFaktor, b.UnfallFaktor, 1e-12);
            Pruefe.Wahr(b.Wirkungen.Any(x => Math.Abs(x.UnfallFaktor - 0.8) < 1e-12), "Sorgfalt wirkt nach dem Laden im Betrieb");
            for (var i = 0; i < 3; i++) { JahrBeenden(sim); JahrBeenden(geladen); }
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, zeitpunkt), Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
        }),

        ("Lebensweise: Spielstand ohne Lebensweise (Daten ohne Abschnitt) lädt, läuft ein Jahr, speichert und lädt gleich – niemand sammelt", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 24 },
                Ohne.Value, new NeuePartie());
            var json = Speicherung.Speichern(sim, spieler);
            Pruefe.Wahr(!json.Contains("\"Lebensweg\""), "kein Feld ohne Lebensweise");
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Wahr(!geladen.Wirtschaft!.Familien.HatLebensweise, "keine Lebensweise");
            JahrBeenden(geladen);
            var zeitpunkt = new DateTime(2026, 11, 8, 12, 0, 0);
            var nachJahr = Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt);
            Pruefe.Wahr(!nachJahr.Contains("\"Lebensweg\""), "niemand hat gesammelt");
            var (wieder, wiederSpieler) = Speicherung.Laden(nachJahr);
            Pruefe.Gleich(nachJahr, Speicherung.Speichern(wieder, wiederSpieler, zeitpunkt));
            var o = geladen.Wirtschaft.Familien.Von(geladenerSpieler!)!.Oberhaupt;
            Pruefe.Wahr(!geladen.Wirtschaft.Ausfuehren(geladenerSpieler!, new LebenswegWaehlen(o.Nummer, Talent.Kampf)).Erfolg, "Befehl abgelehnt");
        }),
    };
}
