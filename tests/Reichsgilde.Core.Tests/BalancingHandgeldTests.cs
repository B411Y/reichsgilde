using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Balancing nach dem Handgeld (Datenversion 75, 9.11.2026; WIRTSCHAFTSMODELL „Balancing nach dem Handgeld“)
//
//  Dieb: Taschendiebe werden seltener gefasst (0,012 → 0,009 je Stunde) – jeder Fang kostet seit dem Handgeld (#371) einen Ersatz mit 100 Gulden
//  und bringt eine Spur vor Gericht. Parfumeur: die Waren der Duftwerkstatt etwas billiger (Moschusduft 226 → 220, Fechterduft 215 → 210).
// ═════════════════════════════════════════════════════════════════════════════

static class BalancingHandgeldTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Balancing nach dem Handgeld: Daten – Taschendiebe 0,009 je Stunde gefasst, Moschusduft 220, Fechterduft 210 (Datenversion 75); " +
         "Datenpfad.WieVorBalancingHandgeld gibt die alten Werte", () =>
        {
            var d = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            Pruefe.Gleich(75, d.Version);
            Pruefe.Ungefaehr(0.009, d.Dieb!.Taschendiebstahl.FangJeStunde, 1e-12);
            Pruefe.Gleich(220.0, d.Waren["Moschusduft"].Grundpreis);
            Pruefe.Gleich(210.0, d.Waren["Fechterduft"].Grundpreis);
            // unverändert: Beute und Fang je Stufe, die Blume der Zwietracht (Stufe 1) und der Heiligenduft (Stufe 3)
            Pruefe.Ungefaehr(1.16, d.Dieb.Taschendiebstahl.BeuteJeMinute, 1e-12);
            Pruefe.Gleich("1 0,8 0,6", string.Join(" ", d.Dieb.Taschendiebstahl.FangJeStufe.Select(x => x.ToString(System.Globalization.CultureInfo.GetCultureInfo("de-DE")))));
            Pruefe.Gleich(124.0, d.Waren["Blume der Zwietracht"].Grundpreis);
            Pruefe.Gleich(384.0, d.Waren["Heiligenduft"].Grundpreis);

            var alt = Wirtschaftsdaten.AusJson(Datenpfad.WieVorBalancingHandgeld());
            Pruefe.Ungefaehr(0.012, alt.Dieb!.Taschendiebstahl.FangJeStunde, 1e-12);
            Pruefe.Gleich(226.0, alt.Waren["Moschusduft"].Grundpreis);
            Pruefe.Gleich(215.0, alt.Waren["Fechterduft"].Grundpreis);
        }),
    };
}
