using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Ordnung;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Der Wachplan (Karte an der Wand der Wachstube, PR #173): wer jetzt auf Streife, beim Geleit oder im Haus ist und was die Leute im Haus tun,
/// wer in der Arrestzelle sitzt, dazu die Zeilen aus dem Bedienfeld der Betriebsverwaltung (Sold, Streife, Razzien, Geleit – <see cref="BetriebsVerwaltung.WacheZeilen"/>).
/// Die Tafel liest nur Abfragen des Kerns (<see cref="Wachwesen.AnwesenheitJetzt"/>, <see cref="Wachwesen.Gefangene"/>) und ändert nichts.
/// </summary>
public sealed class WachplanAnzeige : InnenTafel
{
    public WachplanAnzeige() : base(640) { }

    protected override string Stand(Stadtwirtschaft w, Betrieb b) =>
        string.Join(",", w.Wache.AnwesenheitJetzt(b).Select(s => $"{s.Arbeiter.Nummer}{(int)s.Ort}{(int)s.Taetigkeit}"))
        + "|" + string.Join(",", w.Wache.Gefangene(b).Select(g => g.Spur)) + "|" + string.Join("\n", BetriebsVerwaltung.WacheZeilen(b, w));

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        var uhr = w.Uhr.UhrzeitInMinuten;
        Kopf("Wachplan", b, $"Es ist {uhr / 60:00}:{uhr % 60:00} Uhr · Wachwechsel jede gerade Stunde");
        var stand = w.Wache.AnwesenheitJetzt(b);
        if (stand.Count == 0)
            Text(Inhalt, "Niemand arbeitet in diesem Haus – die Stadt zahlt keinen Sold.", 14, Grau, 600);
        else
        {
            var raster = Raster(2, 18);
            string Nummern(IEnumerable<Wachstand> liste) => string.Join(", ", liste.Select(s => (s.Hauptmann ? "Hauptmann " : "") + "Nr. " + s.Arbeiter.Nummer));
            void Zeile(string titel, string inhalt, Color farbe)
            {
                var tipp = titel switch
                {
                    "Auf Streife" => "Wer gerade die Gassen der Stadt abgeht: Streifen fassen Taschendiebe und schrecken Gauner ab.",
                    "Beim Geleit" => "Wer einen Karren ins Umland begleitet; je Karren geht einer mit.",
                    _ => "Wer gerade im Haus ist und womit er die Zeit verbringt. Zum Wachwechsel jede gerade Stunde wird neu eingeteilt.",
                };
                Tipp(Zelle(raster, titel, 14, Grau, 130), tipp);
                Tipp(Text(raster, inhalt, 14, farbe, 440), tipp);
            }
            var streife = stand.Where(s => s.Ort == Wachort.Patrouille).ToList();
            var geleit = stand.Where(s => s.Ort == Wachort.Geleit).ToList();
            var daheim = stand.Where(s => s.Ort == Wachort.Daheim).ToList();
            Zeile("Auf Streife", streife.Count == 0 ? "niemand" : Nummern(streife), streife.Count == 0 ? Grau : Gruen);
            Zeile("Beim Geleit", geleit.Count == 0 ? "niemand" : $"{Nummern(geleit)} (je Karren einer)", geleit.Count == 0 ? Grau : Gruen);
            foreach (var gruppe in daheim.GroupBy(s => s.Taetigkeit).OrderBy(g => g.Key))
                Zeile(Taetigkeit(gruppe.Key), Nummern(gruppe), Hell);
        }

        var gefangene = w.Wache.Gefangene(b);
        Inhalt.AddChild(new HSeparator());
        Tipp(Text(Inhalt, "Arrestzelle", 16, Gold), "Hier sitzen gefasste Taschendiebe bis zum nächsten Gerichtstag.");
        if (gefangene.Count == 0)
            Text(Inhalt, "Niemand sitzt in der Zelle. Gefasste Taschendiebe kommen hierher, solange das Gericht noch nicht getagt hat.", 13, Grau, 600);
        foreach (var g in gefangene)
            Text(Inhalt, $"Taschendieb im Dienst von {g.Herkunft.Inhaber} – griff {g.Betroffener} in die Tasche ({Zahlenformat.Ganz(g.Betrag)} {w.Daten.Allgemein.Waehrung})", 13, Hell, 600);

        Inhalt.AddChild(new HSeparator());
        foreach (var zeile in BetriebsVerwaltung.WacheZeilen(b, w))
            Text(Inhalt, zeile, 12, Grau, 600);
    }

    private static string Taetigkeit(Wachtaetigkeit t) => t switch
    {
        Wachtaetigkeit.Putzen => "Putzt Rüstung",
        Wachtaetigkeit.Fechten => "Übt das Fechten",
        Wachtaetigkeit.Wuerfeln => "Würfelt",
        Wachtaetigkeit.Schaerfen => "Schärft Klingen",
        Wachtaetigkeit.Wachbuch => "Führt das Wachbuch",
        Wachtaetigkeit.Schlafen => "Schläft",
        Wachtaetigkeit.Befehligen => "Befehligt",
        _ => t.ToString(),
    };
}
