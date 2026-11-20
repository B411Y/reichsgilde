using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// <b>Die Charakterseite</b> (UI-Umbau „Zunftlade“, Teil 4; Entwurf 3) – ein Teil der <c>partial class CharakterAnzeige</c>: das Fenster des
/// eigenen Oberhaupts, das ein Klick auf das Porträt der Lade öffnet. Es führt zusammen, was sonst an vielen Stellen steht: großes Porträt,
/// Stand und Ansehen, Gulden mit dem Gewinn des Jahres, Aktionspunkte, Stress und Amt; daneben die Talente mit „Üben“, darunter Charakterzüge
/// und Familie. Reiter: Überblick, Lebensweg (derselbe Baum wie bisher), Stammbaum und Chronik (das Familienbuch).
///
/// Feste Größe wie die Lade-Fenster (Maße „LadeFensterMasse“), Aussehen aus dem Thema der Fenster („Charakter…“, „FensterZeile…“). Gelesen wird
/// nur der Kern; „Üben“ ist der gewöhnliche Befehl <see cref="TalentTrainieren"/>. Andere Personen behalten das bisherige Charakterfenster.
/// </summary>
public partial class CharakterAnzeige
{
    /// <summary>Stammbaum (true) oder Chronik (false) des eigenen Hauses im Familienbuch öffnen – die Hauptszene kennt das Familienbuch.</summary>
    public event Action<bool>? FamilienbuchGewuenscht;

    /// <summary>Ist das die Person, für die die Charakterseite gilt: das lebende Oberhaupt des eigenen Hauses?</summary>
    private bool IstEigenesOberhaupt(Stadtwirtschaft w, Person p) => _spieler is not null && p.Lebt && w.Familien.Von(_spieler)?.Oberhaupt == p;

    /// <summary>Feste Größe für die Charakterseite, sonst die Breite des gewöhnlichen Charakterfensters.</summary>
    private void SeitenmassSetzen(bool seite)
    {
        if (!seite)
        {
            _inhalt.CustomMinimumSize = Vector2.Zero;
            return;
        }
        var bild = GetViewportRect().Size;
        var stil = FensterStil.Thema.GetStylebox("panel", "FensterTafelKopf").GetMinimumSize();
        var rand = 2 * FensterStil.Mass("rand", "CharakterMasse");
        _inhalt.CustomMinimumSize = new Vector2(
            Mathf.Min(FensterStil.Mass("breite", "LadeFensterMasse"), bild.X) - stil.X - rand,
            FensterStil.Mass("hoehe", "LadeFensterMasse") - stil.Y - rand);
    }

    private static Label Schrift(Node eltern, string text, string variante, bool dehnen = false)
    {
        var label = new Label { Text = text, ThemeTypeVariation = variante, MouseFilter = MouseFilterEnum.Ignore };
        if (dehnen) label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        eltern.AddChild(label);
        return label;
    }

    private static T Kasten<T>(Node eltern, string variante) where T : Container, new()
    {
        var k = new T { ThemeTypeVariation = variante };
        eltern.AddChild(k);
        return k;
    }

    /// <summary>Die Reiter der Charakterseite (auch über dem Lebensweg): Überblick, Lebensweg, Stammbaum, Chronik.</summary>
    private void SeitenReiter(Familienbuch buch, Person p)
    {
        var leiste = new TabBar { FocusMode = FocusModeEnum.None, ClipTabs = false, ThemeTypeVariation = "CharakterReiter" };
        leiste.AddTab("Überblick");
        var frei = buch.HatLebensweg(p) ? FreiePunkte(buch, p) : 0;
        leiste.AddTab(frei <= 0 ? "Lebensweg" : frei == 1 ? "Lebensweg · 1 Punkt frei" : $"Lebensweg · {frei} Punkte frei");
        leiste.SetTabDisabled(1, !buch.HatLebensweg(p));
        leiste.AddTab("Stammbaum");
        leiste.AddTab("Chronik");
        leiste.CurrentTab = _lebensweg ? 1 : 0;
        leiste.TabChanged += nummer =>
        {
            switch (nummer)
            {
                case 0 or 1:
                    if (_lebensweg == (nummer == 1)) return;
                    _lebensweg = nummer == 1;
                    Callable.From(Aufbauen).CallDeferred(); // nicht mitten im Signal der Leiste, die dabei entfernt wird
                    break;
                default:
                    FamilienbuchGewuenscht?.Invoke(nummer == 2);
                    break;
            }
        };
        _inhalt.AddChild(leiste);
    }

    /// <summary>Der Reiter „Überblick“: links das Porträt, in der Mitte die Werte und die Züge, rechts Talente und Familie.</summary>
    private void SeiteBauen(Stadtwirtschaft w, Person p)
    {
        var buch = w.Familien;
        var haus = buch.Von(_spieler!)!;
        var konto = _spieler!;
        var waehrung = w.Daten.Allgemein.Waehrung;
        SeitenReiter(buch, p);
        if (_lebensweg && buch.HatLebensweg(p)) // Reiter „Lebensweg“: derselbe Baum wie im gewöhnlichen Charakterfenster
        {
            LebenswegBauen(w, p);
            return;
        }
        _lebensweg = false;
        if (_meldung != "") Schrift(_inhalt, _meldung, _meldungGut ? "CharakterGut" : "FensterZeileWarnung");

        var spalten = Kasten<HBoxContainer>(_inhalt, "CharakterSpalten");
        spalten.SizeFlagsVertical = SizeFlags.ExpandFill;

        // ── Links: großes Porträt ──
        var bild = PortraetAnzeige.Neu(FensterStil.Mass("bild", "CharakterMasse"));
        bild.Zeigen(buch, p);
        bild.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        spalten.AddChild(bild);

        // ── Mitte: wer, dann die Werte des Hauses, darunter die Züge ──
        var mitte = Kasten<VBoxContainer>(spalten, "CharakterSpalte");
        mitte.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        mitte.SizeFlagsStretchRatio = 1f;
        var wer = Kasten<HFlowContainer>(mitte, "CharakterWer");
        Schrift(wer, $"{p.OberhauptTitel} im Haus {haus.Nachname}", "FensterZeileWert");
        Schrift(wer, $"· {p.Volk.Einzahl()} · {p.Alter(buch.Jahr)} Jahre" + (w.Glaube.IstAktiv ? $" · {p.Glaube.Name()}" : ""), "FensterVor");
        if (p.Krankheit is { } krank) Schrift(wer, $"· krank: {krank.Name}", "FensterZeileWarnung");

        var werte = Kasten<VBoxContainer>(mitte, "FensterZeilen");
        var gewinn = konto.Laufend.Gewinn;
        Wertzeile(werte, "Stand", w.StandVon(konto).Name, $"Ansehen {Zahlenformat.Ganz(w.Rat.Ansehen(konto))}", WohnhausAnzeige.StandText(w, konto));
        Wertzeile(werte, waehrung, Zahlenformat.Ganz(konto.Vermoegen), $"dieses Jahr {Zahlenformat.MitVorzeichen(gewinn)}", WohnhausAnzeige.VermoegenTeile(konto, waehrung),
            warnung: konto.Reinvermoegen < 0);
        if (w.HatAktionspunkte && w.Daten.Aktionspunkte is { } ap)
            Wertzeile(werte, "AP", $"{konto.Aktionspunkte.Vorrat}/{ap.Hoechstens}", $"+{w.AktionspunkteJeRunde + w.ApBonus(konto)} zum Jahreswechsel", WohnhausAnzeige.ApTipp);
        if (buch.StressDaten is { } stress && buch.HatStress(p))
        {
            var stufe = buch.StressStufe(p);
            var zeile = Wertzeile(werte, "Stress", $"{Zahlenformat.Ganz(buch.Stress(p))} · {buch.StressStufenname(stufe)}", "", StressTipp(buch, stress, p), warnung: stufe > 1);
            zeile.AddChild(new StressBalken(buch.Stress(p), stress.Hoechstwert, stress.Stufen.Select(s => s.Ab).ToArray(), StressFarbe(stufe)) { SizeFlagsVertical = SizeFlags.ShrinkCenter });
        }
        if (w.Rat.IstAktiv)
            Wertzeile(werte, "Amt", w.Rat.AmtVon(konto)?.Name ?? "keines", "Wahl am Jahresende", "Ämter, Bewerbung und Wahl stehen in der Ratskammer (Fach „Rat“).");

        Schrift(mitte, "Charakterzüge", "CharakterAbschnitt");
        if (!buch.HatZuege) Schrift(mitte, "In diesem Spielstand gibt es keine Charakterzüge.", "FensterVor");
        else if (p.Zuege.Count == 0) Schrift(mitte, "Keine besonderen Züge.", "FensterVor");
        else
        {
            var zuege = Kasten<HFlowContainer>(mitte, "CharakterZuege");
            foreach (var z in p.Zuege) zuege.AddChild(new ZugIcon(z, ZugTipp(w, z)));
        }

        // ── Rechts: Talente mit „Üben“, darunter die Familie ──
        var rechts = Kasten<VBoxContainer>(spalten, "CharakterSpalte");
        rechts.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rechts.SizeFlagsStretchRatio = 1.1f;
        Schrift(rechts, "Talente", "CharakterAbschnitt");
        var talente = Kasten<VBoxContainer>(rechts, "FensterZeilen");
        var schritt = TalentTexte.Sterne(w.Daten.Aktionspunkte?.TrainingSchritt ?? 0.5);
        foreach (var t in TalentTexte.Alle) TalentMitUeben(talente, w, p, t, schritt);

        Schrift(rechts, "Familie", "CharakterAbschnitt");
        var v = buch.Verwandte(p);
        var leute = new List<(Person Person, string Rolle)>();
        if (v.Ehepartner is { } partner) leute.Add((partner, partner.Ehepartnertitel));
        foreach (var kind in v.Kinder.Where(k => k.Lebt)) leute.Add((kind, kind.SohnOderTochter));
        if (leute.Count == 0) Schrift(rechts, "ledig, keine Kinder", "FensterVor");
        var familie = Kasten<HFlowContainer>(rechts, "CharakterFamilie");
        foreach (var (person, rolle) in leute.Take(6))
        {
            var eintrag = Kasten<HBoxContainer>(familie, "FensterZeileReihe");
            var klein = PortraetAnzeige.Neu(FensterStil.Mass("bild_klein", "CharakterMasse"));
            klein.Zeigen(buch, person);
            eintrag.AddChild(klein);
            var text = Kasten<VBoxContainer>(eintrag, "FensterBefehlSpalte");
            Schrift(text, $"{person.Vorname} · {rolle}", "FensterZeileWert");
            var geisel = w.Diebe.IstGeisel(person);
            Schrift(text, geisel ? "entführt" : person.Krankheit is { } k ? $"krank: {k.Name}" : buch.Verfassung(person), geisel || person.Krankheit is not null ? "FensterZeileWarnung" : "FensterVor");
        }
    }

    /// <summary>Eine Zeile „Name · Wert · Zusatz“ der Wertetafel (wie im Entwurf), mit Hinweis; gibt die Reihe zurück, um etwas anzuhängen.</summary>
    private static HBoxContainer Wertzeile(Container eltern, string name, string wert, string zusatz, string tipp, bool warnung = false)
    {
        var kasten = new PanelContainer { ThemeTypeVariation = "FensterZeile", TooltipText = tipp, MouseFilter = MouseFilterEnum.Stop };
        kasten.SetMeta(FensterStil.Stoff, FensterStil.Pergament);
        eltern.AddChild(kasten);
        var reihe = Kasten<HBoxContainer>(kasten, "CharakterWertReihe");
        reihe.MouseFilter = MouseFilterEnum.Ignore;
        Schrift(reihe, name, "FensterVor").CustomMinimumSize = new Vector2(FensterStil.Mass("wertname", "CharakterMasse"), 0);
        Schrift(reihe, wert, warnung ? "FensterZeileWarnung" : "FensterZeileWert");
        var rest = Schrift(reihe, zusatz, "CharakterZusatz", dehnen: true);
        rest.HorizontalAlignment = HorizontalAlignment.Right;
        return reihe;
    }

    /// <summary>Ein Talent: Name, Sterne, Zahl mit dem Unterschied zur Anlage und der Knopf „Üben · n AP“ (gesperrt mit Grund aus dem Kern).</summary>
    private void TalentMitUeben(Container eltern, Stadtwirtschaft w, Person p, Talent t, string schritt)
    {
        var kasten = new PanelContainer { ThemeTypeVariation = "CharakterTalentZeile", MouseFilter = MouseFilterEnum.Stop };
        kasten.SetMeta(FensterStil.Stoff, FensterStil.Pergament);
        eltern.AddChild(kasten);
        var reihe = Kasten<HBoxContainer>(kasten, "CharakterWertReihe");
        double anlage = p.Anlage(t), jetzt = p.Talent(t), diff = jetzt - anlage;
        Schrift(reihe, t.Name(), "FensterZeileName", dehnen: true);
        reihe.AddChild(new SterneFeld(anlage, jetzt) { SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var zahl = Schrift(reihe, TalentTexte.Sterne(jetzt) + (Math.Abs(diff) < 0.01 ? "" : $" ({(diff > 0 ? "+" : "−")}{TalentTexte.Sterne(Math.Abs(diff))})"),
            diff < -0.01 ? "FensterZeileWarnung" : "FensterZeileWert");
        zahl.CustomMinimumSize = new Vector2(FensterStil.Mass("talentzahl", "CharakterMasse"), 0);
        var tipp = TalentTipp.Text(w, p, t) + $"\nAnlage {TalentTexte.Sterne(anlage)}";
        foreach (var z in p.Zuege.Where(z => Math.Abs(z.Talent(t)) > 1e-9))
            tipp += $"\n{z.Name} {(z.Talent(t) > 0 ? "+" : "−")}{TalentTexte.Sterne(Math.Abs(z.Talent(t)))}";
        if (p.Krankheit is { } k && k.Abzug(t) > 0) tipp += $"\n{k.Name} −{TalentTexte.Sterne(k.Abzug(t))}";
        if (p.Stresslage.TalentAbzug > 0) tipp += $"\nStress −{TalentTexte.Sterne(p.Stresslage.TalentAbzug)}";
        kasten.TooltipText = tipp + "\nMehr zu [[talente|Talenten]] und [[charakterzuege|Charakterzügen]].";

        var talent = t;
        var ueben = new Button { Text = "Üben", FocusMode = FocusModeEnum.None, ThemeTypeVariation = "CharakterUeben" };
        if (w.Familien.TrainingVerboten(_spieler!, t) is { } grund)
        {
            ueben.Disabled = true;
            ueben.TooltipText = grund;
        }
        else ueben.TooltipText = $"{t.Name()} üben: +{schritt} Stern (einmal im Jahr je Talent). Je höher das Talent, desto mehr Aktionspunkte kostet es.";
        ueben.Pressed += () => UiHilfe.Befehlen(_simulation, _spieler, new TalentTrainieren(talent), this, Ergebnis);
        UiHilfe.ApKosten(ueben, w, _spieler!, new TalentTrainieren(t));
        reihe.AddChild(ueben);
    }
}
