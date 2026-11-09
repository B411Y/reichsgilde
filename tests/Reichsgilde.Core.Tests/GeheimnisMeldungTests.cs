using System.IO.Compression;
using System.Text.Json.Nodes;
using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Siegel der Geheimnisse in Meldung und Erpresserbrief: Jede Meldung, die von einem Geheimnis handelt (entstanden, entdeckt, erpresst,
/// angezeigt, bloßgestellt, verurteilt), trägt seine Art (<see cref="Meldung.GeheimnisArt"/>), der Erpresserbrief auch
/// (<see cref="Reichsgilde.Core.Gauner.Forderung.GeheimnisArt"/>) – jeweils mit Gegenprobe (Brief und Anklage mit Beweisen, der öffentliche Satz
/// zum Brief, ein Spitzel ohne Fund, Gerede über verschiedene Arten). Dazu der Spielstand mit und ohne Feld und ein alter Spielstand.
/// </summary>
static class GeheimnisMeldungTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 11, 9, 12, 0, 0);

    /// <summary>Kein Zufall bei den Geheimnissen (wie in den Geheimnistests): keine Ratsherren-Geheimnisse, kein Gerede, keine Beichte, kein Ehepartner, keine Ehebrüche und Ketzer.</summary>
    static readonly (string Alt, string Neu)[] Ruhig =
    {
        ("\"start\": 0.25, \"jeJahr\": 0.02", "\"start\": 0, \"jeJahr\": 0"),
        ("\"gerede\": 0.02", "\"gerede\": 0"),
        ("\"beichte\": 0.25", "\"beichte\": 0"),
        ("\"ehepartner\": 0.1,", "\"ehepartner\": 0,"),
        ("\"Hintergeht heimlich den Ehepartner.\", \"chance\": 0.015", "\"Hintergeht heimlich den Ehepartner.\", \"chance\": 0"),
        ("\"chance\": 0.01, \"nurMit\"", "\"chance\": 0, \"nurMit\""),
    };

    /// <summary>Die Daten ohne Zufall; <paramref name="mehr"/> ersetzt einen Eintrag aus <see cref="Ruhig"/> mit gleichem Alt-Text oder kommt dazu. Jeder Text genau einmal.</summary>
    static Wirtschaftsdaten Daten(params (string Alt, string Neu)[] mehr)
    {
        var json = Json.Value;
        foreach (var (alt, neu) in Ruhig.Where(r => mehr.All(m => m.Alt != r.Alt)).Concat(mehr))
        {
            var erst = json.IndexOf(alt, StringComparison.Ordinal);
            Pruefe.Wahr(erst >= 0 && json.IndexOf(alt, erst + 1, StringComparison.Ordinal) < 0, $"„{alt}“ genau einmal in den Daten");
            json = json.Replace(alt, neu);
        }
        return Wirtschaftsdaten.AusJson(json);
    }

    static Simulation NeueSimulation(Wirtschaftsdaten daten, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, Konfession glaube = Konfession.Katholisch, params string[] zuege)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: 20_000);
        var f = w.Familien.Gruenden(konto, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: glaube);
        konto.Aktionspunkte.Setzen(12);
        f.Oberhaupt.ZuegeLeeren();
        foreach (var k in zuege) f.Oberhaupt.ZugHinzufuegen(w.Daten.Charakterzuege!.Zug(k)!);
        return (konto, f);
    }

    static Person Heiraten(Stadtwirtschaft w, Konto konto)
    {
        var k = w.Familien.Kandidaten.First(k => k.Person.Geschlecht == Geschlecht.Frau);
        k.Person.ZuegeLeeren();
        k.Person.ZugHinzufuegen(w.Daten.Charakterzuege!.Zug("Geduldig")!); // mit Persönlichkeit, sonst bekäme sie am Jahresende eine
        w.Familien.Vermaehlen(konto, k, 0);
        return w.Familien.Von(konto)!.Ehepartner!;
    }

    /// <summary>Gibt <paramref name="inhaber"/> ein Geheimnis dieser Art (Entwicklerweg); mit <paramref name="wisser"/> weiß dieses Haus davon.</summary>
    static Geheimnis GeheimnisGeben(Stadtwirtschaft w, ICharakter inhaber, Geheimnisart art, Konto? wisser = null)
    {
        var e = w.Geheimnisse.Entwickler(wisser ?? w.Konten[0], inhaber.Kennung, art, wissen: wisser is not null);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return w.Geheimnisse.Von(inhaber).First(g => g.Art == art);
    }

    static void Ok(Stadtwirtschaft w, Konto konto, Befehl befehl)
    {
        var e = w.Ausfuehren(konto, befehl);
        Pruefe.Wahr(e.Erfolg, $"{befehl.GetType().Name}: {e.Meldung}");
    }

    /// <summary>Schreibt alle Meldungen des Kerns mit (ab jetzt).</summary>
    static List<Meldung> Mitschreiben(Stadtwirtschaft w)
    {
        var liste = new List<Meldung>();
        w.Gemeldet += liste.Add;
        return liste;
    }

    /// <summary>Die eine Meldung, deren Text das enthält.</summary>
    static Meldung Eine(List<Meldung> liste, string text)
    {
        var treffer = liste.Where(m => m.Text.Contains(text, StringComparison.Ordinal)).ToList();
        Pruefe.Wahr(treffer.Count == 1, $"genau eine Meldung mit „{text}“, waren {treffer.Count}: {string.Join(" | ", liste.Select(m => m.Text))}");
        return treffer[0];
    }

    static void Art(Geheimnisart? erwartet, Meldung m) => Pruefe.Gleich(erwartet, m.GeheimnisArt, m.Text);

    /// <summary>Gibt dem Haus <paramref name="anzahl"/> Beweise gegen <paramref name="taeter"/> (Diebstahl).</summary>
    static void Beweise(Stadtwirtschaft w, Konto inhaber, Konto taeter, int anzahl)
    {
        for (var i = 0; i < anzahl; i++)
            w.Justiz.BeweisGeben(inhaber, w.Justiz.TatMelden(taeter, Vergehen.Diebstahl, inhaber.Inhaber, 200)!);
    }

    /// <summary>Wie ein Spielstand von vor dem Siegel: ohne „GeheimnisArt“ in Meldungen und Erpresserbriefen (die Anzeige im Verfahren hatte das Feld schon).</summary>
    static JsonNode OhneNeuesFeld(string json)
    {
        var n = JsonNode.Parse(json)!;
        void Gehen(JsonNode? k)
        {
            switch (k)
            {
                case JsonObject o:
                    foreach (var (name, wert) in o.ToList())
                    {
                        if (name is "Meldungen" or "Forderungen" && wert is JsonArray liste)
                            foreach (var e in liste.OfType<JsonObject>()) e.Remove("GeheimnisArt");
                        else Gehen(wert);
                    }
                    break;
                case JsonArray a:
                    foreach (var e in a) Gehen(e);
                    break;
            }
        }
        Gehen(n);
        return n;
    }

    static int Zaehlen(string text, string teil)
    {
        var zahl = 0;
        for (var i = text.IndexOf(teil, StringComparison.Ordinal); i >= 0; i = text.IndexOf(teil, i + 1, StringComparison.Ordinal)) zahl++;
        return zahl;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Geheimnis-Siegel: Erpresserbrief um Gulden – der Brief (Forderung) und die vertrauliche Meldung an das Opfer tragen die Art, der öffentliche Satz " +
         "nicht; Gegenprobe: ein Brief mit Beweisen trägt keine", () =>
        {
            var w = NeueSimulation(Daten()).Wirtschaft!;
            var (a, _) = Haus(w, "Haus Hand");
            var (b, fb) = Haus(w, "Haus Fuss");
            var (c, _) = Haus(w, "Haus Zeuge");
            var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ketzerei, wisser: a);
            var liste = Mitschreiben(w);
            Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, 500));
            var vertraulich = Eine(liste, "nennt das Geheimnis");
            Art(Geheimnisart.Ketzerei, vertraulich);
            Pruefe.Wahr(vertraulich.Vertraulich && w.HausVon(vertraulich) == b, "nur für das Opfer");
            Art(null, Eine(liste, $"{b.Inhaber} erhält einen Erpresserbrief von {a.Inhaber}")); // die Stadt erfährt nur, dass ein Brief kam
            var brief = w.Gauner.ForderungAn(b, a)!;
            Pruefe.Gleich((Geheimnisart?)Geheimnisart.Ketzerei, brief.GeheimnisArt);
            Pruefe.Gleich((Geheimnisart?)Geheimnisart.Ketzerei, w.Geheimnisse.ArtDesBriefes(brief));
            Pruefe.Gleich($"{fb.Oberhaupt.Name}: Ketzerei", w.Geheimnisse.WorumDerBrief(brief));
            // Gegenprobe: ein Erpresserbrief mit Beweisen (Haus Zeuge gegen Haus Fuss)
            Beweise(w, c, b, w.Gauner.Daten.ErpressungMindestBeweise);
            liste.Clear();
            Ok(w, c, new Erpressen(b.Inhaber, 300));
            var mitBeweisen = w.Gauner.ForderungAn(b, c)!;
            Pruefe.Gleich((Geheimnisart?)null, mitBeweisen.GeheimnisArt);
            Pruefe.Gleich((Geheimnisart?)null, w.Geheimnisse.ArtDesBriefes(mitBeweisen));
            Pruefe.Gleich((string?)null, w.Geheimnisse.WorumDerBrief(mitBeweisen));
            Pruefe.Wahr(liste.Count > 0 && liste.All(m => m.GeheimnisArt is null), "Brief mit Beweisen: keine Art – " + string.Join(" | ", liste.Select(m => m.Text)));
        }),

        ("Geheimnis-Siegel: Ratsstimme erpresst – die Meldung an das erpresste Haus trägt die Art", () =>
        {
            var w = NeueSimulation(Daten()).Wirtschaft!;
            var (a, _) = Haus(w, "Haus Hebel");
            var (b, fb) = Haus(w, "Haus Sitz");
            var sitz = w.Rat.AmtNamens("Stadthauptmann")!;
            sitz.Ratsherr = null;
            sitz.Konto = b;
            var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Schulden, wisser: a);
            var liste = Mitschreiben(w);
            Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Ratsstimme));
            var m = Eine(liste, "verlangt eure Stimme");
            Art(Geheimnisart.Schulden, m);
            Pruefe.Wahr(m.Vertraulich && w.HausVon(m) == b, "nur für das erpresste Haus");
        }),

        ("Geheimnis-Siegel: Anzeige gegen ein Haus – Anklage und Urteil am Gerichtstag tragen die Art (Mord); Gegenprobe: Anklage und Urteil mit Beweisen ohne Art", () =>
        {
            var sim = NeueSimulation(Daten());
            var w = sim.Wirtschaft!;
            var (a, _) = Haus(w, "Haus Klaeger");
            var (b, fb) = Haus(w, "Haus Taeter");
            var (_, fc) = Haus(w, "Haus Opfer");
            var (d, _) = Haus(w, "Haus Zeuge");
            var (e, _) = Haus(w, "Haus Dieb");
            w.Geheimnisse.Mord(fb.Oberhaupt.Nummer, fc.Oberhaupt);
            var mord = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Mord, wisser: a);
            Beweise(w, d, e, 3);
            var liste = Mitschreiben(w);
            Ok(w, a, new GeheimnisNutzen(mord.Nummer, Geheimnisnutzung.Anzeige));
            var anzeige = Eine(liste, $"{a.Inhaber} zeigt {b.Inhaber} an");
            Pruefe.Gleich(Meldungsart.Anklage, anzeige.Art);
            Art(Geheimnisart.Mord, anzeige);
            Ok(w, d, new Anklagen(e.Inhaber));
            Art(null, Eine(liste, $"{d.Inhaber} klagt {e.Inhaber} wegen"));
            JahrBeenden(sim);
            Art(Geheimnisart.Mord, Eine(liste, $"{b.Inhaber} ist Mord SCHULDIG"));
            var urteil = liste.Single(m => m.Text.StartsWith("Gerichtstag", StringComparison.Ordinal) && m.Text.Contains($": {e.Inhaber} ", StringComparison.Ordinal));
            Art(null, urteil);
        }),

        ("Geheimnis-Siegel: Anzeige gegen einen Ratsherrn – Anklage und Urteil (schuldig wie freigesprochen) tragen die Art", () =>
        {
            foreach (var schuldig in new[] { true, false })
            {
                var sim = NeueSimulation(Daten(("\"beweiskraft\": 20, \"busse\": 300, \"ansehen\": 5, \"meinungEhepartner\"",
                    $"\"beweiskraft\": {(schuldig ? 40 : 1)}, \"busse\": 300, \"ansehen\": 5, \"meinungEhepartner\"")));
                var w = sim.Wirtschaft!;
                var (a, _) = Haus(w, "Haus Klaeger");
                var r = w.Rat.AmtNamens("Kämmerer")!.Ratsherr!;
                var g = GeheimnisGeben(w, r, Geheimnisart.Ehebruch, wisser: a);
                var liste = Mitschreiben(w);
                Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Anzeige));
                Art(Geheimnisart.Ehebruch, Eine(liste, $"zeigt den Ratsherrn {r.Name} an"));
                JahrBeenden(sim);
                Art(Geheimnisart.Ehebruch, Eine(liste, schuldig ? $"Der Ratsherr {r.Name} ist SCHULDIG" : $"Der Ratsherr {r.Name} wird freigesprochen"));
            }
        }),

        ("Geheimnis-Siegel: Bloßstellen – die öffentliche Meldung trägt die Art (Haus und Ratsherr)", () =>
        {
            var w = NeueSimulation(Daten()).Wirtschaft!;
            var (a, _) = Haus(w, "Haus Zunge");
            var (b, fb) = Haus(w, "Haus Schande");
            var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch, wisser: a);
            var r = w.Rat.Ratsherren[0];
            var gr = GeheimnisGeben(w, r, Geheimnisart.Schulden, wisser: a);
            var liste = Mitschreiben(w);
            Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Blossstellen));
            var m = Eine(liste, $"stellt {fb.Oberhaupt.Name} bloß");
            Art(Geheimnisart.Ehebruch, m);
            Pruefe.Wahr(!m.Vertraulich && w.HausVon(m) == b, "öffentlich, betroffen ist das Haus");
            Ok(w, a, new GeheimnisNutzen(gr.Nummer, Geheimnisnutzung.Blossstellen));
            Art(Geheimnisart.Schulden, Eine(liste, $"stellt {r.Name} bloß"));
        }),

        ("Geheimnis-Siegel: Entdecken – Gerede (eine Art: die Art; verschiedene Arten: keine), Beichte, Ehepartner tragen die Art", () =>
        {
            // Gerede mit Chance 1: Haus Ohr erfährt beide Geheimnisse von Haus Mund (verschieden) und das eine von Haus Kehle
            var sim = NeueSimulation(Daten(("\"gerede\": 0.02", "\"gerede\": 1")));
            var w = sim.Wirtschaft!;
            var (ohr, _) = Haus(w, "Haus Ohr");
            var (_, fm) = Haus(w, "Haus Mund");
            GeheimnisGeben(w, fm.Oberhaupt, Geheimnisart.Ketzerei);
            GeheimnisGeben(w, fm.Oberhaupt, Geheimnisart.Ehebruch); // nicht Schulden: die enden am Jahresende vor dem Gerede, wenn das Haus keine hat
            var liste = Mitschreiben(w);
            JahrBeenden(sim);
            var gerede = liste.Where(m => m.Text.StartsWith("Gerede in der Stadt", StringComparison.Ordinal)).ToList();
            Pruefe.Wahr(gerede.Count > 0, "Gerede gemeldet");
            foreach (var m in gerede) Art(null, m); // zwei Arten in einem Satz (Gegenprobe)
            Pruefe.Wahr(gerede.Any(m => w.HausVon(m) == ohr), "Haus Ohr hört Gerede");

            var sim2 = NeueSimulation(Daten(("\"gerede\": 0.02", "\"gerede\": 1")));
            var w2 = sim2.Wirtschaft!;
            var (ohr2, _) = Haus(w2, "Haus Ohr");
            var (_, fk) = Haus(w2, "Haus Kehle");
            GeheimnisGeben(w2, fk.Oberhaupt, Geheimnisart.Ketzerei);
            var liste2 = Mitschreiben(w2);
            JahrBeenden(sim2);
            var ein = liste2.Single(m => m.Text.StartsWith("Gerede in der Stadt", StringComparison.Ordinal) && w2.HausVon(m) == ohr2);
            Art(Geheimnisart.Ketzerei, ein);

            // Beichte (Chance 1) und Ehepartner (Chance 1)
            var sim3 = NeueSimulation(Daten(("\"beichte\": 0.25", "\"beichte\": 1"), ("\"ehepartner\": 0.1,", "\"ehepartner\": 1,")));
            var w3 = sim3.Wirtschaft!;
            var (p, _) = Haus(w3, "Haus Psalm");
            var kirche = w3.BetriebEroeffnen("Kirche Psalm", Betriebsart.Kirche, p, startkarren: "");
            w3.Einstellen(kirche, "Geselle", 1);
            var (_, fr) = Haus(w3, "Haus Reue", Konfession.Katholisch, "Fromm");
            GeheimnisGeben(w3, fr.Oberhaupt, Geheimnisart.Ketzerei);
            var (hold, fh) = Haus(w3, "Haus Hold", Konfession.Katholisch, "Geduldig");
            Heiraten(w3, hold);
            GeheimnisGeben(w3, fh.Oberhaupt, Geheimnisart.Ehebruch);
            var liste3 = Mitschreiben(w3);
            JahrBeenden(sim3);
            Art(Geheimnisart.Ketzerei, Eine(liste3, $"Im Beichtstuhl hört {p.Inhaber}: {fr.Oberhaupt.Name}"));
            Art(Geheimnisart.Ehebruch, Eine(liste3, "Unfriede im Haus Hold"));
        }),

        ("Geheimnis-Siegel: Spitzel – der Bericht über ein Haus und über einen Ratsherrn trägt die Art des Fundes; Gegenprobe: ohne Fund keine", () =>
        {
            foreach (var sicher in new[] { true, false })
            {
                var chance = sicher ? "1" : "0";
                var sim = NeueSimulation(Daten(("\"spitzel\": 0.3, \"spitzelJeStern\": 0.03, \"spitzelMin\": 0.1, \"spitzelMax\": 0.6, \"spitzelRatsherr\": 0.4",
                    $"\"spitzel\": {chance}, \"spitzelJeStern\": 0, \"spitzelMin\": {chance}, \"spitzelMax\": {chance}, \"spitzelRatsherr\": {chance}")));
                var w = sim.Wirtschaft!;
                var (a, _) = Haus(w, "Haus Spaeher");
                var (b, fb) = Haus(w, "Haus Ziel");
                GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch);
                var r = w.Rat.Ratsherren[0];
                GeheimnisGeben(w, r, Geheimnisart.Ketzerei);
                Ok(w, a, new SpitzelAnsetzen(b.Inhaber));
                Ok(w, a, new SpitzelAnsetzen(r.Kennung));
                var liste = Mitschreiben(w);
                JahrBeenden(sim);
                Art(sicher ? Geheimnisart.Ehebruch : null, Eine(liste, $"Der Spitzel von {a.Inhaber} berichtet über {b.Inhaber}"));
                Art(sicher ? Geheimnisart.Ketzerei : null, Eine(liste, $"Der Spitzel von {a.Inhaber} berichtet über {r.Name}"));
            }
        }),

        ("Geheimnis-Siegel: Entstehen – die vertrauliche Meldung an das eigene Haus trägt die Art (Entwicklerweg, Schulden am Jahresende)", () =>
        {
            var sim = NeueSimulation(Daten());
            var w = sim.Wirtschaft!;
            var (a, fa) = Haus(w, "Haus Last");
            var liste = Mitschreiben(w);
            GeheimnisGeben(w, fa.Oberhaupt, Geheimnisart.Ehebruch);
            var neu = Eine(liste, "hütet nun „Ehebruch“");
            Art(Geheimnisart.Ehebruch, neu);
            Pruefe.Wahr(neu.Vertraulich && w.HausVon(neu) == a, "nur für das eigene Haus");
            a.Familienkosten(25_000);
            JahrBeenden(sim);
            Art(Geheimnisart.Schulden, Eine(liste, "hütet nun „Schulden“"));
        }),

        ("Geheimnis-Siegel: eine Partie mit KI (viel Gerede und Ehebruch) – jede Meldung mit Art nennt die Art im Text (außer Urteilen), jede eindeutige " +
         "Geheimnis-Meldung trägt eine, der öffentliche Satz zum Brief keine; ohne Abschnitt „geheimnisse“ trägt keine Meldung eine Art", () =>
        {
            var daten = Daten(("\"gerede\": 0.02", "\"gerede\": 0.3"), ("\"start\": 0.25, \"jeJahr\": 0.02", "\"start\": 1, \"jeJahr\": 0.2"),
                ("\"Hintergeht heimlich den Ehepartner.\", \"chance\": 0.015", "\"Hintergeht heimlich den Ehepartner.\", \"chance\": 0.3"));
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 4711 }, daten, new NeuePartie());
            var w = sim.Wirtschaft!;
            var liste = Mitschreiben(w);
            for (var i = 0; i < 4; i++) JahrBeenden(sim);
            var mitArt = liste.Where(m => m.GeheimnisArt is not null).ToList();
            Pruefe.Wahr(mitArt.Count >= 5, $"Meldungen mit Art: {mitArt.Count}");
            foreach (var m in mitArt)
                Pruefe.Wahr(m.Text.Contains(w.Geheimnisse.Daten.Art(m.GeheimnisArt!.Value).Name, StringComparison.Ordinal)
                            || m.Text.StartsWith("Gerichtstag", StringComparison.Ordinal), $"Art {m.GeheimnisArt} im Text: {m.Text}");
            string[] eindeutig = { "Ein Geheimnis im Haus ", "Im Beichtstuhl hört ", "nennt das Geheimnis: ", "Unfriede im Haus ", " und verlangt eure Stimme", " bloß: " };
            foreach (var m in liste.Where(m => eindeutig.Any(t => m.Text.Contains(t, StringComparison.Ordinal))))
                Pruefe.Wahr(m.GeheimnisArt is not null, "ohne Art: " + m.Text);
            foreach (var m in liste.Where(m => m.Text.Contains("erhält einen Erpresserbrief", StringComparison.Ordinal)))
                Art(null, m);
            // Gegenprobe: ohne Abschnitt (wie vor Teil 3c) keine Art
            var knoten = JsonNode.Parse(Json.Value)!.AsObject();
            knoten.Remove("geheimnisse");
            var (ohne, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 4711 },
                Wirtschaftsdaten.AusJson(knoten.ToJsonString()), new NeuePartie());
            var ohneListe = Mitschreiben(ohne.Wirtschaft!);
            JahrBeenden(ohne);
            Pruefe.Wahr(ohneListe.Count > 20 && ohneListe.All(m => m.GeheimnisArt is null), "ohne Geheimnisse keine Art");
        }),

        ("Geheimnis-Siegel: Meldungsordnung – eine Meldung über ein Geheimnis ohne andere Art ist „Geheimnis“ (eigen: Siegel, fremd: Verlauf, fremd und " +
         "vertraulich: verworfen); Urteil, Spitzelbericht und Anzeige bleiben Gericht und Spitzel; ohne Art bleibt derselbe Satz „Sonstiges“", () =>
        {
            var o = Meldungsordnung.AusJson(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "game", "daten", "meldungen.json")));
            var geheim = o.Arten.Single(a => a.Geheimnis);
            Pruefe.Gleich("Geheimnis", geheim.Id);
            var satz = "Ein Geheimnis im Haus Brendel: Anna hütet nun „Ehebruch“. Erfährt es ein anderes Haus, kann es euch erpressen, anzeigen oder bloßstellen.";
            Pruefe.Gleich("Geheimnis", o.Einordnen(new Meldung(Meldungsart.Sonstiges, satz) { GeheimnisArt = Geheimnisart.Ehebruch }).Id);
            Pruefe.Gleich("Sonstiges", o.Einordnen(new Meldung(Meldungsart.Sonstiges, satz)).Id, "Gegenprobe ohne Art");
            Pruefe.Gleich(Meldestufe.Siegel, Meldungsordnung.Stufe(geheim, Betroffenheit.Eigen, satz, null));
            Pruefe.Gleich(Meldestufe.Verlauf, Meldungsordnung.Stufe(geheim, Betroffenheit.Fremd, satz, null));
            Pruefe.Gleich("Gericht", o.Einordnen(new Meldung(Meldungsart.Sonstiges, "Gerichtstag 1402: Haus Brendel ist Mord SCHULDIG (Anzeige: Haus Eisenbart).")
                { GeheimnisArt = Geheimnisart.Mord }).Id);
            Pruefe.Gleich("Spitzel", o.Einordnen(new Meldung(Meldungsart.Sonstiges, "Der Spitzel von Haus Brendel berichtet über Haus Eisenbart: nichts Neues " +
                "(insgesamt 0). Dazu ein Geheimnis – Anna Eisenbart: Ehebruch.") { GeheimnisArt = Geheimnisart.Ehebruch }).Id);
            Pruefe.Gleich("Gericht", o.Einordnen(new Meldung(Meldungsart.Anklage, "Haus Brendel zeigt Haus Eisenbart an: Mord (Anna Eisenbart).")
                { GeheimnisArt = Geheimnisart.Mord }).Id);

            // Mit den Meldungen des Kerns: Das Opfer eines Erpresserbriefs bekommt den Brief (Diebstahl) und das Siegel „Geheimnis“, ein drittes Haus nichts davon
            var w = NeueSimulation(Daten()).Wirtschaft!;
            var (a, _) = Haus(w, "Haus Hand");
            var (b, fb) = Haus(w, "Haus Fuss");
            var (c, _) = Haus(w, "Haus Dritt");
            var g = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch, wisser: a);
            var liste = Mitschreiben(w);
            Ok(w, a, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, 500));
            var nennt = Eine(liste, "nennt das Geheimnis");
            var oeffentlich = Eine(liste, "erhält einen Erpresserbrief");
            var opfer = o.Einstufen(w, b, nennt.Text, nennt);
            Pruefe.Wahr(opfer.Art.Id == "Geheimnis" && opfer.Bezug == Betroffenheit.Eigen && !opfer.Verwerfen, $"Opfer: {opfer.Art.Id} {opfer.Bezug}");
            Pruefe.Gleich(Meldestufe.Siegel, Meldungsordnung.Stufe(opfer.Art, opfer.Bezug, opfer.Text, null));
            Pruefe.Gleich("Diebstahl", o.Einstufen(w, b, oeffentlich.Text, oeffentlich).Art.Id);
            Pruefe.Wahr(o.Einstufen(w, c, nennt.Text, nennt).Verwerfen, "ein drittes Haus liest den vertraulichen Satz nicht");
        }),

        ("Geheimnis-Siegel: Spielstand – Art an Meldungen und Erpresserbrief bleibt (speichern, laden, gleich weiter); ohne Geheimnis-Meldung kein Feld", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 41 },
                Daten(), new NeuePartie());
            JahrBeenden(sim);
            var w = sim.Wirtschaft!;
            // Ohne Geheimnis-Meldung: kein Feld (die Ratsherren haben ohne Zufall keine Geheimnisse, niemand nutzt etwas)
            var vorher = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            Pruefe.Wahr(!vorher.Contains("\"GeheimnisArt\"", StringComparison.Ordinal), "kein Feld ohne Geheimnis-Meldung");
            Pruefe.Wahr(w.Meldungsverlauf.Count > 20, "Meldungen im Verlauf");
            // Mit: Brief, Bloßstellen, Ratsstimme
            spieler.Aktionspunkte.Setzen(12);
            var b = w.Konten.First(k => k != spieler && k.IstAktiv && w.Familien.Von(k) is not null);
            var fb = w.Familien.Von(b)!;
            var g1 = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Ehebruch, wisser: spieler);
            var g2 = GeheimnisGeben(w, fb.Oberhaupt, Geheimnisart.Schulden, wisser: spieler);
            Ok(w, spieler, new GeheimnisNutzen(g1.Nummer, Geheimnisnutzung.Gulden, 300));
            Ok(w, spieler, new GeheimnisNutzen(g2.Nummer, Geheimnisnutzung.Blossstellen));
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var n = JsonNode.Parse(json)!.AsObject();
            Pruefe.Wahr(Zaehlen(n["Meldungen"]!.ToJsonString(), "\"GeheimnisArt\"") >= 3, "Art in den Meldungen des Spielstands");
            Pruefe.Wahr(json.Contains("\"GeheimnisArt\": \"Ehebruch\"", StringComparison.Ordinal), "Art als Name");
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            var w2 = geladen.Wirtschaft!;
            Pruefe.Wahr(w.Meldungsverlauf.SequenceEqual(w2.Meldungsverlauf), "derselbe Verlauf (mit Art)");
            var b2 = w2.Konten[w.Konten.ToList().IndexOf(b)];
            Pruefe.Gleich((Geheimnisart?)Geheimnisart.Ehebruch, w2.Gauner.ForderungAn(b2, geladenerSpieler!)?.GeheimnisArt);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, Zeitpunkt));
            JahrBeenden(sim);
            JahrBeenden(geladen);
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, Zeitpunkt), Speicherung.Speichern(geladen, geladenerSpieler, Zeitpunkt), "ein Jahr weiter gleich");
        }),

        ("Geheimnis-Siegel: alter Spielstand – ohne das Feld (wie vor dem Siegel) lädt er, die Meldungen ohne Art, der Brief nennt die Art über sein Geheimnis; " +
         "neu gespeichert bleibt er gleich, die Partie läuft wie mit dem Feld; der Greybox-Stand 1406 bekommt kein Feld", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 41 },
                Daten(), new NeuePartie());
            JahrBeenden(sim);
            var w = sim.Wirtschaft!;
            spieler.Aktionspunkte.Setzen(12);
            var b = w.Konten.First(k => k != spieler && k.IstAktiv && w.Familien.Von(k) is not null);
            var g = GeheimnisGeben(w, w.Familien.Von(b)!.Oberhaupt, Geheimnisart.Ehebruch, wisser: spieler);
            Ok(w, spieler, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, 300));
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var alt = OhneNeuesFeld(json).ToJsonString();
            Pruefe.Wahr(json.Contains("\"GeheimnisArt\"", StringComparison.Ordinal) && !alt.Contains("\"GeheimnisArt\"", StringComparison.Ordinal), "der alte Stand hat kein Feld");
            var (geladen, geladenerSpieler) = Speicherung.Laden(alt);
            var w2 = geladen.Wirtschaft!;
            Pruefe.Wahr(w2.Meldungsverlauf.Count == w.Meldungsverlauf.Count && w2.Meldungsverlauf.All(m => m.GeheimnisArt is null), "Meldungen ohne Art");
            var brief = w2.Gauner.ForderungAn(w2.Konten[w.Konten.ToList().IndexOf(b)], geladenerSpieler!)!;
            Pruefe.Gleich((Geheimnisart?)null, brief.GeheimnisArt);
            Pruefe.Gleich((Geheimnisart?)Geheimnisart.Ehebruch, w2.Geheimnisse.ArtDesBriefes(brief), "über die Nummer des Geheimnisses");
            // neu gespeichert: nichts kommt dazu
            Pruefe.Gleich(alt, JsonNode.Parse(Speicherung.Speichern(geladen, geladenerSpieler, Zeitpunkt))!.ToJsonString(), "neu gespeichert gleich");
            // ein Jahr weiter: bis auf das Feld derselbe Stand wie mit dem Feld
            JahrBeenden(sim);
            JahrBeenden(geladen);
            Pruefe.Gleich(OhneNeuesFeld(Speicherung.Speichern(sim, spieler, Zeitpunkt)).ToJsonString(),
                OhneNeuesFeld(Speicherung.Speichern(geladen, geladenerSpieler, Zeitpunkt)).ToJsonString(), "ein Jahr weiter gleich");
            Pruefe.Wahr(w2.Meldungsverlauf.Any(m => m.GeheimnisArt is null), "die alten Meldungen bleiben ohne Art");

            // Greybox, Herbst 1406, Datenversion 55: kein Feld, auch nach einem Jahr
            var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
            using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
            var (greybox, greyboxSpieler) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            JahrBeenden(greybox);
            var nachJahr = Speicherung.Speichern(greybox, greyboxSpieler, Zeitpunkt);
            Pruefe.Wahr(!nachJahr.Contains("\"GeheimnisArt\"", StringComparison.Ordinal), "Greybox 1406: kein Feld");
            var (wieder, wiederSpieler) = Speicherung.Laden(nachJahr);
            Pruefe.Gleich(nachJahr, Speicherung.Speichern(wieder, wiederSpieler, Zeitpunkt));
        }),
    };
}
