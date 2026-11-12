using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entscheidungen;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Entscheidungen (Benachrichtigungen Stufe 3, Teil 1): Erpresserbrief, Schutzgeld und Lösegeld als offene Fragen mit Frist und Voreinstellung,
/// Befehl <see cref="EntscheidungTreffen"/>, Doppelweg über die Gauner-Tafel, Obergrenze je Haus, Spielstand alt und neu. Jede Regel mit Gegenprobe.
/// Die Forderungen stellt das Entwicklerfenster zu (<see cref="EntwicklerForderung"/>) – ohne Beweise, Versteck und Würfel.
/// </summary>
static class EntscheidungTests
{
    /// <summary>Die offenen Fragen der Unterwelt (Teil 1) – Teil 2 fragt in langen Läufen auch nach Krankheit, Erbe, Geheimnis und Mahnung.</summary>
    static List<Entscheidung> Unterwelt(this Entscheidungswesen ew) => ew.Offen.Where(e => EntscheidungDaten.HatJahresfrist(e.Art)).ToList();

    static List<Entscheidung> UnterweltFuer(this Entscheidungswesen ew, Konto haus) => ew.OffenFuer(haus).Where(e => EntscheidungDaten.HatJahresfrist(e.Art)).ToList();

    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 11, 10, 12, 0, 0);

    /// <summary>Ein Mensch („Haus Mensch“ mit Familie und Tochter), zwei fordernde Häuser, Entwicklerbefehle erlaubt.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Mensch, Konto Elster, Konto Rabe) Partie(double vermoegen = 10_000)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value) { EntwicklerErlaubt = true };
        var w = sim.Wirtschaft!;
        var mensch = w.KontoEroeffnen("Haus Mensch", startvermoegen: vermoegen);
        w.Familien.Gruenden(mensch, Volk.Mensch, Geschlecht.Mann, alter: 40);
        w.Familien.KindAufnehmen(mensch, 16);
        w.Familien.KindAufnehmen(mensch, 14, Geschlecht.Mann);
        var elster = w.KontoEroeffnen("Haus Elster", startvermoegen: 10_000);
        var rabe = w.KontoEroeffnen("Haus Rabe", startvermoegen: 10_000);
        return (sim, w, mensch, elster, rabe);
    }

    static void Fordern(Stadtwirtschaft w, Konto opfer, Konto von, Entscheidungsart art, double betrag) =>
        Pruefe.Wahr(w.Ausfuehren(opfer, new EntwicklerForderung(art, von.Inhaber, betrag)).Erfolg, $"Forderung {art} von {von.Inhaber}");

    static Entscheidung Einzige(Stadtwirtschaft w, Konto haus) =>
        w.Entscheidungen.UnterweltFuer(haus) is { Count: 1 } l ? l[0] : throw new Exception($"Erwartet eine offene Entscheidung, sind {w.Entscheidungen.UnterweltFuer(haus).Count}.");

    static List<Meldung> Mitschreiben(Stadtwirtschaft w)
    {
        var liste = new List<Meldung>();
        // Nur die Meldungen der Unterwelt-Fragen (Teil 2 fragt in langen Läufen auch nach Krankheit, Erbe …): Schliessen meldet unmittelbar vor Geschlossen.
        w.Entscheidungen.Geschlossen += (e, _) =>
        {
            if (EntscheidungDaten.HatJahresfrist(e.Art) && w.Meldungsverlauf.Count > 0 && w.Meldungsverlauf[^1] is { Art: Meldungsart.Entscheidung } m) liste.Add(m);
        };
        return liste;
    }

    /// <summary>Bis zur ersten Minute des Jahres <paramref name="jahr"/> (Spieljahr ab 1400).</summary>
    static void BisJahr(Simulation sim, int jahr)
    {
        while (sim.Zeit.Jahr < jahr) sim.Tick();
    }

    static Person Tochter(Stadtwirtschaft w, Konto k) => w.Familien.Von(k)!.Lebende.First(p => p != w.Familien.Von(k)!.Oberhaupt);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Entscheidungen: Ein Erpresserbrief an einen Menschen wird in der nächsten Minute eine Frage mit Jahresfrist und Voreinstellung Ablehnen; ein KI-Haus bekommt keine", () =>
        {
            var (sim, w, mensch, elster, rabe) = Partie();
            w.KiAnmelden(rabe);
            Fordern(w, mensch, elster, Entscheidungsart.Erpresserbrief, 400);
            Fordern(w, rabe, elster, Entscheidungsart.Erpresserbrief, 400);
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count, "vor dem Tick");
            sim.Tick();
            var e = Einzige(w, mensch);
            Pruefe.Gleich(Entscheidungsart.Erpresserbrief, e.Art);
            Pruefe.Gleich(elster, e.Absender);
            Pruefe.Gleich(400.0, e.Betrag);
            Pruefe.Gleich(1, e.Nummer);
            Pruefe.Gleich(Entscheidungswesen.Ablehnen, e.Voreinstellung);
            Pruefe.Gleich(2L * Spielzeit.MinutenProTag - 1, e.Frist, "letzte Minute des Folgejahres");
            Pruefe.Gleich(0, w.Entscheidungen.UnterweltFuer(rabe).Count, "KI-Haus");
            Pruefe.Gleich(1, w.Entscheidungen.Unterwelt().Count);
            Pruefe.Wahr(w.Entscheidungen.Titel(e).Contains("Haus Elster") && w.Entscheidungen.Beschreibung(e).Contains("400"), w.Entscheidungen.Beschreibung(e));
            Pruefe.Gleich(2, w.Entscheidungen.Wahlen(e).Count);
            sim.Tick();
            Pruefe.Gleich(1, w.Entscheidungen.Unterwelt().Count, "keine zweite Frage zur selben Forderung");
        }),

        ("Entscheidungen: EntscheidungTreffen zahlt über den bestehenden Befehl (0 AP), schließt die Frage und schreibt eine vertrauliche Meldung in den Verlauf", () =>
        {
            var (sim, w, mensch, elster, _) = Partie();
            var meldungen = Mitschreiben(w);
            Fordern(w, mensch, elster, Entscheidungsart.Erpresserbrief, 400);
            sim.Tick();
            var e = Einzige(w, mensch);
            var (ap, geld, geldElster) = (mensch.Aktionspunkte.Vorrat, mensch.Vermoegen, elster.Vermoegen);
            var r = w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer, Entscheidungswesen.Zahlen));
            Pruefe.Wahr(r.Erfolg, r.Meldung);
            Pruefe.Gleich(ap, mensch.Aktionspunkte.Vorrat, "0 AP");
            Pruefe.Ungefaehr(geld - 400, mensch.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(geldElster + 400, elster.Vermoegen, 1e-9);
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count);
            Pruefe.Wahr(w.Gauner.ForderungAn(mensch, elster) is null, "Forderung erledigt");
            var m = meldungen.Single();
            Pruefe.Wahr(m.Vertraulich && w.HausVon(m) == mensch && m.Text.Contains("Schweigegeld gezahlt"), m.Text);
            Pruefe.Gleich(m, w.Meldungsverlauf.Last(x => x.Art == Meldungsart.Entscheidung));
            // Gegenprobe: Dieselbe Nummer ein zweites Mal geht nicht.
            Pruefe.Wahr(!w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer, Entscheidungswesen.Zahlen)).Erfolg, "schon entschieden");
        }),

        ("Entscheidungen: Ein fehlgeschlagener Befehl (zu wenig Geld) lässt die Frage offen – mit Geld klappt dieselbe Wahl", () =>
        {
            var (sim, w, mensch, elster, _) = Partie(vermoegen: 100);
            Fordern(w, mensch, elster, Entscheidungsart.Schutzgeld, 300);
            sim.Tick();
            var e = Einzige(w, mensch);
            var r = w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer, Entscheidungswesen.Zahlen));
            Pruefe.Wahr(!r.Erfolg && r.Meldung.Contains("nicht"), r.Meldung);
            Pruefe.Gleich(e, Einzige(w, mensch));
            Pruefe.Wahr(w.Entscheidungen.Folgen(e, Entscheidungswesen.Zahlen).Contains("reicht euer Geld nicht"), w.Entscheidungen.Folgen(e, Entscheidungswesen.Zahlen));
            w.Ausfuehren(mensch, new EntwicklerGulden(1_000));
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer, Entscheidungswesen.Zahlen)).Erfolg, "mit Geld");
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count);
        }),

        ("Entscheidungen: Falsche Nummer, fremdes Haus und unbekannte Wahl werden abgelehnt, die Frage bleibt", () =>
        {
            var (sim, w, mensch, elster, rabe) = Partie();
            Fordern(w, mensch, elster, Entscheidungsart.Erpresserbrief, 400);
            sim.Tick();
            var e = Einzige(w, mensch);
            Pruefe.Wahr(!w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer + 5, 0)).Erfolg, "Nummer");
            Pruefe.Wahr(!w.Ausfuehren(rabe, new EntscheidungTreffen(e.Nummer, 0)).Erfolg, "fremdes Haus");
            Pruefe.Wahr(!w.Ausfuehren(mensch, new EntscheidungTreffen(e.Nummer, 2)).Erfolg, "Wahl 2");
            Pruefe.Gleich(e, Einzige(w, mensch));
        }),

        ("Entscheidungen: Doppelweg – antwortet die Gauner-Tafel direkt, schließt das die Frage (mit Meldung); eine fremde Antwort schließt sie nicht", () =>
        {
            var (sim, w, mensch, elster, rabe) = Partie();
            var meldungen = Mitschreiben(w);
            Fordern(w, mensch, elster, Entscheidungsart.Schutzgeld, 300);
            Fordern(w, mensch, rabe, Entscheidungsart.Erpresserbrief, 200);
            sim.Tick();
            Pruefe.Gleich(2, w.Entscheidungen.UnterweltFuer(mensch).Count);
            // Gegenprobe: Eine Antwort auf die andere Forderung schließt nur deren Frage.
            Pruefe.Wahr(w.Ausfuehren(mensch, new ErpressungBeantworten(rabe.Inhaber, false)).Erfolg, "Erpresser abgewiesen");
            var rest = Einzige(w, mensch);
            Pruefe.Gleich(Entscheidungsart.Schutzgeld, rest.Art);
            Pruefe.Wahr(meldungen.Single().Text.Contains("Erpresserbrief von Haus Rabe zurückgewiesen"), meldungen.Single().Text);
            Pruefe.Wahr(w.Ausfuehren(mensch, new DiebesforderungBeantworten(elster.Inhaber, Diebesbriefart.Schutzgeld, true)).Erfolg, "Tafel zahlt");
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count);
            Pruefe.Gleich(2, meldungen.Count);
            Pruefe.Wahr(!w.Ausfuehren(mensch, new EntscheidungTreffen(rest.Nummer, Entscheidungswesen.Ablehnen)).Erfolg, "nichts mehr offen");
            sim.Tick();
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count, "keine neue Frage aus der erledigten Forderung");
        }),

        ("Entscheidungen: Fristablauf beim Erpresserbrief – bis Ende des Folgejahres offen, dann gilt Schweigen als Nein (Abschluss Frist)", () =>
        {
            var (sim, w, mensch, elster, _) = Partie();
            var meldungen = Mitschreiben(w);
            var abschluesse = new List<Entscheidungsabschluss>();
            w.Entscheidungen.Geschlossen += (e, a) => { if (EntscheidungDaten.HatJahresfrist(e.Art)) abschluesse.Add(a); };
            Fordern(w, mensch, elster, Entscheidungsart.Erpresserbrief, 400);
            sim.Tick();
            var e = Einzige(w, mensch);
            // Gegenprobe: Nach dem ersten Jahreswechsel ist sie noch offen.
            BisJahr(sim, 1401);
            sim.Tick();
            Pruefe.Gleich(e, Einzige(w, mensch));
            while (sim.Zeit.VergangeneMinuten < e.Frist) sim.Tick();
            Pruefe.Gleich(0L, w.Entscheidungen.RestMinuten(e));
            Pruefe.Wahr(w.Gauner.ForderungAn(mensch, elster) is not null, "vor der letzten Minute steht die Forderung");
            var geld = mensch.Vermoegen;
            sim.Tick(); // letzte Minute: Jahresrechnung, Schweigen heißt Nein
            Pruefe.Wahr(w.Gauner.ForderungAn(mensch, elster) is null, "Forderung verfallen");
            sim.Tick(); // Abgleich: Frage geschlossen
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count);
            Pruefe.Gleich(Entscheidungsabschluss.Frist, abschluesse.Single());
            Pruefe.Wahr(meldungen.Single().Text.Contains("Schweigen galt als Nein"), meldungen.Single().Text);
            Pruefe.Wahr(mensch.Vermoegen <= geld, "nichts gezahlt");
        }),

        ("Entscheidungen: Lösegeld – bei Fristablauf zahlt die Voreinstellung, wenn das Geld reicht; sonst gilt Ablehnen und die Geisel ist fort", () =>
        {
            foreach (var reich in new[] { true, false })
            {
                var (sim, w, mensch, elster, _) = Partie(vermoegen: reich ? 10_000 : 50);
                var meldungen = Mitschreiben(w);
                var tochter = Tochter(w, mensch);
                Fordern(w, mensch, elster, Entscheidungsart.Loesegeld, 800);
                sim.Tick();
                var e = Einzige(w, mensch);
                Pruefe.Gleich(tochter.Nummer, e.Person);
                Pruefe.Gleich(Entscheidungswesen.Zahlen, e.Voreinstellung);
                Pruefe.Gleich(reich ? Entscheidungswesen.Zahlen : Entscheidungswesen.Ablehnen, w.Entscheidungen.Voreinstellung(e));
                Pruefe.Wahr(w.Entscheidungen.Folgen(e, Entscheidungswesen.Ablehnen).Contains("nicht zurücknehmen"), "Folgen nennen das Unumkehrbare");
                Pruefe.Wahr(w.Diebe.IstGeisel(tochter), "Geisel");
                while (sim.Zeit.VergangeneMinuten <= e.Frist) sim.Tick();
                sim.Tick();
                Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count, reich ? "reich" : "arm");
                Pruefe.Wahr(!w.Diebe.IstGeisel(tochter), "keine Geisel mehr");
                Pruefe.Gleich(reich, tochter.Lebt && w.Familien.Von(mensch)!.Lebende.Contains(tochter), reich ? "frei" : "verkauft");
                Pruefe.Wahr(meldungen.Single().Text.Contains(reich ? "Lösegeld für" : "kein Lösegeld"), meldungen.Single().Text);
            }
        }),

        ("Entscheidungen: Schutzgeld unbeantwortet – nach Fristablauf brennt es wie vor Stufe 3 in der nächsten Nacht", () =>
        {
            var (sim, w, mensch, elster, _) = Partie();
            var giesserei = w.BetriebEroeffnen("Gießerei Mensch", Betriebsart.Giesserei, mensch);
            Pruefe.Wahr(w.Ausfuehren(elster, new EntwicklerDiebeshaus()).Erfolg, "Versteck");
            Fordern(w, mensch, elster, Entscheidungsart.Schutzgeld, 300);
            sim.Tick();
            var e = Einzige(w, mensch);
            Pruefe.Wahr(w.Entscheidungen.Folgen(e, Entscheidungswesen.Ablehnen).Contains(giesserei.Name), w.Entscheidungen.Folgen(e, Entscheidungswesen.Ablehnen));
            while (sim.Zeit.VergangeneMinuten <= e.Frist) sim.Tick();
            Pruefe.Wahr(w.Diebe.Auftraege.Any(a => a.Art == Diebesstueck.Brandstiftung && a.Betrieb == giesserei.Name), "Brand für die nächste Nacht vorgemerkt");
            sim.Tick();
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count);
        }),

        ("Entscheidungen: Die Obergrenze löst bei der Unterwelt nichts aus – auch mit hoechstOffen 1 bleiben alle Forderungen offen, nichts wird gezahlt oder abgelehnt", () =>
        {
            foreach (var hoechstens in new[] { 6, 1 })
            {
                var daten = Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft())
                    .Replace("\"hoechstOffen\": 6", $"\"hoechstOffen\": {hoechstens}"));
                var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten) { EntwicklerErlaubt = true };
                var w = sim.Wirtschaft!;
                Pruefe.Gleich(hoechstens, w.Entscheidungen.Daten.HoechstOffen);
                var mensch = w.KontoEroeffnen("Haus Mensch", startvermoegen: 10_000);
                w.Familien.Gruenden(mensch, Volk.Mensch, Geschlecht.Mann, alter: 40);
                var tochter = w.Familien.KindAufnehmen(mensch, 16);
                var abschluesse = new List<(Entscheidung, Entscheidungsabschluss)>();
                w.Entscheidungen.Geschlossen += (e, a) => { if (EntscheidungDaten.HatJahresfrist(e.Art)) abschluesse.Add((e, a)); };
                var erpresser = Enumerable.Range(1, 7).Select(i => w.KontoEroeffnen($"Haus Erpresser {i}", startvermoegen: 1_000)).ToList();
                for (var i = 0; i < 7; i++)
                {
                    Fordern(w, mensch, erpresser[i], Entscheidungsart.Erpresserbrief, 100 + i);
                    sim.Tick();
                }
                Fordern(w, mensch, erpresser[0], Entscheidungsart.Loesegeld, 500);
                var geld = mensch.Vermoegen;
                sim.Tick();
                Pruefe.Gleich(8, w.Entscheidungen.UnterweltFuer(mensch).Count, $"alle acht offen (hoechstOffen {hoechstens})");
                Pruefe.Gleich(0, abschluesse.Count, "keine lief aus");
                Pruefe.Gleich(7, w.Gauner.Forderungen.Count(f => f.Opfer == mensch));
                Pruefe.Wahr(w.Diebe.IstGeisel(tochter), "die Geisel bleibt Geisel");
                Pruefe.Gleich(geld, mensch.Vermoegen, "nichts gezahlt");
            }
        }),

        ("Entscheidungen: Eine direkte Antwort schließt nur ihre eigene Frage – eine zugleich verfallene Lösegeldforderung schließt der Abgleich mit „Schweigen“", () =>
        {
            var (sim, w, mensch, elster, rabe) = Partie(vermoegen: 50);
            var meldungen = Mitschreiben(w);
            var abschluesse = new List<(Entscheidung, Entscheidungsabschluss)>();
            w.Entscheidungen.Geschlossen += (e, a) => { if (EntscheidungDaten.HatJahresfrist(e.Art)) abschluesse.Add((e, a)); };
            Fordern(w, mensch, elster, Entscheidungsart.Loesegeld, 800);
            sim.Tick();
            var loesegeld = Einzige(w, mensch);
            BisJahr(sim, 1401);
            Fordern(w, mensch, rabe, Entscheidungsart.Erpresserbrief, 100);
            sim.Tick();
            while (sim.Zeit.VergangeneMinuten <= loesegeld.Frist) sim.Tick(); // die letzte Minute: Jahresrechnung, die Geisel wird verkauft
            Pruefe.Wahr(!w.Diebe.IstGeisel(Tochter(w, mensch)) && w.Diebe.Briefe.Count == 0, "Lösegeld verfallen");
            Pruefe.Gleich(2, w.Entscheidungen.UnterweltFuer(mensch).Count, "vor dem Abgleich noch beide offen");
            Pruefe.Wahr(w.Ausfuehren(mensch, new ErpressungBeantworten(rabe.Inhaber, false)).Erfolg, "Tafel lehnt den Erpresser ab");
            var (erste, grund) = abschluesse.Single();
            Pruefe.Gleich(Entscheidungsart.Erpresserbrief, erste.Art);
            Pruefe.Gleich(Entscheidungsabschluss.Entschieden, grund);
            Pruefe.Gleich(loesegeld.Nummer, Einzige(w, mensch).Nummer); // Gegenprobe: das Lösegeld bekommt nicht Grund und Wahl der Tafel
            sim.Tick();
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count);
            Pruefe.Gleich(Entscheidungsabschluss.Frist, abschluesse[1].Item2);
            Pruefe.Wahr(meldungen[^1].Text.Contains("kein Lösegeld gezahlt") && !meldungen.Any(m => m.Text.Contains("Lösegeld für") && m.Text.Contains("gezahlt.")),
                string.Join(" | ", meldungen.Select(m => m.Text)));
        }),

        ("Entscheidungen: Zwei Geiseln beim selben Dieb – die jüngere Forderung wartet, bis die ältere beantwortet ist", () =>
        {
            var (sim, w, mensch, elster, _) = Partie();
            Fordern(w, mensch, elster, Entscheidungsart.Loesegeld, 500);
            Fordern(w, mensch, elster, Entscheidungsart.Loesegeld, 600);
            sim.Tick();
            var liste = w.Entscheidungen.Unterwelt().OrderBy(e => e.Nummer).ToList();
            Pruefe.Gleich(2, liste.Count);
            var r = w.Ausfuehren(mensch, new EntscheidungTreffen(liste[1].Nummer, Entscheidungswesen.Zahlen));
            Pruefe.Wahr(!r.Erfolg && r.Meldung.Contains("zuerst"), r.Meldung);
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntscheidungTreffen(liste[0].Nummer, Entscheidungswesen.Zahlen)).Erfolg, "ältere");
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntscheidungTreffen(liste[1].Nummer, Entscheidungswesen.Voreinstellungswahl)).Erfolg, "jüngere mit Voreinstellung");
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count);
            Pruefe.Wahr(w.Familien.Von(mensch)!.Lebende.Count() == 3, "beide frei");
        }),

        ("Entscheidungen: Spielstand – offene Fragen und Nummern überstehen Speichern und Laden; ohne je eine Frage fehlt der Abschnitt", () =>
        {
            var (sim, w, mensch, elster, rabe) = Partie();
            var leer = JsonNode.Parse(Speicherung.Speichern(sim, mensch, Zeitpunkt))!["Wirtschaft"]!.AsObject();
            Pruefe.Wahr(!leer.ContainsKey("Entscheidungen"), "ohne Frage kein Abschnitt");
            Fordern(w, mensch, elster, Entscheidungsart.Erpresserbrief, 400);
            Fordern(w, mensch, rabe, Entscheidungsart.Loesegeld, 700);
            sim.Tick();
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntscheidungTreffen(1, Entscheidungswesen.Ablehnen)).Erfolg, "erste entschieden");
            var json = Speicherung.Speichern(sim, mensch, Zeitpunkt);
            var (geladen, spieler) = Speicherung.Laden(json);
            var w2 = geladen.Wirtschaft!;
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, spieler, Zeitpunkt));
            var e = Einzige(w2, spieler!);
            Pruefe.Gleich((2, Entscheidungsart.Loesegeld, "Haus Rabe", 700.0), (e.Nummer, e.Art, e.Absender!.Inhaber, e.Betrag));
            Fordern(w2, spieler!, w2.Konten.First(k => k.Inhaber == "Haus Elster"), Entscheidungsart.Schutzgeld, 300);
            geladen.Tick();
            Pruefe.Gleich(3, w2.Entscheidungen.Unterwelt().Max(x => x.Nummer), "die Nummern laufen weiter");
        }),

        ("Entscheidungen: Alter Spielstand ohne Abschnitt lädt mit leerer Liste – die erste Minute legt die Fragen aus den Forderungen neu an", () =>
        {
            var (sim, w, mensch, elster, rabe) = Partie();
            Fordern(w, mensch, elster, Entscheidungsart.Erpresserbrief, 400);
            Fordern(w, mensch, rabe, Entscheidungsart.Schutzgeld, 300);
            sim.Tick();
            var vorher = w.Entscheidungen.Unterwelt().Select(e => (e.Art, e.Absender!.Inhaber, e.Betrag, e.Frist)).ToList();
            var n = JsonNode.Parse(Speicherung.Speichern(sim, mensch, Zeitpunkt))!.AsObject();
            Pruefe.Wahr(n["Wirtschaft"]!.AsObject().Remove("Entscheidungen"), "Abschnitt entfernt");
            var (geladen, spieler) = Speicherung.Laden(n.ToJsonString());
            Pruefe.Gleich(0, geladen.Wirtschaft!.Entscheidungen.Unterwelt().Count, "leer nach dem Laden");
            geladen.Tick();
            var nachher = geladen.Wirtschaft.Entscheidungen.Unterwelt().Select(e => (e.Art, e.Absender!.Inhaber, e.Betrag, e.Frist)).ToList();
            Pruefe.Gleich(string.Join("; ", vorher), string.Join("; ", nachher));
            Pruefe.Wahr(geladen.Wirtschaft.Entscheidungen.Unterwelt().All(e => e.Haus == spieler), "an den Menschen");
        }),

        ("Entscheidungen: Über den Befehlsboten mit Verzögerung wirkt die Wahl erst im Ziel-Tick; das Protokoll spielt dieselbe Partie ab", () =>
        {
            var (sim, w, mensch, elster, _) = Partie();
            Fordern(w, mensch, elster, Entscheidungsart.Erpresserbrief, 400);
            sim.Tick();
            var e = Einzige(w, mensch);
            sim.Bote.AufzeichnungBeginnen();
            sim.Bote.Verzoegerung = 2;
            sim.Bote.Einreihen(mensch, new EntscheidungTreffen(e.Nummer, Entscheidungswesen.Zahlen));
            sim.Tick();
            Pruefe.Gleich(1, w.Entscheidungen.Unterwelt().Count, "noch nicht");
            sim.Tick();
            sim.Tick();
            Pruefe.Gleich(0, w.Entscheidungen.Unterwelt().Count, "im Ziel-Tick");
            var protokoll = sim.Bote.AufzeichnungBeenden();
            var abgespielt = protokoll.Abspielen();
            Pruefe.Gleich(Reichsgilde.Core.Mehrspieler.Zustandspruefsumme.Berechnen(sim), Reichsgilde.Core.Mehrspieler.Zustandspruefsumme.Berechnen(abgespielt));
        }),

        ("Entscheidungen: Daten – fristMinuten nur für Arten ohne Jahresfrist, ab 1 Minute; Obergrenze ab 1; Datenversion 84", () =>
        {
            Pruefe.Gleich(85, Daten.Value.Version);
            Pruefe.Gleich(6, Daten.Value.Entscheidungen!.HoechstOffen);
            Pruefe.Gleich(120, Daten.Value.Entscheidungen.FristFuer(Entscheidungsart.Erpresserbrief), "standard");
            var json = File.ReadAllText(Datenpfad.Wirtschaft());
            const string alt = "\"entscheidungen\": { \"hoechstOffen\": 6, \"erbeAbSterberisiko\": 0.1, \"fristMinuten\": { \"standard\": 120, \"Krankheit\": 120, \"Erbe\": 120, \"Geheimnis\": 120, \"Mahnung\": 120 } }";
            Pruefe.Wahr(json.Contains(alt), "Abschnitt gefunden");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace(alt, "\"entscheidungen\": { \"hoechstOffen\": 0 }")), "hoechstOffen");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace(alt, "\"entscheidungen\": { \"fristMinuten\": { \"Loesegeld\": 60 } }")), "Jahresfrist");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace(alt, "\"entscheidungen\": { \"fristMinuten\": { \"Medikus\": 60 } }")), "Medikus");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace(alt, "\"entscheidungen\": { \"fristMinuten\": { \"standard\": 0 } }")), "mindestens 1");
            var ohne = Wirtschaftsdaten.AusJson(json.Replace(alt + ",", ""));
            Pruefe.Gleich<EntscheidungDaten?>(null, ohne.Entscheidungen);
            var w = new Stadtwirtschaft(ohne, Fronstatut.FreieStadt, 1);
            Pruefe.Gleich(6, w.Entscheidungen.Daten.HoechstOffen, "ohne Abschnitt die Vorgabe");
        }),

        ("Entscheidungen: Die Forderung des Entwicklerfensters geht nur mit erlaubten Entwicklerbefehlen", () =>
        {
            var (sim, w, mensch, elster, _) = Partie();
            sim.EntwicklerErlaubt = false;
            Pruefe.Wahr(!w.Ausfuehren(mensch, new EntwicklerForderung(Entscheidungsart.Erpresserbrief, elster.Inhaber, 100)).Erfolg, "gesperrt");
            sim.EntwicklerErlaubt = true;
            Pruefe.Wahr(!w.Ausfuehren(mensch, new EntwicklerForderung(Entscheidungsart.Erpresserbrief, mensch.Inhaber, 100)).Erfolg, "nicht von sich selbst");
            Pruefe.Wahr(w.Ausfuehren(mensch, new EntwicklerForderung(Entscheidungsart.Erpresserbrief, elster.Inhaber, 100)).Erfolg, "erlaubt");
            Pruefe.Wahr(!w.Ausfuehren(mensch, new EntwicklerForderung(Entscheidungsart.Erpresserbrief, elster.Inhaber, 100)).Erfolg, "nicht doppelt");
        }),
    };
}
