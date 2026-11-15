using Reichsgilde;

/// <summary>Zielform je Sitzhaltung (Rock folgt dem Knie, Nutzer 15.11.2026): FigurenBaukasten.SitzForm und SitzFormen.</summary>
public static class FigurenBaukastenSitzformTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Figuren-Baukasten: je Sitzhaltung eine eigene Zielform (Sitzen, Sitzen_<Clip>), sonst keine; alle Namen in SitzFormen", () =>
        {
            Pruefe.Gleich("Sitzen", FigurenBaukasten.SitzForm("Sitzen"));
            Pruefe.Gleich("Sitzen_Schreiben", FigurenBaukasten.SitzForm("Schreiben"));
            Pruefe.Gleich("Sitzen_Kutschieren", FigurenBaukasten.SitzForm("Kutschieren"));
            foreach (var klip in FigurenBaukasten.SitzendeArbeitsclips)
                Pruefe.Wahr(FigurenBaukasten.SitzFormen.Contains(FigurenBaukasten.SitzForm(klip)!), klip);
            Pruefe.Gleich(9, FigurenBaukasten.SitzFormen.Count);
            // Gegenprobe: stehende Clips und SitzenBock haben keine Zielform
            foreach (var klip in new[] { "Stehen", "Gehen", "Haemmern", "Hueten", "SitzenBock", "" })
                Pruefe.Wahr(FigurenBaukasten.SitzForm(klip) is null, klip);
        }),
    };
}
