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
    public PersonalAnzeige() : base(900) { }

    protected override string Stand(Stadtwirtschaft w, Betrieb b)
    {
        var s = w.Werkstaetten;
        return $"{b.Belegt}/{b.Plaetze}|{b.Lohnstufe}|{s.PraemieVerboten(b)}|{w.Personal.EinstellenVerboten(b, w.Personal.Anfaenger(b) ?? "")}|{string.Join(",", Auswahl())}|" + string.Join(";",
            s.Arbeiterliste(b).Select(z => $"{z.Nummer}:{z.Name}:{z.Arbeitskraft}:{z.Ware}:{(int)z.Laune}:{(int)(z.Dienstjahre * 10)}:{(int)(z.Erfahrung * 20)}:{z.FaelltAusBis}:{z.BefoerderungVerboten}:{z.Eigenschaft?.Kennung}:{z.EigenschaftWahl?.Count}:{(int)z.Lohn}:{s.PraemieVerboten(b, z.Nummer)}"));
    }

    /// <summary>Ältere Stufen mit eigener Schriftfarbe: Anfänger grau, Mittelstufe hell, Vorarbeiter golden.</summary>
    private static Color StufenFarbe(int stufe) => stufe >= 3 ? Gold : stufe == 2 ? Hell : Grau;

    protected override void Aufbauen(Stadtwirtschaft w, Betrieb b)
    {
        var s = w.Werkstaetten;
        var waehrung = w.Daten.Allgemein.Waehrung;
        var jetzt = Sim!.Zeit.VergangeneMinuten;
        var gewaehlt = Auswahl();
        Kopf("Personalbuch", b, $"{b.Belegt} von {b.Plaetze} Plätzen · Lohnkosten {Zahlenformat.Ganz(b.Lohnkosten)} {waehrung} im Jahr · Lohn {b.Lohnstufe.Name()}");

        var liste = s.Arbeiterliste(b);
        if (liste.Count == 0) Text(Inhalt, "In der Werkstatt arbeitet niemand. Stellt unten jemanden ein.", 14, Grau);
        foreach (var z in liste)
        {
            var gewaehlteZeile = gewaehlt.Contains(z.Nummer);
            var karte = new HBoxContainer();
            karte.AddThemeConstantOverride("separation", 12);
            Inhalt.AddChild(karte);

            var bild = PortraetAnzeige.Neu(72, lebendig: false);
            karte.AddChild(bild);
            bild.Zeigen(z.Volk, z.Geschlecht, z.Aussehen, Reichsgilde.Core.Dynastie.Aussehenspalette.AltersstufeAus(z.Menschenjahre),
                Reichsgilde.Core.Dynastie.Kleidung.Schlicht, Reichsgilde.Core.Dynastie.Standgruppe.Gemeiner);

            var mitte = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0) };
            mitte.AddThemeConstantOverride("separation", 2);
            karte.AddChild(mitte);
            var name = z.Name == "" ? $"{z.Arbeitskraft} Nr. {z.Nummer}" : z.Name;
            var kopf = Reihe(mitte);
            Text(kopf, (gewaehlteZeile ? "» " : "") + name, 16, gewaehlteZeile ? Gold : Hell);
            Text(kopf, $"{z.Arbeitskraft} · Nr. {z.Nummer}", 13, StufenFarbe(z.Stufe));
            Text(mitte, $"{z.Volk.Einzahl()} · {Zahlenformat.Ganz(z.Menschenjahre)} Jahre · arbeitet an: {z.Ware}{(z.Zugeteilt ? "" : " (Betrieb)")}", 12, Grau);

            // Erfahrung bis zur nächsten Stufe
            if (z.ErfahrungNaechste is { } ziel && z.NaechsteStufe is { } naechste)
            {
                var erf = Reihe(mitte);
                Balken(erf, Math.Clamp(z.Erfahrung / Math.Max(ziel, 0.01), 0, 1), 170, Gruen);
                var rest = z.JahreBisNaechste is { } j ? (j <= 0 ? "genug für die Beförderung" : $"noch etwa {Zahlenformat.Kommazahl(j)} Jahre") : "sammelt hier keine Erfahrung";
                Text(erf, $"Erfahrung {Zahlenformat.Ganz(z.Erfahrung)} von {Zahlenformat.Ganz(ziel)} Arbeitsstunden zum {naechste} · {rest}", 12, Grau);
            }
            else Text(mitte, "Höchste Stufe erreicht.", 12, Grau);

            if (z.Eigenschaft is { } e) Tipp(Text(mitte, $"{e.Name} ({e.Gruppe}): {e.Wirkung}", 12, Gold, 430), $"{e.Name}\n{e.Gruppe}\n{e.Wirkung}");
            var zustand = z.FaelltAusBis is { } bis
                ? $"fehlt noch {Zahlenformat.Kommazahl((bis - jetzt) / (double)Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag)} Tage" : "da";
            Text(mitte, $"Laune {Zahlenformat.Ganz(z.Laune)} · Tempo {Zahlenformat.Ganz(z.TempoFaktor * 100)} % · Dienst {Zahlenformat.Kommazahl(z.Dienstjahre)} J. · {zustand}", 12,
                z.FaelltAusBis is not null || z.Laune < 35 ? Rot : Grau);

            var rechts = new VBoxContainer();
            rechts.AddThemeConstantOverride("separation", 4);
            karte.AddChild(rechts);
            Text(rechts, $"Lohn {Zahlenformat.Ganz(z.Lohn)} {waehrung}/Jahr", 13, Hell);
            var befoerdern = z.NaechsteStufe is { } n ? $"Befördern → {n}" : "Befördern";
            var knopfTipp = z.BefoerderungVerboten
                ?? (z.BriefKosten is { } kosten ? $"Beförderung zum {z.NaechsteStufe}: der Brief kostet {Zahlenformat.Ganz(kosten)} {waehrung}." : null);
            var bk = Knopf(rechts, befoerdern, () => Befehlen(new ArbeiterBefoerdern(b.Name, z.Nummer)), z.BefoerderungVerboten);
            if (z.BefoerderungVerboten is null && knopfTipp is not null) Tipp(bk, knopfTipp);
            Knopf(rechts, $"Prämie {Zahlenformat.Ganz(s.PraemieKosten(b, z.Nummer))}", () => Befehlen(new PraemieZahlen(b.Name, z.Nummer)), s.PraemieVerboten(b, z.Nummer));
            Knopf(rechts, "Entlassen", () => Befehlen(new ArbeiterEntlassen(b.Name, z.Nummer)));

            Tipp(mitte, $"{name}\n{z.Arbeitskraft} Nr. {z.Nummer} · {z.Volk.Einzahl()}\nGrundlohn {Zahlenformat.Ganz(w.Daten.Lohn(z.Arbeitskraft, b.Art))} {waehrung} (Lohnstufe {b.Lohnstufe.Name()})\nLohn im Jahr: {Zahlenformat.Ganz(z.Lohn)} {waehrung} (mit Schicht, Lohnstufe und Einrichtung)");

            // Beförderung zum Vorarbeiter: eine Eigenschaft aus drei Karten wählen
            if (z.EigenschaftWahl is { Count: > 0 } wahl && z.Eigenschaft is null)
            {
                Text(Inhalt, $"{name} ist Vorarbeiter. Wählt seine Eigenschaft – sie gilt für immer:", 14, Gold);
                var karten = Reihe(Inhalt);
                foreach (var ek in wahl)
                {
                    var kk = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
                    kk.AddThemeConstantOverride("separation", 3);
                    karten.AddChild(kk);
                    var verboten = w.Personal.EigenschaftWaehlenVerboten(b, z.Nummer, ek.Kennung);
                    var wk = Knopf(kk, $"{ek.Name} ({ek.Gruppe})", () => Befehlen(new EigenschaftWaehlen(b.Name, z.Nummer, ek.Kennung)), verboten, 14);
                    Tipp(wk, verboten ?? $"{ek.Name}: {ek.Wirkung}");
                    Text(kk, ek.Wirkung, 12, Hell, 250);
                }
            }
            Inhalt.AddChild(new HSeparator());
        }
        Text(Inhalt, "Einstellen", 16, Gold);
        var einstellen = Reihe(Inhalt);
        var anfaenger = w.Personal.Anfaenger(b);
        if (anfaenger is null) Text(Inhalt, "Hier gibt es kein Personal zum Einstellen.", 12, Grau);
        else
        {
            Knopf(einstellen, $"{anfaenger} einstellen · Lohn {Zahlenformat.Ganz(w.Daten.Lohn(anfaenger, b.Art))}", () => Befehlen(new ArbeiterEinstellen(b.Name, anfaenger)),
                w.Personal.EinstellenVerboten(b, anfaenger));
            Text(einstellen, "Man stellt nur Anfänger ein; alle anderen steigen durch Erfahrung und Beförderung auf.", 12, Grau, 500);
        }

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
