using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Gemeinsame Bausteine für die Meinungen (Charaktersystem Teil 2, PR #231): Farbe nach Wert, der Tooltip mit allen Gründen
/// und der Balken von −100 (rot) über 0 bis +100 (grün). Charakterfenster und Rats-Tafel zeigen die Meinung gleich.
/// </summary>
internal static partial class MeinungsHilfe
{
    public static readonly Color Gruen = new(0.55f, 0.85f, 0.5f);
    public static readonly Color Rot = new(0.95f, 0.45f, 0.4f);
    public static readonly Color Grau = new(0.72f, 0.72f, 0.72f);

    /// <summary>Grün ab +5, rot ab −5, dazwischen grau.</summary>
    public static Color Farbe(double wert) => wert >= 5 ? Gruen : wert <= -5 ? Rot : Grau;

    /// <summary>
    /// Tooltip „Meinung von X über Euch“ mit allen Gründen: feste zuerst, die aus Taten danach und als verblassend gekennzeichnet.
    /// Ohne Familie (kein Oberhaupt) gibt es keine Gründe zu zeigen.
    /// </summary>
    public static string Tipp(Stadtwirtschaft w, ICharakter von, Konto spieler)
    {
        var summe = w.Meinungen.Meinung(von, spieler);
        var kopf = $"{von.Name} über Euch: {Zahlenformat.MitVorzeichen(summe)}";
        if (w.Meinungen.Oberhaupt(spieler) is not { } ich) return kopf + "\nMehr zu [[meinung|Meinungen]].";
        var gruende = w.Meinungen.Gruende(von, ich);
        if (gruende.Count == 0) return kopf + "\nKeine Gründe – er kennt Euch kaum.\nMehr zu [[meinung|Meinungen]].";
        var text = kopf;
        foreach (var g in gruende.Where(g => !g.Verblasst)) text += $"\n{Zahlenformat.MitVorzeichen(g.Wert)}  {g.Text}";
        var verblassend = gruende.Where(g => g.Verblasst).ToList();
        if (verblassend.Count > 0)
        {
            text += "\nAus Taten (verblassen jedes Jahr):";
            foreach (var g in verblassend) text += $"\n{Zahlenformat.MitVorzeichen(g.Wert)}  {g.Text}";
        }
        return text + "\nMehr zu [[meinung|Meinungen]] und [[aktivitaeten|Aktivitäten]].";
    }

    /// <summary>Der Balken: Mitte = 0, nach links rot, nach rechts grün.</summary>
    public sealed partial class Balken : Control
    {
        private const int B = 180, H = 14;
        private readonly double _wert, _hoechst;

        public Balken(double wert, double hoechst)
        {
            _wert = wert;
            _hoechst = Math.Max(1, hoechst);
            CustomMinimumSize = new Vector2(B, H);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(0, 0, B, H), new Color(0.22f, 0.20f, 0.18f));
            var halb = B / 2f;
            var laenge = (float)(Math.Clamp(_wert / _hoechst, -1, 1) * halb);
            if (Math.Abs(laenge) > 0.5f)
                DrawRect(laenge > 0 ? new Rect2(halb, 1, laenge, H - 2) : new Rect2(halb + laenge, 1, -laenge, H - 2), _wert >= 0 ? Gruen * 0.85f : Rot * 0.85f);
            DrawLine(new Vector2(halb, 0), new Vector2(halb, H), new Color(0.9f, 0.85f, 0.7f, 0.8f), 1f);
            DrawRect(new Rect2(0, 0, B, H), new Color(0.05f, 0.04f, 0.03f), false, 1.5f);
        }
    }
}
