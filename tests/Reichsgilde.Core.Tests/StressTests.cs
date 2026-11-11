using System.IO.Compression;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests für den Stress (Charaktersystem Teil 3b): Daten, wer Stress hat, Quellen (Taten gegen den Charakter, Trauer, Haus, Gericht, Krankheit),
/// Ventile, Abklingen, Stufen und Folgen, Zusammenbruch und Spielstand – jeweils mit Gegenprobe.
/// </summary>
static class StressTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    /// <summary>Dieselben Daten ohne den Abschnitt "stress" – wie vor Teil 3b (alte Spielstände bringen solche Daten mit).</summary>
    static readonly Lazy<Wirtschaftsdaten> Ohne = new(() =>
    {
        var knoten = JsonNode.Parse(Json.Value)!.AsObject();
        knoten.Remove("stress");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    });

    static Simulation NeueSimulation(int startwert = 1400, Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, params string[] zuege)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: 50_000);
        var f = w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: Konfession.Katholisch);
        konto.Aktionspunkte.Setzen(12);
        Zuege(w, f.Oberhaupt, zuege);
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
        var e = w.Familien.Von(konto)!.Ehepartner!;
        e.ZuegeLeeren();
        return e;
    }

    static Ratsherr Ratsherr(Stadtwirtschaft w, int nummer = 0) => w.Rat.Ratsherren[nummer];

    static Ratsherr Katholisch(Stadtwirtschaft w) => w.Rat.Ratsherren.First(r => w.Glaube.GlaubeVon(r) == Konfession.Katholisch);

    static double Stress(Stadtwirtschaft w, Person p) => w.Familien.Stress(p);

    /// <summary>Steht Stress im Spielstand (außer in den mitgebrachten Daten, die den Abschnitt „Stress“ immer als Feld führen)?</summary>
    static bool StressImSpielstand(string json)
    {
        var n = JsonNode.Parse(json)!.AsObject();
        foreach (var k in n.Select(p => p.Key).Where(k => k.Equals("daten", StringComparison.OrdinalIgnoreCase)).ToList()) n.Remove(k);
        var text = n.ToJsonString();
        return text.Contains("\"Stress\"", StringComparison.Ordinal) || text.Contains("\"StressWuerfe\"", StringComparison.Ordinal);
    }

    static void Ok(Stadtwirtschaft w, Konto konto, Befehl befehl)
    {
        var e = w.Ausfuehren(konto, befehl);
        Pruefe.Wahr(e.Erfolg, $"{befehl.GetType().Name}: {e.Meldung}");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Stress: Daten – Datenversion 77, Stufen ab 40 (angespannt) und 70 (zermürbt), Höchstwert 100, danach 60; Ventile Wirtshaus, Messe, Jagd; " +
         "neue erworbene Züge Schwermütig und Reizbar ohne Anlass", () =>
        {
            var d = Daten.Value.Stress!;
            Pruefe.Gleich(83, Daten.Value.Version);
            Pruefe.Wahr(d.IstAktiv, "Stress aktiv");
            Pruefe.Gleich("angespannt:40,zermürbt:70", string.Join(",", d.Stufen.Select(s => $"{s.Name}:{s.Ab}")));
            Pruefe.Gleich(100.0, d.Hoechstwert);
            Pruefe.Gleich(60.0, d.NachZusammenbruch);
            Pruefe.Gleich("Zechen=15,Messe=15,Jagd=30", string.Join(",", d.Ventile.Select(v => $"{v.Key}={v.Value}")));
            Pruefe.Gleich("Trinker,Schwermuetig,Reizbar", string.Join(",", d.Zusammenbruch.Zuege));
            var z = Daten.Value.Charakterzuege!;
            foreach (var k in new[] { "Schwermuetig", "Reizbar" })
            {
                Pruefe.Gleich(Zugart.Erworben, z.Zug(k)!.Art);
                Pruefe.Gleich(0, z.Zug(k)!.Daten.Anlaesse.Count); // kommen nur mit dem Zusammenbruch
            }
            Pruefe.Gleich(-0.5, z.Zug("Schwermuetig")!.Talent(Talent.Handwerk));
            Pruefe.Gleich(0.5, z.Zug("Reizbar")!.Talent(Talent.Kampf));
            Pruefe.Gleich(-5.0, z.Zug("Reizbar")!.Daten.Meinung.Allgemein);
            Pruefe.Gleich(-5.0, d.ZugAbklingen["Schwermuetig"]);
            Pruefe.Wahr(d.Taten.Count >= 20, "Taten gegen den Charakter");
            // Gegenprobe: ohne Abschnitt gibt es keinen Stress
            Pruefe.Wahr(Ohne.Value.Stress is null, "ohne Abschnitt");
        }),

        ("Stress: Daten werden geprüft – unbekannte Tat, unbekannter Zug, unbekannte Aktivität und Krankheit, ungeordnete Stufen", () =>
        {
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"tat\": \"Erpressen\", \"stress\": 15", "\"tat\": \"Kuchenbacken\", \"stress\": 15")),
                "„Kuchenbacken“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("{ \"zug\": \"Ehrlich\", \"tat\": \"Beauftragen\"", "{ \"zug\": \"Bucklig\", \"tat\": \"Beauftragen\"")),
                "„Bucklig“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"ventile\": { \"Zechen\": 15", "\"ventile\": { \"Tanzen\": 15")), "„Tanzen“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"name\": \"zermürbt\", \"ab\": 70", "\"name\": \"zermürbt\", \"ab\": 30")), "aufsteigend");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"Haarausfall\" ] }", "\"Schnupfen\" ] }")), "„Schnupfen“");
            // Gegenprobe: die echten Daten laden
            Pruefe.Wahr(Wirtschaftsdaten.AusJson(Json.Value).Stress is { IstAktiv: true }, "echte Daten laden");
        }),

        ("Stress: Wer Stress hat – Oberhaupt, Ehepartner und volljähriger Erbe, nicht das Kind und nicht die Brautschau; ohne Abschnitt niemand", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Probe");
            var e = Heiraten(w, konto);
            var erbe = w.Familien.KindAufnehmen(konto, 25);
            var kind = w.Familien.KindAufnehmen(konto, 5);
            Pruefe.Wahr(w.Familien.HatStress(f.Oberhaupt) && w.Familien.HatStress(e) && w.Familien.HatStress(erbe), "Oberhaupt, Ehepartner, Erbe");
            Pruefe.Wahr(!w.Familien.HatStress(kind), "das Kind nicht");
            Pruefe.Wahr(!w.Familien.HatStress(w.Familien.Kandidaten[0].Person), "die Brautschau nicht");
            w.Familien.StressAendern(kind, 30, "Probe");
            Pruefe.Gleich(0.0, Stress(w, kind));
            w.Familien.StressAendern(erbe, 30, "Probe");
            Pruefe.Gleich(30.0, Stress(w, erbe));
            // Gegenprobe: ohne Abschnitt "stress"
            var w2 = NeueSimulation(daten: Ohne.Value).Wirtschaft!;
            var (_, f2) = Haus(w2, "Haus Ohne");
            Pruefe.Wahr(!w2.Familien.HatStresswesen && !w2.Familien.HatStress(f2.Oberhaupt), "ohne Abschnitt kein Stress");
            w2.Familien.StressAendern(f2.Oberhaupt, 50, "Probe");
            Pruefe.Gleich(0.0, Stress(w2, f2.Oberhaupt));
        }),

        ("Stress: Taten gegen den Charakter – ehrlich und fromm bestellt Krötenschleim (+10, +15), verschlagen nichts; ein abgelehnter Befehl bringt nichts", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (ziel, _) = Haus(w, "Haus Ziel");
            var (a, fa) = Haus(w, "Haus Ehrlich", "Ehrlich", "Fromm");
            var (b, fb) = Haus(w, "Haus Schlau", "Verschlagen", "Zynisch");
            var befehl = new Beauftragen(ziel.Inhaber, Gaunerstueck.Kroetenschleim);
            Pruefe.Gleich(25.0, w.Familien.StressDurch(a, befehl)); // Vorschau für KI und Oberfläche
            Ok(w, a, befehl);
            Pruefe.Gleich(25.0, Stress(w, fa.Oberhaupt));
            var texte = string.Join(" | ", fa.Oberhaupt.Stresslage.Eintraege.Select(x => x.Text));
            Pruefe.Wahr(texte.Contains("Gaunerstück in Auftrag gegeben (ehrlich)") && texte.Contains("Krötenschleim in Auftrag gegeben (fromm)"), texte);
            Ok(w, b, befehl);
            Pruefe.Gleich(0.0, Stress(w, fb.Oberhaupt)); // Gegenprobe: verschlagen
            // Gegenprobe: abgelehnt (kein Geld) – kein Stress
            var (c, fc) = Haus(w, "Haus Arm", "Ehrlich");
            c.Gaunerkosten(c.Vermoegen);
            Pruefe.Wahr(!w.Ausfuehren(c, befehl).Erfolg, "ohne Geld abgelehnt");
            Pruefe.Gleich(0.0, Stress(w, fc.Oberhaupt));
        }),

        ("Stress: Träge übt (+5), Fleißig nicht; Gierig schenkt ab 200 Gulden (+5), darunter nicht; Ehrlich besticht einen Ratsherrn (+5)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (a, fa) = Haus(w, "Haus Faul", "Traege");
            var (b, fb) = Haus(w, "Haus Emsig", "Fleissig");
            Ok(w, a, new TalentTrainieren(Talent.Kampf));
            Ok(w, b, new TalentTrainieren(Talent.Kampf));
            Pruefe.Gleich(5.0, Stress(w, fa.Oberhaupt));
            Pruefe.Gleich(0.0, Stress(w, fb.Oberhaupt));
            var (g, fg) = Haus(w, "Haus Geiz", "Gierig");
            Ok(w, g, new Schenken(Ratsherr(w, 0).Name, 100));
            Pruefe.Gleich(0.0, Stress(w, fg.Oberhaupt)); // unter 200 nichts
            Ok(w, g, new Schenken(Ratsherr(w, 1).Name, 250));
            Pruefe.Gleich(5.0, Stress(w, fg.Oberhaupt));
            var (h, fh) = Haus(w, "Haus Redlich", "Ehrlich");
            Ok(w, h, new Aktivitaet("Geschenk", Ratsherr(w, 2).Kennung, 50)); // auch als Aktivität ist ein Geschenk an einen Ratsherrn Bestechung
            Pruefe.Gleich(5.0, Stress(w, fh.Oberhaupt));
            Pruefe.Wahr(fh.Oberhaupt.Stresslage.Eintraege[^1].Text.Contains("Ratsherrn beschenkt"), "Text der Quelle");
        }),

        ("Stress: Ventile – Zechen baut 15 ab, wer es mag 22,5, wer es nicht mag bekommt 8; je Art einmal im Jahr, Messe trotzdem; im nächsten Jahr wieder", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (a, fa) = Haus(w, "Haus Ruhe");
            var (b, fb) = Haus(w, "Haus Zecher", "Gesellig");
            var (c, fc) = Haus(w, "Haus Fromm", "Fromm");
            foreach (var f in new[] { fa, fb, fc }) w.Familien.StressSetzen(f.Oberhaupt, 50);
            Ok(w, a, new Aktivitaet("Zechen", Ratsherr(w, 0).Kennung));
            Ok(w, b, new Aktivitaet("Zechen", Ratsherr(w, 0).Kennung));
            Ok(w, c, new Aktivitaet("Zechen", Ratsherr(w, 0).Kennung));
            Pruefe.Ungefaehr(35, Stress(w, fa.Oberhaupt), 1e-9);
            Pruefe.Ungefaehr(27.5, Stress(w, fb.Oberhaupt), 1e-9);
            Pruefe.Ungefaehr(58, Stress(w, fc.Oberhaupt), 1e-9); // mag es nicht
            Ok(w, a, new Aktivitaet("Zechen", Ratsherr(w, 1).Kennung)); // dieselbe Art im selben Jahr: kein Abbau mehr
            Pruefe.Ungefaehr(35, Stress(w, fa.Oberhaupt), 1e-9);
            Pruefe.Gleich(0.0, w.Familien.StressDurch(a, new Aktivitaet("Zechen", Ratsherr(w, 2).Kennung)));
            Ok(w, a, new Aktivitaet("Messe", Katholisch(w).Kennung)); // eine andere Art schon
            Pruefe.Ungefaehr(20, Stress(w, fa.Oberhaupt), 1e-9);
            JahrBeenden(sim);
            Zuege(w, fa.Oberhaupt); // ohne Persönlichkeit hätte der Jahreswechsel ihm gerade drei Züge gegeben
            w.Familien.StressSetzen(fa.Oberhaupt, 50);
            a.Aktionspunkte.Setzen(12);
            Ok(w, a, new Aktivitaet("Zechen", Ratsherr(w, 0).Kennung)); // neues Jahr: wieder frei
            Pruefe.Ungefaehr(35, Stress(w, fa.Oberhaupt), 1e-9);
            // Ohne Stress verbraucht ein Ventil die Art nicht (Befund der Prüfung): erst ruhig zur Messe, dann gestresst noch einmal
            w.Familien.StressSetzen(fb.Oberhaupt, 0);
            b.Aktionspunkte.Setzen(12);
            Ok(w, b, new Aktivitaet("Zechen", Ratsherr(w, 1).Kennung));
            Pruefe.Gleich(0, fb.Oberhaupt.Stresslage.Ventile.Count);
            w.Familien.StressSetzen(fb.Oberhaupt, 50);
            Ok(w, b, new Aktivitaet("Zechen", Ratsherr(w, 2).Kennung));
            Pruefe.Ungefaehr(27.5, Stress(w, fb.Oberhaupt), 1e-9); // gesellig: 22,5
        }),

        ("Stress: Gastmahl ist kein Ventil (ruhig bleibt), Scheu bekommt 8; Zechen mit dem eigenen Ehepartner senkt den Stress beider", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (a, fa) = Haus(w, "Haus Gast");
            var (b, fb) = Haus(w, "Haus Scheu", "Scheu");
            foreach (var f in new[] { fa, fb }) w.Familien.StressSetzen(f.Oberhaupt, 50);
            Ok(w, a, new Aktivitaet("Gastmahl", Ratsherr(w, 0).Kennung));
            Ok(w, b, new Aktivitaet("Gastmahl", Ratsherr(w, 0).Kennung));
            Pruefe.Ungefaehr(50, Stress(w, fa.Oberhaupt), 1e-9);
            Pruefe.Ungefaehr(58, Stress(w, fb.Oberhaupt), 1e-9);
            var (c, fc) = Haus(w, "Haus Ehe");
            var e = Heiraten(w, c);
            w.Familien.StressSetzen(fc.Oberhaupt, 40);
            w.Familien.StressSetzen(e, 40);
            Ok(w, c, new Aktivitaet("Zechen", e.Kennung));
            Pruefe.Ungefaehr(25, Stress(w, fc.Oberhaupt), 1e-9);
            Pruefe.Ungefaehr(25, Stress(w, e), 1e-9);
            // Das Gegenüber hat die Aktivität nicht gewählt: Mag es sie nicht, bekommt es keine +8, nur keinen Abbau (Befund der Prüfung)
            var (d, fd) = Haus(w, "Haus Fromme Frau");
            var e2 = Heiraten(w, d);
            Zuege(w, e2, "Fromm");
            w.Familien.StressSetzen(fd.Oberhaupt, 40);
            w.Familien.StressSetzen(e2, 40);
            Ok(w, d, new Aktivitaet("Zechen", e2.Kennung));
            Pruefe.Ungefaehr(25, Stress(w, fd.Oberhaupt), 1e-9);
            Pruefe.Ungefaehr(40, Stress(w, e2), 1e-9);
        }),

        ("Stress: Trauer – stirbt ein Kind, +20 für beide Eltern; stirbt der Ehepartner, +30; stirbt das Oberhaupt, +10 für den Erben (Elternteil)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Leid");
            var o = f.Oberhaupt;
            var e = Heiraten(w, konto);
            var kind = w.Familien.EntwicklerGeburt(f, Geschlecht.Frau);
            w.Familien.AnKrankheitSterben(kind, "an der Ruhr");
            Pruefe.Gleich(20.0, Stress(w, o));
            Pruefe.Gleich(20.0, Stress(w, e));
            Pruefe.Wahr(o.Stresslage.Eintraege[^1].Text.Contains($"Trauer um Tochter {kind.Vorname}"), o.Stresslage.Eintraege[^1].Text);
            w.Familien.AnKrankheitSterben(e, "an der Ruhr");
            Pruefe.Gleich(50.0, Stress(w, o));
            var (k2, f2) = Haus(w, "Haus Erbe");
            var erbe = w.Familien.KindAufnehmen(k2, 25);
            var kind2 = w.Familien.KindAufnehmen(k2, 5);
            w.Familien.AnKrankheitSterben(f2.Oberhaupt, "an der Ruhr");
            Pruefe.Wahr(f2.Oberhaupt == erbe, "der Erbe führt das Haus");
            Pruefe.Gleich(10.0, Stress(w, erbe));
            Pruefe.Gleich(0.0, kind2.Stresslage.Wert); // Gegenprobe: das Kind hat keinen Stress
        }),

        ("Stress: Haus, Gericht, Krankheit – Bargeld im Minus +10 am Jahresende, angeklagt +15, feiges Oberhaupt klagt an +5, erkrankt +5", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (a, fa) = Haus(w, "Haus Feige", "Feige");
            var (b, fb) = Haus(w, "Haus Beklagt");
            var spur = w.Justiz.TatMelden(b, Vergehen.Diebstahl, "Haus Feige", 100)!;
            w.Justiz.BeweisGeben(a, spur);
            Ok(w, a, new Anklagen(b.Inhaber));
            Pruefe.Gleich(5.0, Stress(w, fa.Oberhaupt));
            Pruefe.Gleich(15.0, Stress(w, fb.Oberhaupt));
            Pruefe.Wahr(fb.Oberhaupt.Stresslage.Eintraege[^1].Text.Contains("Angeklagt von Haus Feige"), "Text");
            Pruefe.Wahr(w.Gesundheit.Erkranken(b, fb.Oberhaupt, "Husten"), "erkrankt");
            Pruefe.Gleich(20.0, Stress(w, fb.Oberhaupt));
            var (c, fc) = Haus(w, "Haus Pleite");
            w.Familien.StressSetzen(fc.Oberhaupt, 50);
            c.Gaunerkosten(c.Vermoegen + 100); // Bargeld −100
            w.Familien.StressImJahreswechsel(fc);
            Pruefe.Gleich(45.0, Stress(w, fc.Oberhaupt)); // +10, dann −15
            Pruefe.Gleich("Bargeld im Minus +10 | Abklingen −15",
                string.Join(" | ", fc.Oberhaupt.Stresslage.Eintraege.Select(x => $"{x.Text} {Zahlenformat.MitVorzeichen(x.Wert)}")));
        }),

        ("Stress: weitere Quellen – Jahr mit Verlust, verfallene AP (fleißig), Buße (gierig), Pranger (hochmütig), Ausbau und Neubau zum Stadtpalais " +
         "(bescheiden), Ketzerei (fromm), Erpressen (ehrlich und großzügig), Streit, Pest im Haus, Trauer um Geschwister und Verschleppte – mit Gegenproben", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            // Jahr mit Verlust (Betriebsgewinn unter 0) und verfallende Aktionspunkte beim fleißigen Oberhaupt
            var (a, fa) = Haus(w, "Haus Verlust", "Fleissig");
            a.Schaden(5_000);
            a.JahrAbschliessen(a.Laufend.Jahr + 1);
            w.Familien.StressImJahreswechsel(fa);
            var texte = string.Join(" | ", fa.Oberhaupt.Stresslage.Eintraege.Select(x => $"{x.Text} {Zahlenformat.MitVorzeichen(x.Wert)}"));
            Pruefe.Gleich("Ein Jahr mit Verlust +5 | Aktionspunkte verfallen lassen (fleißig) +5 | Abklingen −10", texte);
            var (b, fb) = Haus(w, "Haus Gewinn", "Traege"); // Gegenprobe: kein Verlust, träge lässt AP gern verfallen
            b.JahrAbschliessen(b.Laufend.Jahr + 1);
            w.Familien.StressImJahreswechsel(fb);
            Pruefe.Gleich(0, fb.Oberhaupt.Stresslage.Eintraege.Count);
            // Buße, Pranger (Ereignisse aus Gericht und Jahresende)
            var (c, fc) = Haus(w, "Haus Geiz", "Gierig", "Hochmuetig");
            w.Familien.StressTat(c, "Busse", 300);
            w.Familien.StressTat(c, "Pranger");
            Pruefe.Gleich(20.0, Stress(w, fc.Oberhaupt));
            // Wohnhaus zum Stadtpalais (Ausbau von Stufe 2, Neubau Stufe 3), nicht zum Steinhaus
            var (d, _) = Haus(w, "Haus Demut", "Bescheiden");
            d.WohnhausStufe = 2;
            Pruefe.Gleich(10.0, w.Familien.StressDurch(d, new WohnhausAusbauen()));
            Pruefe.Gleich(10.0, w.Familien.StressDurch(d, new WohnhausBauen(3, 0, 0, 0)));
            Pruefe.Gleich(10.0, w.Familien.StressDurch(d, new WohnhausBauen(4, 0, 0, 0)));
            Pruefe.Gleich(0.0, w.Familien.StressDurch(d, new WohnhausBauen(2, 0, 0, 0)));
            d.WohnhausStufe = 1;
            Pruefe.Gleich(0.0, w.Familien.StressDurch(d, new WohnhausAusbauen())); // zum Steinhaus
            // Ketzerei: Bekehrungspredigt gegen den Stadtglauben; gilt der eigene Glaube, nicht
            var (e, _) = Haus(w, "Haus Fromm", "Fromm");
            w.Glaube.Stadtglaube = Konfession.Evangelisch;
            Pruefe.Gleich(15.0, w.Familien.StressDurch(e, new Bekehrungspredigt()));
            w.Glaube.Stadtglaube = Konfession.Katholisch;
            Pruefe.Gleich(0.0, w.Familien.StressDurch(e, new Bekehrungspredigt()));
            // Erpressen: ehrlich +15, großzügig +10
            var (g, _) = Haus(w, "Haus Gut", "Ehrlich", "Grosszuegig");
            Pruefe.Gleich(25.0, w.Familien.StressDurch(g, new Erpressen(a.Inhaber, 500)));
            // Streit im Haus (Oberhaupt und Erbe), Pest im Haus (alle Stressträger einmal)
            var (h, fh) = Haus(w, "Haus Zank");
            var erbe = w.Familien.KindAufnehmen(h, 25);
            var kind = w.Familien.KindAufnehmen(h, 20);
            w.Familien.StressStreit(fh, erbe);
            Pruefe.Gleich(10.0, Stress(w, fh.Oberhaupt));
            Pruefe.Gleich(10.0, Stress(w, erbe));
            w.Familien.StressPestImHaus(h);
            Pruefe.Gleich(20.0, Stress(w, fh.Oberhaupt));
            Pruefe.Gleich(20.0, Stress(w, erbe));
            Pruefe.Gleich(0.0, Stress(w, kind)); // das zweite Kind ist kein Erbe
            // Trauer um Geschwister: das zweite Kind stirbt – der Erbe trauert um seine Schwester (+5), das Oberhaupt um die Tochter (+20)
            w.Familien.AnKrankheitSterben(kind, "an der Ruhr");
            Pruefe.Gleich(25.0, Stress(w, erbe));
            Pruefe.Gleich(40.0, Stress(w, fh.Oberhaupt));
            // Eine Geisel, die in die Fremde verkauft wird, zählt wie ein Todesfall
            var kind2 = w.Familien.KindAufnehmen(h, 3);
            w.Familien.Verschwinden(h, kind2, "Probe");
            Pruefe.Gleich(60.0, Stress(w, fh.Oberhaupt));
            Pruefe.Wahr(fh.Oberhaupt.Stresslage.Eintraege[^1].Text.StartsWith("Verschleppt: Tochter", StringComparison.Ordinal), fh.Oberhaupt.Stresslage.Eintraege[^1].Text);
        }),

        ("Stress: Sorgen von außen – Diebstahl, Feuer und Überfall aus der öffentlichen Meldung (ab Hoch, auch über den echten Überfall), nicht aus einer vertraulichen oder vereitelten; " +
         "Betrieb mit Verlust, Ware im Preis verfallen, Tod im Haus", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 34 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var d = w.Daten.Stress!;
            var o = w.Familien.Von(spieler)!.Oberhaupt;
            var betrieb = w.Betriebe.First(b => b.Konto == spieler && b.IstWerkstatt);
            sim.EntwicklerErlaubt = true;
            Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new Reichsgilde.Core.Entwickler.EntwicklerFeuer(betrieb.Name)).Erfolg, "Feuer");
            Pruefe.Gleich(d.Haus.Brand, Stress(w, o));
            Pruefe.Gleich($"Brand oder Anschlag ({betrieb.Name})", o.Stresslage.Eintraege[^1].Text);
            if (w.Gauner.GegenWohnhaus)
            {
                Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new Reichsgilde.Core.Entwickler.EntwicklerHausbrand()).Erfolg, "Hausbrand");
                Pruefe.Gleich(2 * d.Haus.Brand, Stress(w, o));
                Pruefe.Gleich("Brand oder Anschlag (Wohnhaus)", o.Stresslage.Eintraege[^1].Text);
            }
            w.Familien.StressSetzen(o, 0);
            w.Melden(Meldungsart.Ueberfall, "Probe: Überfall", haus: spieler); // Vorgabe der Art: hoch
            Pruefe.Gleich(d.Haus.Ueberfall, Stress(w, o));
            w.Melden(Meldungsart.Diebstahl, "Probe: leerer Einbruch", haus: spieler, wichtigkeit: Meldungswichtigkeit.Normal); // Gegenprobe: vereitelt/leer
            w.Gauner.Melden(spieler, "Probe: Eure Bande hat gestohlen", Meldungsart.Diebstahl); // Gegenprobe: vertraulich an den Auftraggeber
            Pruefe.Gleich(d.Haus.Ueberfall, Stress(w, o));
            w.Melden(Meldungsart.Diebstahl, "Probe: Einbruch mit Beute", haus: spieler, wichtigkeit: Meldungswichtigkeit.Hoch);
            Pruefe.Gleich(d.Haus.Ueberfall + d.Haus.Diebstahl, Stress(w, o));
            var ohneFamilie = w.KontoEroeffnen("Haus ohne Familie", startvermoegen: 1_000); // Gegenprobe: ein Haus ohne Familie bricht nichts
            w.Melden(Meldungsart.Feuer, "Probe: Feuer ohne Familie", haus: ohneFamilie);
            Pruefe.Gleich(d.Haus.Ueberfall + d.Haus.Diebstahl, Stress(w, o));

            // Der echte Weg (Ueberfall.Wuerfeln, Wichtigkeit von der Meldestelle): ein gelungener Überfall bringt Stress, ein abgewehrter nicht
            var karren = w.Betriebe.Where(b => b.Konto == spieler).SelectMany(b => b.Karren).FirstOrDefault()
                         ?? w.BetriebEroeffnen("Probe-Grube", Betriebsart.Mine, spieler).Karren[0];
            Meldung? letzte = null;
            w.Gemeldet += m => letzte = m;
            int gelungen = 0, abgewehrt = 0;
            for (var i = 0; i < 20_000 && (gelungen < 3 || abgewehrt < 3); i++)
            {
                letzte = null;
                w.Familien.StressSetzen(o, 0);
                Ueberfall.Wuerfeln(karren, w.Daten.Logistik.Eskorten["Stadtwache"], sim.Zeit, w);
                if (letzte is not { Art: Meldungsart.Ueberfall } m) continue;
                var erbeutet = m.Text.Contains("erbeutet", StringComparison.Ordinal);
                if (erbeutet) gelungen++; else abgewehrt++;
                Pruefe.Gleich(erbeutet ? d.Haus.Ueberfall : 0.0, Stress(w, o), m.Text);
            }
            Pruefe.Wahr(gelungen >= 3 && abgewehrt >= 3, $"Überfälle gelungen {gelungen}, abgewehrt {abgewehrt}");

            // Jahresende: Betrieb mit Verlust und Ware unter der Schwelle – jedes für sich, mit Gegenproben
            var f = w.Familien.Von(spieler)!;
            bool Steht(string anfang) => o.Stresslage.Eintraege.Any(e => e.Text.StartsWith(anfang, StringComparison.Ordinal));
            string Texte() => string.Join(" | ", o.Stresslage.Eintraege.Select(e => e.Text));
            void Jahreswechsel(double nachfrage)
            {
                o.Stresslage.JahrAbschliessen(int.MaxValue); // Einträge leeren
                w.Markt[betrieb.Produkt].NachfrageFaktor = nachfrage; // 5: gefragt, der Preis steht weit über der Schwelle; 0,05: er fällt darunter
                Pruefe.Wahr(w.Markt[betrieb.Produkt].Preisfaktor < d.Haus.PreisverfallAb == nachfrage < 1, $"Preisfaktor {w.Markt[betrieb.Produkt].Preisfaktor}");
                w.Familien.StressSetzen(o, 50);
                w.Familien.StressImJahreswechsel(f);
            }
            betrieb.JahrAbschliessen(betrieb.Laufend.Jahr + 1); // das Jahr des Feuers (Schaden) ist vorbei
            betrieb.JahrAbschliessen(betrieb.Laufend.Jahr + 1); // ein leeres Jahr ohne Verlust
            sim.Tick(); // mitten am Tag: ein neuer Betrieb läuft nicht das ganze Jahr
            var neu = w.BetriebEroeffnen("Probe-Werkstatt", betrieb.Art, spieler);
            Pruefe.Wahr(!neu.Laufend.Ganzjaehrig, "neuer Betrieb nicht ganzjährig");
            neu.Laufend.Fixkosten += 1_000;
            neu.JahrAbschliessen(neu.Laufend.Jahr + 1);
            var fremd = w.Betriebe.First(b => b.Konto != spieler && b.Konto != ohneFamilie);
            fremd.Laufend.Fixkosten += 1_000;
            fremd.JahrAbschliessen(fremd.Laufend.Jahr + 1);
            Pruefe.Wahr(fremd.Vorjahr is { Ganzjaehrig: true, Gewinn: < 0 }, "fremder Betrieb mit Verlust");
            Jahreswechsel(5);
            Pruefe.Wahr(!Steht("Verlust in") && !Steht("Preise verfallen"), "Gegenprobe: kein eigener ganzjähriger Verlust, Preis nicht verfallen – " + Texte());
            betrieb.Laufend.Fixkosten += 1_000;
            betrieb.JahrAbschliessen(betrieb.Laufend.Jahr + 1);
            Jahreswechsel(5);
            Pruefe.Wahr(Steht($"Verlust in {betrieb.Name}") && !Steht("Preise verfallen"), "nur Verlust – " + Texte());
            betrieb.JahrAbschliessen(betrieb.Laufend.Jahr + 1);
            Jahreswechsel(0.05);
            Pruefe.Wahr(!Steht("Verlust in") && Steht($"Preise verfallen ({betrieb.Produkt})"), "nur Preisverfall – " + Texte());

            // Tod im Haus: die zweite Frau des verstorbenen Oberhaupts ist mit dem Erben (Sohn aus erster Ehe) nicht verwandt
            var w2 = NeueSimulation().Wirtschaft!;
            var (k2, f2) = Haus(w2, "Haus Witwe");
            var witwe = Heiraten(w2, k2);
            var erbe = w2.Familien.KindAufnehmen(k2, 25, Geschlecht.Mann);
            w2.Familien.AnKrankheitSterben(f2.Oberhaupt, "an der Ruhr");
            Pruefe.Wahr(f2.Oberhaupt == erbe, "der Sohn erbt");
            var vorher = Stress(w2, erbe);
            w2.Familien.AnKrankheitSterben(witwe, "an der Ruhr");
            Pruefe.Gleich(vorher + d.Trauer.Haus, Stress(w2, erbe));
            Pruefe.Gleich($"Tod im Haus: {witwe.Vorname}", erbe.Stresslage.Eintraege[^1].Text);

            // Gegenprobe: Eine Meldung an ein erloschenes Haus bricht nichts und gibt niemandem Stress
            var (k3, f3) = Haus(w2, "Haus Erloschen");
            var letzter = f3.Oberhaupt;
            w2.Familien.AnKrankheitSterben(letzter, "an der Ruhr");
            Pruefe.Wahr(!k3.IstAktiv, "Haus erloschen");
            var stressVorher = Stress(w2, letzter);
            w2.Melden(Meldungsart.Diebstahl, "Probe: Diebstahl im erloschenen Haus", haus: k3, wichtigkeit: Meldungswichtigkeit.Hoch);
            Pruefe.Gleich(stressVorher, Stress(w2, letzter));
        }),

        ("Stress: Rat – wer zur Wahl steht und verliert (Bewerber wie abgewählter Amtsinhaber), bekommt Stress, der Gewinner und wer sich nicht bewarb nicht; ein abgelehnter Antrag auch, ein angenommener nicht", () =>
        {
            var sim = NeueSimulation(35);
            var w = sim.Wirtschaft!;
            var amt = w.Rat.Aemter.Where(a => a.Stufe == 1).OrderBy(a => a.Name, StringComparer.Ordinal).First();
            var (a, fa) = Haus(w, "Haus Anwärter");
            var (b, fb) = Haus(w, "Haus Rivale");
            var (_, fc) = Haus(w, "Haus Zuschauer");
            a.Stand = b.Stand = w.Daten.StandStufe(amt.Daten.MindestStand);
            Ok(w, a, new AmtBewerben(amt.Name));
            Ok(w, b, new AmtBewerben(amt.Name));
            JahrBeenden(sim);
            bool Verloren(Familie f) => f.Oberhaupt.Stresslage.Eintraege.Any(e => e.Text == $"Wahl zum Amt {amt.Name} verloren");
            Pruefe.Wahr(Verloren(fa) || Verloren(fb), "mindestens einer hat verloren");
            foreach (var (k, f) in new[] { (a, fa), (b, fb) })
                Pruefe.Wahr(Verloren(f) == (w.Rat.AmtVon(k)?.Name != amt.Name), $"{k.Inhaber}: verloren genau dann, wenn er das Amt nicht hat");
            Pruefe.Wahr(!Verloren(fc), "Gegenprobe: wer sich nicht bewarb, hat nichts verloren");

            // Ein Amtsinhaber verteidigt sein Amt gegen einen Bewerber: wer verliert, bekommt Stress – der abgewählte Amtsinhaber mit „Amt … verloren“.
            // Ohne Hilfe behält der Amtsinhaber das Amt; hat der Herausforderer die Gunst aller Ratsherren, wird er abgewählt.
            foreach (var beliebt in new[] { false, true })
            {
                var simA = NeueSimulation(37);
                var wA = simA.Wirtschaft!;
                var amtA = wA.Rat.Aemter.Where(x => x.Stufe == 1).OrderBy(x => x.Name, StringComparer.Ordinal).First();
                var (inhaber, fi) = Haus(wA, "Haus Inhaber");
                var (herausforderer, fh) = Haus(wA, "Haus Herausforderer");
                inhaber.Stand = herausforderer.Stand = wA.Daten.StandStufe(amtA.Daten.MindestStand);
                StadtkasseTests.Sitz(wA, amtA.Name, inhaber);
                Ok(wA, herausforderer, new AmtBewerben(amtA.Name));
                if (beliebt) foreach (var r in wA.Rat.Ratsherren) wA.Rat.GunstAendern(r, herausforderer, "geschenk", "Geschenke", 100);
                JahrBeenden(simA);
                var inhaberBleibt = wA.Rat.AmtVon(inhaber)?.Name == amtA.Name;
                Pruefe.Gleich(!beliebt, inhaberBleibt, $"beliebt {beliebt}: Amtsinhaber bleibt");
                Pruefe.Gleich(beliebt, wA.Rat.AmtVon(herausforderer)?.Name == amtA.Name, $"beliebt {beliebt}: Herausforderer gewinnt");
                bool Hat(Familie fam, string text) => fam.Oberhaupt.Stresslage.Eintraege.Any(e => e.Text == text);
                Pruefe.Gleich(beliebt, Hat(fi, $"Amt {amtA.Name} verloren"), $"beliebt {beliebt}: Amtsinhaber");
                Pruefe.Gleich(!beliebt, Hat(fh, $"Wahl zum Amt {amtA.Name} verloren"), $"beliebt {beliebt}: Herausforderer");
                Pruefe.Wahr(!Hat(fi, $"Wahl zum Amt {amtA.Name} verloren") && !Hat(fh, $"Amt {amtA.Name} verloren"), "jeder nur mit seinem Text");
            }

            // Antrag abgelehnt: die Steuer senken bei knapper Kasse will kein Ratsherr; erhöhen schon (Gegenprobe)
            foreach (var (prozent, durch) in new[] { (4, false), (6, true) })
            {
                var sim2 = NeueSimulation(36);
                var w2 = sim2.Wirtschaft!;
                var (k, fk) = Haus(w2, "Haus Reformer");
                StadtkasseTests.Sitz(w2, "Zöllner", k);
                StadtkasseTests.KasseAuf(w2, durch ? 1000 : 3000);
                Pruefe.Gleich((bool?)durch, w2.Ratsantraege.Prognose(new Ratsantrag(Antragsart.Steuersatz, k, Prozent: prozent)).Item4);
                Ok(w2, k, new SteuersatzBeantragen(prozent));
                JahrBeenden(sim2);
                Pruefe.Gleich(!durch, fk.Oberhaupt.Stresslage.Eintraege.Any(e => e.Text == "Antrag im Rat abgelehnt"), $"Steuersatz {prozent} %: " + string.Join(" | ", w2.Rat.LetzteSitzung));
            }
        }),

        ("Stress: Abklingen – jedes Jahr −15, in glücklicher Ehe −20, schwermütig −10; nie unter 0; die Ventile sind im neuen Jahr frei", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (a, fa) = Haus(w, "Haus Gleich");
            var (b, fb) = Haus(w, "Haus Glueck");
            var e = Heiraten(w, b);
            var (c, fc) = Haus(w, "Haus Schwer", "Schwermuetig");
            var (d, fd) = Haus(w, "Haus Leicht");
            foreach (var f in new[] { fa, fb, fc }) w.Familien.StressSetzen(f.Oberhaupt, 50);
            w.Familien.StressSetzen(fd.Oberhaupt, 10);
            w.Meinungen.EntwicklerGrundSetzen(e, b, 60);
            Pruefe.Wahr(w.Meinungen.Meinung(e, fb.Oberhaupt) >= 50, "glückliche Ehe");
            fa.Oberhaupt.Stresslage.VentilMerken("Zechen");
            foreach (var f in new[] { fa, fb, fc, fd }) w.Familien.StressImJahreswechsel(f);
            Pruefe.Gleich(35.0, Stress(w, fa.Oberhaupt));
            Pruefe.Gleich(30.0, Stress(w, fb.Oberhaupt));
            Pruefe.Gleich(40.0, Stress(w, fc.Oberhaupt));
            Pruefe.Gleich(0.0, Stress(w, fd.Oberhaupt));
            Pruefe.Gleich(0, fa.Oberhaupt.Stresslage.Ventile.Count);
        }),

        ("Stress: Stufen und Folgen – ab 40 Erfahrung −25 % und Gefahr +5 Prozentpunkte; ab 70 Erfahrung −50 %, Talente −½, Krankheiten +25 %, " +
         "Ehepartner −5 („unleidlich“), KI vorsichtiger; darunter nichts; ohne Abschnitt nichts", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Last");
            var o = f.Oberhaupt;
            var e = Heiraten(w, konto);
            var r = Ratsherr(w, 0);
            var pfad = w.Familien.AktiverPfad(o);
            w.Familien.StressSetzen(o, 30);
            var erfahrung = w.Familien.ErfahrungFaktor(o, pfad);
            var gefahr = w.Meinungen.Aktivitaet(konto, r, "Zechen")!.Gefahr;
            var talent = o.Talent(Talent.Handwerk);
            var krank = w.Familien.KrankheitFaktor(o);
            var meinung = w.Meinungen.Meinung(e, o);
            Pruefe.Gleich(0, w.Familien.StressStufe(o));
            Pruefe.Gleich("ruhig", w.Familien.StressStufenname(0));
            w.Familien.StressSetzen(o, 45);
            Pruefe.Gleich(1, w.Familien.StressStufe(o));
            Pruefe.Ungefaehr(erfahrung * 0.75, w.Familien.ErfahrungFaktor(o, pfad), 1e-12);
            Pruefe.Ungefaehr(gefahr + 0.05, w.Meinungen.Aktivitaet(konto, r, "Zechen")!.Gefahr, 1e-12);
            Pruefe.Ungefaehr(talent, o.Talent(Talent.Handwerk), 1e-12);
            w.Familien.StressSetzen(o, 75);
            Pruefe.Gleich(2, w.Familien.StressStufe(o));
            Pruefe.Gleich("zermürbt", w.Familien.StressStufenname(2));
            Pruefe.Ungefaehr(erfahrung * 0.5, w.Familien.ErfahrungFaktor(o, pfad), 1e-12);
            Pruefe.Ungefaehr(Math.Max(0, talent - 0.5), o.Talent(Talent.Handwerk), 1e-12);
            Pruefe.Ungefaehr(krank * 1.25, w.Familien.KrankheitFaktor(o), 1e-12);
            Pruefe.Ungefaehr(meinung - 5, w.Meinungen.Meinung(e, o), 1e-9);
            Pruefe.Wahr(w.Meinungen.Gruende(e, o).Any(g => g.Text == "Unleidlich (Stress)" && g.Wert == -5), "Grund im Tooltip");
            Pruefe.Ungefaehr(1.2, w.Familien.KiStressVorsicht(konto), 1e-12);
            Pruefe.Wahr(w.Familien.StressFolgen(2).Contains("alle Talente −½ Stern"), w.Familien.StressFolgen(2));
            // Gegenprobe: zurück auf 30 – alles wie vorher
            w.Familien.StressSetzen(o, 30);
            Pruefe.Ungefaehr(talent, o.Talent(Talent.Handwerk), 1e-12);
            Pruefe.Ungefaehr(meinung, w.Meinungen.Meinung(e, o), 1e-9);
            // Gegenprobe: ohne Abschnitt wirkt auch ein hoher Wert nicht
            var w2 = NeueSimulation(daten: Ohne.Value).Wirtschaft!;
            var (_, f2) = Haus(w2, "Haus Ohne");
            var t2 = f2.Oberhaupt.Talent(Talent.Handwerk);
            w2.Familien.StressSetzen(f2.Oberhaupt, 90);
            Pruefe.Gleich(0, w2.Familien.StressStufe(f2.Oberhaupt));
            Pruefe.Ungefaehr(t2, f2.Oberhaupt.Talent(Talent.Handwerk), 1e-12);
        }),

        ("Stress: Zusammenbruch bei 100 – ein Zug (Trinker, Schwermütig, Reizbar) oder eine Krankheit (Vergesslichkeit, Lachanfälle, Angst im Dunkeln, " +
         "Haarausfall), danach 60; mit derselben Partie dieselbe Folge", () =>
        {
            string Reihe()
            {
                var w = NeueSimulation(77).Wirtschaft!;
                var meldungen = 0;
                w.Meldung += m => { if (m.StartsWith("Zusammenbruch:", StringComparison.Ordinal)) meldungen++; };
                var folgen = new List<string>();
                for (var i = 0; i < 12; i++)
                {
                    var (_, f) = Haus(w, $"Haus {i}");
                    var o = f.Oberhaupt;
                    w.Familien.StressSetzen(o, 95);
                    w.Familien.StressAendern(o, 10, "Probe");
                    Pruefe.Gleich(60.0, Stress(w, o));
                    Pruefe.Gleich("Zusammenbruch", o.Stresslage.Eintraege[^1].Text);
                    var zug = o.Zuege.Select(z => z.Kennung).FirstOrDefault(k => k is "Trinker" or "Schwermuetig" or "Reizbar");
                    var krank = o.Krankheit?.Name;
                    Pruefe.Wahr((zug is null) != (krank is null), $"genau eine Folge: Zug {zug}, Krankheit {krank}");
                    Pruefe.Wahr(krank is null or "Vergesslichkeit" or "Lachanfälle" or "Angst im Dunkeln" or "Haarausfall", $"Krankheit {krank}");
                    folgen.Add(zug ?? krank!);
                }
                Pruefe.Gleich(12, meldungen);
                Pruefe.Wahr(folgen.Any(x => x is "Trinker" or "Schwermuetig" or "Reizbar") && folgen.Any(x => x is not ("Trinker" or "Schwermuetig" or "Reizbar")),
                    $"beides kommt vor: {string.Join(", ", folgen)}");
                return string.Join(",", folgen);
            }
            Pruefe.Gleich(Reihe(), Reihe());
        }),

        ("Stress: Zusammenbruch mit allen drei Zügen und krank – nichts mehr zu geben, nur der Fall auf 60; mit allen drei Zügen und gesund eine Krankheit", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Ende", "Trinker", "Schwermuetig", "Reizbar");
            var o = f.Oberhaupt;
            Pruefe.Wahr(w.Gesundheit.Erkranken(konto, o, "Husten"), "krank");
            w.Familien.StressSetzen(o, 99);
            w.Familien.StressAendern(o, 5, "Probe");
            Pruefe.Gleich(60.0, Stress(w, o));
            Pruefe.Gleich(3, o.Zuege.Count);
            Pruefe.Gleich("Husten", o.Krankheit!.Name);
            var (k2, f2) = Haus(w, "Haus Gesund", "Trinker", "Schwermuetig", "Reizbar");
            w.Familien.StressSetzen(f2.Oberhaupt, 99);
            w.Familien.StressAendern(f2.Oberhaupt, 5, "Probe");
            Pruefe.Wahr(f2.Oberhaupt.Krankheit is { Name: "Vergesslichkeit" or "Lachanfälle" or "Angst im Dunkeln" or "Haarausfall" }, "Krankheit statt Zug");
            Pruefe.Gleich(60.0, Stress(w, f2.Oberhaupt)); // die Krankheit des Zusammenbruchs bringt keinen neuen Stress
        }),

        ("Stress: Spielstand – Wert, Einträge, Ventile und die Würfe des Zusammenbruchs bleiben; die Partie läuft gleich weiter", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 31 },
                Daten.Value, new NeuePartie());
            for (var i = 0; i < 2; i++) JahrBeenden(sim);
            var w = sim.Wirtschaft!;
            var o = w.Familien.Von(spieler)!.Oberhaupt;
            w.Familien.StressSetzen(o, 98);
            w.Familien.StressAendern(o, 5, "Probe"); // Zusammenbruch: würfelt
            o.Stresslage.VentilMerken("Messe");
            w.Familien.StressAendern(o, 15, "Probe"); // 75: zermürbt – der Abzug an den Talenten muss nach dem Laden wieder da sein
            Pruefe.Gleich(0.5, o.Stresslage.TalentAbzug);
            var zeitpunkt = new DateTime(2026, 11, 9, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            Pruefe.Wahr(StressImSpielstand(json) && json.Contains("\"StressWuerfe\""), "Stress im Spielstand");
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
            var neu = geladen.Wirtschaft!.Familien.Von(geladenerSpieler!)!.Oberhaupt;
            Pruefe.Gleich(o.Stresslage.Wert, neu.Stresslage.Wert);
            Pruefe.Gleich(string.Join("|", o.Stresslage.Eintraege), string.Join("|", neu.Stresslage.Eintraege));
            Pruefe.Gleich("Messe", string.Join(",", neu.Stresslage.Ventile));
            Pruefe.Gleich(0.5, neu.Stresslage.TalentAbzug);
            foreach (var t in TalentTexte.Alle) Pruefe.Gleich(o.Talent(t), neu.Talent(t), $"{t} nach dem Laden");
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen));
            // ein zweiter Zusammenbruch würfelt in beiden gleich
            foreach (var (welt, p) in new[] { (w, o), (geladen.Wirtschaft, neu) })
            {
                welt.Familien.StressSetzen(p, 99);
                welt.Familien.StressAendern(p, 5, "Probe");
            }
            Pruefe.Gleich(string.Join(",", o.Zuege) + o.Krankheit?.Name, string.Join(",", neu.Zuege) + neu.Krankheit?.Name);
            for (var i = 0; i < 3; i++) { JahrBeenden(sim); JahrBeenden(geladen); }
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, zeitpunkt), Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
        }),

        ("Stress: Spielstand ohne Stress (Daten ohne Abschnitt) lädt, läuft ein Jahr, speichert und lädt gleich – kein Feld, niemand hat Stress", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 32 },
                Ohne.Value, new NeuePartie());
            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Wahr(!geladen.Wirtschaft!.Familien.HatStresswesen, "kein Stress");
            JahrBeenden(geladen);
            var zeitpunkt = new DateTime(2026, 11, 9, 12, 0, 0);
            var nachJahr = Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt);
            Pruefe.Wahr(!StressImSpielstand(nachJahr), "kein Feld ohne Stress");
            var (wieder, wiederSpieler) = Speicherung.Laden(nachJahr);
            Pruefe.Gleich(nachJahr, Speicherung.Speichern(wieder, wiederSpieler, zeitpunkt));
        }),

        ("Stress: alter Spielstand (Greybox 1406, Datenversion 55) lädt mit seinen Daten, läuft ein Jahr, speichert und lädt gleich – ohne Stress", () =>
        {
            var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
            using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
            var (sim, spieler) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            Pruefe.Wahr(!sim.Wirtschaft!.Familien.HatStresswesen, "Daten 55 ohne Stress");
            JahrBeenden(sim);
            var zeitpunkt = new DateTime(2026, 11, 9, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            Pruefe.Wahr(!StressImSpielstand(json), "kein Feld");
            var (wieder, wiederSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(wieder, wiederSpieler, zeitpunkt));
        }),

        ("Stress: Entwicklerfenster – Stress setzen (Eintrag „Entwicklerhilfe“), 100 löst den Zusammenbruch aus; gesperrt, beim Kind und ohne Abschnitt abgelehnt", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 33 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var o = w.Familien.Von(spieler)!.Oberhaupt;
            Pruefe.Wahr(!sim.Bote.SofortAusfuehren(spieler, new Reichsgilde.Core.Entwickler.EntwicklerStress(o.Nummer, 50)).Erfolg, "gesperrt");
            sim.EntwicklerErlaubt = true;
            Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new Reichsgilde.Core.Entwickler.EntwicklerStress(o.Nummer, 75)).Erfolg, "gesetzt");
            Pruefe.Gleich(75.0, Stress(w, o));
            Pruefe.Gleich("Entwicklerhilfe", o.Stresslage.Eintraege[^1].Text);
            Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new Reichsgilde.Core.Entwickler.EntwicklerStress(o.Nummer, 100)).Erfolg, "Zusammenbruch");
            Pruefe.Gleich(60.0, Stress(w, o));
            var kind = w.Familien.KindAufnehmen(spieler, 3);
            Pruefe.Wahr(!sim.Bote.SofortAusfuehren(spieler, new Reichsgilde.Core.Entwickler.EntwicklerStress(kind.Nummer, 50)).Erfolg, "Kind ohne Stress");
            var (sim2, spieler2) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 33 },
                Ohne.Value, new NeuePartie());
            sim2.EntwicklerErlaubt = true;
            var e = sim2.Bote.SofortAusfuehren(spieler2, new Reichsgilde.Core.Entwickler.EntwicklerStress(sim2.Wirtschaft!.Familien.Von(spieler2)!.Oberhaupt.Nummer, 50));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("niemand Stress"), e.Meldung);
        }),

        ("Stress: In einer Partie mit KI-Häusern (Greybox, 10 Jahre) bekommen Oberhäupter Stress, keiner kommt über 100", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
                Daten.Value, new NeuePartie(Startvermoegen: 15_000, Beruf: "Schmied"), karte: Reichsgilde.Core.Bauen.Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            var w = sim.Wirtschaft!;
            var quellen = 0;
            var hoechst = 0.0;
            for (var i = 0; i < 10; i++)
            {
                JahrBeenden(sim);
                foreach (var k in w.Konten)
                    if (w.Familien.Von(k) is { } f)
                        foreach (var p in f.Lebende)
                        {
                            hoechst = Math.Max(hoechst, p.Stresslage.Wert);
                            quellen += p.Stresslage.Eintraege.Count(x => x.Wert > 0 && x.Jahr == w.Familien.Jahr - 1);
                        }
            }
            Pruefe.Wahr(quellen > 0, "Quellen kommen vor");
            Pruefe.Wahr(hoechst <= 100, $"höchstens 100 ({hoechst})");
        }),
    };
}
