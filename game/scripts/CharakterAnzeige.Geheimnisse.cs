using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Geheimnisse im Charakterfenster (Charaktersystem Teil 3c): bei Personen des eigenen Hauses „Ihr hütet …“, bei anderen nur, was Euer Haus weiß
/// (auch aus der Beichte), und was die ganze Stadt weiß. Bei einem bekannten Geheimnis die Knöpfe Erpressen (Gulden), Ratsstimme, Anzeigen und
/// Bloßstellen – ausgegraut mit dem Grund; bei Beichtwissen „Beichtgeheimnis brechen“. Beim Ratsherrn dazu der Spitzel. Liest den Kern nur,
/// handelt über Befehle. Vor jeder Zeile das Siegel der Art (`GeheimnisSiegel`, Rückfall: nur Text).
/// </summary>
public partial class CharakterAnzeige
{
    private double _schweigegeld = 500;

    private void GeheimnisseBauen(Stadtwirtschaft w, ICharakter c)
    {
        var gw = w.Geheimnisse;
        _letzterBefehl.Clear();
        if (!gw.IstAktiv || _spieler is null) return;
        var liste = gw.Sichtbar(_spieler, c).OrderBy(g => g.Nummer).ToList();
        var ratsherr = c as Ratsherr;
        if (liste.Count == 0 && ratsherr is null) return;
        _inhalt.AddChild(new HSeparator());
        var kopf = Hauptmenue.Text(_inhalt, "Geheimnisse", 17, Gold);
        kopf.MouseFilter = MouseFilterEnum.Stop;
        kopf.TooltipText = "Was Euer Haus über diese Person weiß – erfahren durch Spitzel, Gerede oder die Beichte. Ein Geheimnis lässt sich nutzen: " +
                           "erpressen, anzeigen, bloßstellen – oder schweigen.\nMehr zu [[geheimnisse|Geheimnissen]].";
        if (_meldung != "" && _aktivMit is null)
            Hauptmenue.Text(_inhalt, _meldung, 14, _meldungGut ? Gruen : Rot, umbruchBreite: Breite - 50);
        if (liste.Count == 0)
            Hauptmenue.Text(_inhalt, $"Ihr wisst nichts über {c.Name}, was sich verwenden ließe.", 13, Grau, umbruchBreite: Breite - 50);
        foreach (var g in liste) GeheimnisZeile(w, c, g);
        if (ratsherr is not null) RatsspitzelZeile(w, ratsherr);
    }

    private static readonly Dictionary<Geheimnisart, Texture2D?> SiegelSpeicher = new();

    /// <summary>Das Siegel einer Geheimnisart (assets/oberflaeche/geheimnisse/&lt;art&gt;.png, rund, 128 px) oder null, wenn es die Datei nicht gibt.</summary>
    internal static Texture2D? GeheimnisSiegel(Geheimnisart art)
    {
        if (SiegelSpeicher.TryGetValue(art, out var bild)) return bild;
        var pfad = "res://assets/oberflaeche/geheimnisse/" + art.ToString().ToLowerInvariant() + ".png";
        bild = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
        SiegelSpeicher[art] = bild;
        return bild;
    }

    private void GeheimnisZeile(Stadtwirtschaft w, ICharakter c, Geheimnis g)
    {
        var gw = w.Geheimnisse;
        var art = gw.Daten.Art(g.Art);
        var spieler = _spieler!;
        var eigen = gw.IstEigen(spieler, g);
        var wissen = gw.Wissen(spieler, g);
        var text = eigen ? $"Ihr hütet: {gw.Beschreibung(g)} (seit {g.Jahr})"
            : g.Oeffentlich ? $"Die ganze Stadt weiß: {gw.Beschreibung(g)}"
            : wissen is { AusBeichte: true } ? $"Aus der Beichte: {gw.Beschreibung(g)}"
            : $"Ihr wisst: {gw.Beschreibung(g)}";
        if (eigen && g.Oeffentlich) text += " – die ganze Stadt weiß es";
        else if (eigen && g.EhepartnerWeiss) text += " – der Ehepartner weiß es";
        var kasten = new VBoxContainer();
        kasten.AddThemeConstantOverride("separation", 2);
        _inhalt.AddChild(kasten);
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 8);
        kasten.AddChild(reihe);
        var siegel = GeheimnisSiegel(g.Art);
        if (siegel is not null)
        {
            // Siegel der Art (64 px, mit Mipmaps) in Zeilenhöhe; ohne Datei bleibt nur der Text
            var bild = new TextureRect
            {
                Texture = siegel, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(36, 36), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, MouseFilter = MouseFilterEnum.Pass,
                SizeFlagsVertical = SizeFlags.ShrinkBegin, TooltipText = art.Beschreibung,
            };
            reihe.AddChild(bild);
        }
        var zeile = Hauptmenue.Text(reihe, text, 15, g.Oeffentlich ? Grau : eigen ? Rot : Gold, umbruchBreite: Breite - 50 - (siegel is null ? 0 : 44));
        zeile.MouseFilter = MouseFilterEnum.Stop;
        var tipp = art.Beschreibung;
        if (wissen is not null && !eigen) tipp += $"\nErfahren {wissen.Jahr} – {wissen.Quelle}.";
        tipp += art.Beweiskraft > 0
            ? $"\nVor Gericht ein Vergehen: Beweiskraft {Zahlenformat.Ganz(art.Beweiskraft)}, Buße {Zahlenformat.Ganz(art.Busse)}" +
              (art.KerkerJahre > 0 ? $", {art.KerkerJahre} Jahr Kerker" : "") + (art.NurMitStadtglaube ? " (nur wer sich nach außen zum Stadtglauben bekennt)" : "") + "."
              + (art.NurMitStadtglaube && !g.Oeffentlich && gw.KeinVergehen(g) is { } keines ? $" Hier: {keines}" : "")
            : "\nKein Vergehen – nicht anzeigbar.";
        if (art.Ansehen > 0) tipp += $"\nBloßgestellt: Ansehen −{Zahlenformat.Ganz(art.Ansehen)} für {gw.Daten.AnsehenJahre} Jahre.";
        if (eigen) tipp += "\nWer es erfährt, kann Euch erpressen, anzeigen oder bloßstellen. Solange es geheim ist, drückt es jedes Jahr ein wenig auf die Seele (Stress).";
        zeile.TooltipText = tipp + "\nMehr zu [[geheimnisse|Geheimnissen]].";
        if (eigen || g.Oeffentlich) return;

        var knoepfe = new HFlowContainer();
        knoepfe.AddThemeConstantOverride("h_separation", 8);
        knoepfe.AddThemeConstantOverride("v_separation", 4);
        kasten.AddChild(knoepfe);
        if (wissen is { AusBeichte: true })
        {
            var ziel = c is Ratsherr r ? r.Kennung : gw.HausVon(g)?.Inhaber ?? "";
            BefehlsKnopf(w, knoepfe, "Beichtgeheimnis brechen", new BeichtgeheimnisBrechen(ziel),
                w.Kirchenwesen.IstAktiv ? null : "In dieser Stadt gibt es keine Kirchen.",
                "Was Ihr in der Beichte gehört habt, wird nutzbar – aber das ist selbst ein Vergehen (Beichtbruch).");
            return;
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        if (c is Person)
        {
            var betrag = new Zahlfeld { MinValue = 50, MaxValue = 20000, Step = 50, Value = _schweigegeld, Suffix = waehrung, CustomMinimumSize = new Vector2(140, 0) };
            knoepfe.AddChild(betrag);
            var erpressen = BefehlsKnopf(w, knoepfe, "Erpressen", new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, _schweigegeld), null,
                "Ein Erpresserbrief: Schweigegeld gegen Euer Schweigen. Zahlt das Haus, vergesst Ihr es; zahlt es nicht, bleibt Euch Anzeige oder Bloßstellen. " +
                "Der Brief ist selbst eine Spur gegen Euch.");
            betrag.ValueChanged += neu =>
            {
                _schweigegeld = neu;
                KnopfPruefen(w, erpressen, new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, neu), null);
            };
        }
        BefehlsKnopf(w, knoepfe, "Ratsstimme", new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Ratsstimme), null,
            "In der nächsten Ratssitzung stimmt der Erpresste bei allen Punkten wie Ihr (Euer Antrag und Eure Wahl: für Euch). Einmal je Geheimnis und Jahr; " +
            "eine Spur, und er denkt schlechter über Euch.");
        BefehlsKnopf(w, knoepfe, "Anzeigen", new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Anzeige), null,
            $"Vor Gericht als Beweis (Gebühr {Zahlenformat.Ganz(w.Justiz.IstAktiv ? w.Justiz.Gerichtsgebuehr(_spieler!) : 0)} {waehrung}). Verhandelt wird am Jahresende; danach weiß es die ganze Stadt.");
        BefehlsKnopf(w, knoepfe, "Bloßstellen", new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Blossstellen), null,
            "Die ganze Stadt erfährt es – ohne Gericht und ohne Spur. Wer bloßgestellt wird, vergisst es Euch nicht.");
    }

    /// <summary>Beim Ratsherrn: ein Spitzel bis zum Jahresende (findet nur Geheimnisse, keine Spuren).</summary>
    private void RatsspitzelZeile(Stadtwirtschaft w, Ratsherr r)
    {
        if (!w.Justiz.IstAktiv) return;
        var knoepfe = new HFlowContainer();
        _inhalt.AddChild(knoepfe);
        BefehlsKnopf(w, knoepfe, "Spitzel ansetzen", new SpitzelAnsetzen(r.Kennung), w.Geheimnisse.RatsspitzelVerboten(_spieler!, r),
            $"Ein Spitzel heftet sich bis zum Jahresende an {r.Name} ({Zahlenformat.Ganz(w.Justiz.Daten.SpitzelKosten)} {w.Daten.Allgemein.Waehrung}). " +
            "Am Gerichtstag berichtet er, was er gefunden hat – nur Geheimnisse, keine Taten.");
    }

    /// <summary>Ein Knopf für einen Befehl: ausgegraut mit Grund (Regel des Kerns, Aktionspunkte), sonst mit Kosten im Tooltip.</summary>
    private Button BefehlsKnopf(Stadtwirtschaft w, Container eltern, string text, Befehl befehl, string? grund, string erklaerung)
    {
        var knopf = new Button { Text = text, FocusMode = FocusModeEnum.None };
        eltern.AddChild(knopf);
        knopf.SetMeta("erklaerung", erklaerung);
        KnopfPruefen(w, knopf, befehl, grund);
        knopf.Pressed += () => UiHilfe.Befehlen(_simulation, _spieler, _letzterBefehl.GetValueOrDefault(knopf, befehl), this, Ergebnis);
        return knopf;
    }

    /// <summary>Der Befehl hinter jedem Knopf – beim Erpressen ändert ihn das Zahlfeld (Betrag). Neu bei jedem Aufbau.</summary>
    private readonly Dictionary<Button, Befehl> _letzterBefehl = new();

    private void KnopfPruefen(Stadtwirtschaft w, Button knopf, Befehl befehl, string? grund)
    {
        _letzterBefehl[knopf] = befehl;
        var ap = w.ApKosten(_spieler!, befehl);
        grund ??= befehl is GeheimnisNutzen n && w.Geheimnisse.Nummer(n.Geheimnis) is { } g ? w.Geheimnisse.NutzenVerboten(_spieler!, g, n.Nutzung, n.Betrag) : null;
        if (grund is null && !w.ApReichen(_spieler!, befehl)) grund = $"Nicht genug Aktionspunkte – das kostet {ap} AP.";
        knopf.Disabled = grund is not null;
        var erklaerung = knopf.GetMeta("erklaerung").AsString();
        knopf.TooltipText = (grund is not null ? $"Gerade nicht möglich: {grund}\n" : ap > 0 ? $"{ap} AP. " : "") + erklaerung;
    }
}
