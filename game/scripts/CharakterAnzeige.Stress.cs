using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Stress im Charakterfenster (Charaktersystem Teil 3b): bei Oberhaupt, Erbe und Ehepartner des eigenen Hauses die Zeile „Stress“ mit Balken
/// (Marken bei den Stufen, Farbe von ruhig bis rot) und einem Tooltip mit Stufe, Folgen und den Quellen und Ventilen dieses und des letzten
/// Jahres. Bei fremden Personen nur die Stufe („wirkt angespannt“). Liest den Kern nur.
/// </summary>
public partial class CharakterAnzeige
{
    private static readonly Color StressRuhig = new(0.62f, 0.68f, 0.52f);
    private static readonly Color StressAngespannt = new(0.92f, 0.72f, 0.30f);
    private static readonly Color StressZermuerbt = new(0.90f, 0.35f, 0.28f);

    private static Color StressFarbe(int stufe) => stufe switch { <= 0 => StressRuhig, 1 => StressAngespannt, _ => StressZermuerbt };

    private void StressBauen(Container daten, Stadtwirtschaft w, Person p)
    {
        var buch = w.Familien;
        if (buch.StressDaten is not { } d || !buch.HatStress(p)) return;
        var stufe = buch.StressStufe(p);
        var name = buch.StressStufenname(stufe);
        var eigen = _spieler is not null && buch.FamilieVon(p)?.Konto == _spieler;
        if (!eigen)
        {
            var fremd = Hauptmenue.Text(daten, $"Wirkt {name}", 14, stufe > 0 ? StressFarbe(stufe) : Grau);
            fremd.MouseFilter = MouseFilterEnum.Stop;
            fremd.TooltipText = $"Wie es in {(p.Geschlecht == Geschlecht.Frau ? "ihr" : "ihm")} aussieht, seht Ihr nur von außen: {name}.\nMehr zu [[stress|Stress]].";
            return;
        }
        var wert = buch.Stress(p);
        var zeile = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = StressTipp(buch, d, p) };
        zeile.AddThemeConstantOverride("separation", 10);
        daten.AddChild(zeile);
        Hauptmenue.Text(zeile, "Stress:", 14).MouseFilter = MouseFilterEnum.Ignore;
        zeile.AddChild(new StressBalken(wert, d.Hoechstwert, d.Stufen.Select(s => s.Ab).ToArray(), StressFarbe(stufe)));
        Hauptmenue.Text(zeile, $"{Zahlenformat.Ganz(wert)} · {name}", 15, StressFarbe(stufe)).MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Für die Liste der Aktivitäten: „Stress: Heinrich −15, Agnes −15“ (leer, wenn die Aktivität keinen Stress bewegt).</summary>
    private string StressHinweis(Stadtwirtschaft w, string art, ICharakter mit, double betrag)
    {
        if (_spieler is null || !w.Familien.HatStresswesen) return "";
        var posten = w.Familien.StressVorschau(_spieler, new Aktivitaet(art, mit.Kennung, betrag));
        if (posten.Count == 0) return "";
        return "Stress: " + string.Join(", ", posten.GroupBy(x => x.Person).Select(g =>
            $"{g.Key.Vorname} {Zahlenformat.MitVorzeichen(g.Sum(x => x.Wert))} ({string.Join("; ", g.Select(x => x.Text))})")) + ".";
    }

    /// <summary>Tooltip: Wert und Stufe, Folgen, Quellen und Ventile dieses und des letzten Jahres, Abklingen und Ventile.</summary>
    private static string StressTipp(Familienbuch buch, StressDaten d, Person p)
    {
        var stufe = buch.StressStufe(p);
        var text = $"Stress {Zahlenformat.Ganz(buch.Stress(p))} von {Zahlenformat.Ganz(d.Hoechstwert)} – {buch.StressStufenname(stufe)}";
        var folgen = buch.StressFolgen(stufe);
        text += folgen == "" ? "\nKeine Folgen." : $"\nFolgen: {folgen}.";
        text += "\nStufen: " + string.Join(", ", d.Stufen.Select(s => $"ab {Zahlenformat.Ganz(s.Ab)} {s.Name}")) +
                $", bei {Zahlenformat.Ganz(d.Hoechstwert)} Zusammenbruch (danach {Zahlenformat.Ganz(d.NachZusammenbruch)}).";
        var eintraege = p.Stresslage.Eintraege.Where(e => e.Jahr >= buch.Jahr - 1).ToList();
        if (eintraege.Count == 0) text += "\nIn diesem und im letzten Jahr nichts.";
        foreach (var gruppe in eintraege.GroupBy(e => e.Jahr).OrderByDescending(g => g.Key))
        {
            text += $"\n{(gruppe.Key == buch.Jahr ? "Dieses Jahr" : "Letztes Jahr")} ({gruppe.Key}):";
            foreach (var e in gruppe) text += $"\n{Zahlenformat.MitVorzeichen(e.Wert)}  {e.Text}";
        }
        var ventile = string.Join(", ", d.Ventile.Keys);
        text += $"\nJedes Jahr klingt er um {Zahlenformat.Ganz(d.Abklingen)} ab. Ventile ({ventile}) wirken je einmal im Jahr.";
        return text + "\nMehr zu [[stress|Stress]] und [[aktivitaeten|Aktivitäten]].";
    }

    /// <summary>Der Balken 0 … Höchstwert mit Marken an den Stufen; gefüllt in der Farbe der Stufe.</summary>
    private sealed partial class StressBalken : Control
    {
        private const int B = 180, H = 14;
        private readonly double _wert, _hoechst;
        private readonly double[] _marken;
        private readonly Color _farbe;

        public StressBalken(double wert, double hoechst, double[] marken, Color farbe)
        {
            _wert = wert;
            _hoechst = Math.Max(1, hoechst);
            _marken = marken;
            _farbe = farbe;
            CustomMinimumSize = new Vector2(B, H);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(0, 0, B, H), new Color(0.22f, 0.20f, 0.18f));
            var laenge = (float)(Math.Clamp(_wert / _hoechst, 0, 1) * B);
            if (laenge > 0.5f) DrawRect(new Rect2(0, 1, laenge, H - 2), _farbe * 0.9f);
            foreach (var m in _marken)
            {
                var x = (float)(m / _hoechst * B);
                DrawLine(new Vector2(x, 0), new Vector2(x, H), new Color(0.9f, 0.85f, 0.7f, 0.8f), 1f);
            }
            DrawRect(new Rect2(0, 0, B, H), new Color(0.05f, 0.04f, 0.03f), false, 1.5f);
        }
    }
}
