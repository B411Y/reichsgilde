using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Das Personalbuch (Innenraum der Werkstatt, PR #152): alle Arbeiter mit Art, Ware, Laune, Tempo, Dienstjahren und Ausfall; Einstellen,
/// Entlassen, Befördern, Lohnstufe (knapp, üblich, gut) und Prämie (an alle oder je Arbeiter) mit Kosten. Wo ein Knopf gesperrt ist,
/// nennt sein Hinweis den Grund aus dem Kern (<see cref="Werkstattwesen.BefoerdernVerboten"/> und Verwandte), dazu steht er als Hinweis unter der Liste.
/// </summary>
public sealed class PersonalAnzeige : InnenTafel
{
    public PersonalAnzeige() : base(860) { }

    protected override string Stand(Stadtwirtschaft w, Betrieb b)
    {
        var s = w.Werkstaetten;
        return $"{b.Belegt}/{b.Plaetze}|{b.Lohnstufe}|{s.PraemieVerboten(b)}|{string.Join(",", Auswahl())}|" + string.Join(";",
            s.Arbeiterliste(b).Select(z => $"{z.Nummer}:{z.Arbeitskraft}:{z.Ware}:{(int)z.Laune}:{(int)(z.Dienstjahre * 10)}:{z.FaelltAusBis}:{z.BefoerderungVerboten}:{s.PraemieVerboten(b, z.Nummer)}"));
    }

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        var s = w.Werkstaetten;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var jetzt = Sim!.Zeit.VergangeneMinuten;
        var gewaehlt = Auswahl();
        Kopf("Personalbuch", b, $"{b.Belegt} von {b.Plaetze} Plätzen · Lohnkosten {Zahlenformat.Ganz(b.Lohnkosten)} {waehrung} im Jahr · Lohn {b.Lohnstufe.Name()}");

        if (b.Arbeiter.Count == 0) Text(Inhalt, "In der Werkstatt arbeitet niemand. Stellt unten jemanden ein.", 14, Grau);
        else
        {
            var raster = Raster(9);
            foreach (var kopf in new[] { "Arbeiter", "Arbeitet an", "Laune", "Tempo", "Dienst", "Zustand", "", "", "" })
                Zelle(raster, kopf, 12, Grau);
            foreach (var z in s.Arbeiterliste(b))
            {
                var gewaehlteZeile = gewaehlt.Contains(z.Nummer);
                Zelle(raster, (gewaehlteZeile ? "» " : "") + $"{z.Arbeitskraft} Nr. {z.Nummer}", 14, gewaehlteZeile ? Gold : Hell, 130);
                Zelle(raster, z.Ware + (z.Zugeteilt ? "" : " (Betrieb)"), 13, z.Zugeteilt ? Hell : Grau, 130);
                Zelle(raster, Zahlenformat.Ganz(z.Laune), 14, z.Laune < 35 ? Rot : z.Laune > 65 ? Gruen : Hell, 40, rechts: true);
                Zelle(raster, $"{Zahlenformat.Ganz(z.TempoFaktor * 100)} %", 14, Hell, 54, rechts: true);
                Zelle(raster, $"{Zahlenformat.Kommazahl(z.Dienstjahre)} J.", 13, Grau, 56, rechts: true);
                Zelle(raster, z.FaelltAusBis is { } bis
                    ? $"fehlt noch {Zahlenformat.Kommazahl((bis - jetzt) / (double)Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag)} Tage" : "da", 13,
                    z.FaelltAusBis is null ? Grau : Rot, 120);
                Knopf(raster, $"Prämie {Zahlenformat.Ganz(s.PraemieKosten(b, z.Nummer))}", () => Befehlen(new PraemieZahlen(b.Name, z.Nummer)),
                    s.PraemieVerboten(b, z.Nummer));
                Knopf(raster, "Befördern", () => Befehlen(new ArbeiterBefoerdern(b.Name, z.Nummer)), z.BefoerderungVerboten);
                Knopf(raster, "Entlassen", () => Befehlen(new ArbeiterEntlassen(b.Name, z.Nummer)));
            }
            // Gründe für gesperrte Beförderungen stehen auch hier – ein Hinweis am Knopf allein sieht man leicht nicht
            foreach (var grund in s.Arbeiterliste(b).Where(z => z.Arbeitskraft == s.Daten?.Befoerderung.Von && z.BefoerderungVerboten is not null)
                         .Select(z => $"Nr. {z.Nummer}: {z.BefoerderungVerboten}").Distinct())
                Text(Inhalt, grund, 12, Grau, 820);
        }

        Inhalt.AddChild(new HSeparator());
        Text(Inhalt, "Einstellen", 16, Gold);
        var einstellen = Reihe(Inhalt);
        var voll = b.Belegt >= b.Plaetze;
        foreach (var (kraft, daten) in w.Daten.Arbeitskraefte.Where(k => k.Value.IstErlaubt(w.Statut, b.Ort)))
            Knopf(einstellen, $"{kraft} einstellen · Lohn {Zahlenformat.Ganz(daten.Kosten)}", () => Befehlen(new ArbeiterEinstellen(b.Name, kraft)),
                voll ? $"Alle {b.Plaetze} Plätze sind besetzt." : null);
        if (voll) Text(Inhalt, $"Alle {b.Plaetze} Plätze sind besetzt – ein Ausbau der Werkstatt schafft mehr.", 12, Grau);

        Inhalt.AddChild(new HSeparator());
        Text(Inhalt, "Lohn und Prämie", 16, Gold);
        var lohn = Reihe(Inhalt);
        Zelle(lohn, "Lohnstufe:", 14);
        var lohnVerboten = s.LohnstufeVerboten(b);
        foreach (var stufe in new[] { Lohnstufe.Knapp, Lohnstufe.Ueblich, Lohnstufe.Gut })
        {
            var d = s.Lohnstufe(stufe);
            var knopf = Knopf(lohn, $"{stufe.Name()} · Lohn ×{Zahlenformat.Kommazahl(d.LohnFaktor)} · Laune {Zahlenformat.MitVorzeichen(d.Laune)}",
                () => Befehlen(new LohnstufeSetzen(b.Name, stufe)), lohnVerboten);
            knopf.ToggleMode = true;
            knopf.ButtonPressed = b.Lohnstufe == stufe;
        }
        var praemie = Reihe(Inhalt);
        Knopf(praemie, $"Prämie an alle · {Zahlenformat.Ganz(s.PraemieKosten(b))} {waehrung}", () => Befehlen(new PraemieZahlen(b.Name)), s.PraemieVerboten(b));
        Text(praemie, $"Eine Prämie hebt die Laune eine Weile (um {Zahlenformat.Ganz(s.Daten?.Praemie.Laune ?? 0)}); Arbeiter mit schlechter Laune arbeiten langsamer und kündigen irgendwann.", 12, Grau, 540);
    }
}
