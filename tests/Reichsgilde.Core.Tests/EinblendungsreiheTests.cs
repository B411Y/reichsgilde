using Reichsgilde;

/// <summary>
/// Tests für die Reihe der zentralen Einblendung (<c>game/scripts/Einblendungsreihe.cs</c>): Hinter großen Fenstern und Briefen ruht sie unsichtbar,
/// eine laufende hält an und geht danach weiter, keine Meldung geht verloren; ohne offenes Fenster läuft alles wie vorher.
/// </summary>
static class EinblendungsreiheTests
{
    private sealed class M
    {
        public string Name = "";
        public override string ToString() => Name;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Einblendung: Gegenprobe ohne offenes Fenster – nacheinander, sichtbar, nichts hält an", () =>
        {
            var r = new Einblendungsreihe<M>();
            M a = new() { Name = "a" }, b = new() { Name = "b" };
            Pruefe.Wahr(r.Einreihen(a, 4) && r.Einreihen(b, 4), "Einreihen");
            Pruefe.Gleich(a, r.Beginnen());
            Pruefe.Gleich(null, r.Beginnen()); // eine läuft schon
            Pruefe.Wahr(r.Sichtbar && !r.Angehalten, "Ohne Fenster sichtbar und nicht angehalten");
            r.Fertig(a);
            Pruefe.Gleich(b, r.Beginnen());
        }),

        ("Einblendung: Fenster geht über einer laufenden auf – unsichtbar und angehalten, danach geht sie weiter", () =>
        {
            var r = new Einblendungsreihe<M>();
            M a = new() { Name = "a" }, b = new() { Name = "b" };
            r.Einreihen(a, 4);
            r.Einreihen(b, 4);
            r.Beginnen();
            Pruefe.Wahr(r.RuhtSetzen(true), "Wechsel beim Öffnen");
            Pruefe.Wahr(!r.Sichtbar && r.Angehalten, "Fenster offen: unsichtbar, die laufende hält an");
            Pruefe.Gleich(a, r.Laufend); // nicht verloren
            Pruefe.Wahr(!r.RuhtSetzen(true), "Kein zweiter Wechsel");
            Pruefe.Wahr(r.RuhtSetzen(false), "Wechsel beim Schließen");
            Pruefe.Wahr(r.Sichtbar && !r.Angehalten, "Danach wieder sichtbar und läuft weiter");
            Pruefe.Gleich(a, r.Laufend);
            r.Fertig(a);
            Pruefe.Gleich(b, r.Beginnen());
        }),

        ("Einblendung: Brief oder Bericht offen – keine neue beginnt, die wartende kommt erst danach", () =>
        {
            var r = new Einblendungsreihe<M>();
            var a = new M { Name = "a" };
            r.RuhtSetzen(true);
            r.Einreihen(a, 4);
            Pruefe.Gleich(null, r.Beginnen());
            Pruefe.Gleich(1, r.Wartend);
            Pruefe.Wahr(!r.Sichtbar, "unsichtbar, solange das Fenster offen ist");
            r.RuhtSetzen(false);
            Pruefe.Gleich(a, r.Beginnen());
            Pruefe.Gleich(0, r.Wartend);
        }),

        ("Einblendung: Flut – über der Grenze kommt die Meldung ohne Einblendung gleich in die Ecke", () =>
        {
            var r = new Einblendungsreihe<M>();
            r.RuhtSetzen(true);
            for (var i = 0; i < 4; i++) Pruefe.Wahr(r.Einreihen(new M(), 4), "unter der Grenze");
            Pruefe.Wahr(!r.Einreihen(new M(), 4), "die fünfte geht direkt in die Ecke");
            Pruefe.Gleich(4, r.Wartend);
        }),
    };
}
