using Reichsgilde;

/// <summary>Briefe warten während Ratssitzung und Stimmrunde (<c>game/scripts/Briefaufschub.cs</c>).</summary>
static class BriefaufschubTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Briefaufschub: ohne offene Sitzung kommt ein Brief sofort", () =>
        {
            var a = new Briefaufschub<string>();
            Pruefe.Wahr(!a.Zurueckstellen("Zunft", ruht: false), "sofort");
            Pruefe.Gleich(0, a.Wartend);
            Pruefe.Gleich(0, a.Freigeben(false).Count);
        }),
        ("Briefaufschub: während der Sitzung warten Briefe, danach kommen alle in ihrer Reihenfolge, keiner geht verloren", () =>
        {
            var a = new Briefaufschub<string>();
            Pruefe.Wahr(a.Zurueckstellen("Zunft", ruht: true), "wartet");
            Pruefe.Wahr(a.Zurueckstellen("Kontor", ruht: true), "wartet");
            Pruefe.Gleich(0, a.Freigeben(true).Count);              // Gegenprobe: solange die Tafel offen ist, kommt keiner
            Pruefe.Gleich(2, a.Wartend);
            var frei = a.Freigeben(false);
            Pruefe.Gleich("Zunft,Kontor", string.Join(",", frei));
            Pruefe.Gleich(0, a.Wartend);
        }),
        ("Briefphase: Briefe warten die ganze Folge Stimmrunde → Ratssitzung → Jahresbericht ab, auch zwischen den Tafeln; Gegenprobe: danach nicht", () =>
        {
            Pruefe.Wahr(Briefphase.Ruht(true, true, false, false, false, false), "Stimmrunde offen");
            Pruefe.Wahr(Briefphase.Ruht(false, true, false, false, false, false), "zwischen Stimmrunde und Sitzung (Klicktest 4: dort kam der Brief)");
            Pruefe.Wahr(Briefphase.Ruht(false, false, true, false, false, false), "Sitzung anstehend");
            Pruefe.Wahr(Briefphase.Ruht(false, false, false, true, false, true), "Sitzung offen, Bericht wartet");
            Pruefe.Wahr(Briefphase.Ruht(false, false, false, false, false, true), "Bericht wartet");
            Pruefe.Wahr(Briefphase.Ruht(false, false, false, false, true, false), "Jahresbericht offen");
            Pruefe.Wahr(!Briefphase.Ruht(false, false, false, false, false, false), "danach: Briefe kommen");
            // Ablauf: Brief kommt zwischen Stimmrunde und Sitzung, wartet über Sitzung und Bericht, erscheint danach
            var a = new Briefaufschub<string>();
            Pruefe.Wahr(a.Zurueckstellen("Brief der Zunft", Briefphase.Ruht(false, true, false, false, false, false)), "wartet");
            Pruefe.Gleich(0, a.Freigeben(Briefphase.Ruht(false, false, false, true, false, false)).Count);
            Pruefe.Gleich(0, a.Freigeben(Briefphase.Ruht(false, false, false, false, true, false)).Count);
            Pruefe.Gleich("Brief der Zunft", string.Join(",", a.Freigeben(Briefphase.Ruht(false, false, false, false, false, false))));
        }),
        ("Briefaufschub: ein Brief direkt nach der Sitzung reiht sich hinter die wartenden", () =>
        {
            var a = new Briefaufschub<string>();
            a.Zurueckstellen("alt", ruht: true);
            Pruefe.Wahr(a.Zurueckstellen("neu", ruht: false), "hinter den alten");
            Pruefe.Gleich("alt,neu", string.Join(",", a.Freigeben(false)));
        }),
    };
}
