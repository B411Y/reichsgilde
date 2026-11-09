using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Was man einem Arbeiter im Innenraum ansieht (Personal und Talentbaum, PR #224): die <b>Stufe seiner Laufbahn</b> (1 = Anfänger, 2 = Geselle
/// und Entsprechungen, 3 = Vorarbeiter und Entsprechungen; 0 = ohne Stufen: Orks, Unfreie), sein <b>Volk</b>, sein <b>Geschlecht</b> und seine <b>Haarfarbe</b>.
/// Die Tracht richtet sich nach der Stufe, nicht nach dem Namen der Arbeitskraft: Knecht, Taschendieb und Rekrut sehen wie Lehrlinge aus, Altknecht
/// wie Gesellen, Großknecht, Meisterdieb und Feldwebel wie Vorarbeiter. Ohne Godot, damit ein Test die Datei mitübersetzen kann (<c>ArbeitertrachtTests</c>).
/// </summary>
/// <param name="Frau">Null: aus der Nummer der Figur gewürfelt (Figuren ohne Arbeiter des Kerns: Fest- und Nachtszenen, Marktdiebe).</param>
/// <param name="Haar">Null oder <see cref="Haarfarbe.Kahl"/>: aus der Palette des Volks nach der Nummer.</param>
public readonly record struct Arbeiterbild(string Arbeitskraft, int Stufe, Volk Volk, bool? Frau, Haarfarbe? Haar)
{
    /// <summary>Erste Stufe der Laufbahn: Lehrling, Knecht, Taschendieb, Rekrut.</summary>
    public bool Anfaenger => Stufe == 1;

    /// <summary>Dritte Stufe: Vorarbeiter, Großknecht, Meisterdieb, Feldwebel.</summary>
    public bool Vorarbeiter => Stufe == 3;

    /// <summary>
    /// Die Stufe: die des Kerns (<see cref="Personalwesen.Stufe"/>); ohne Abschnitt „personal“ in den Daten gibt er 0 – dann nach dem Namen der alten
    /// Laufbahn (Lehrling 1, Geselle 2), sonst 0 (Orks und Unfreie haben keine Stufen).
    /// </summary>
    public static int StufeVon(int kernStufe, string arbeitskraft) =>
        kernStufe > 0 ? kernStufe : arbeitskraft switch { "Lehrling" => 1, "Geselle" => 2, _ => 0 };

    /// <summary>Ein echter Arbeiter des Kerns mit seinem Volk, Geschlecht und Haar (<paramref name="personal"/> darf fehlen: dann nach dem Namen).</summary>
    public static Arbeiterbild Von(Arbeiter a, Personalwesen? personal) => new(
        a.Arbeitskraft, StufeVon(personal?.Stufe(a.Arbeitskraft) ?? 0, a.Arbeitskraft), a.Volk, FrauVon(a.Volk, a.Geschlecht), a.Aussehen.Haar);

    /// <summary>Eine Zeile der Arbeiterliste (<see cref="ArbeiterZeile"/>), mit der Stufe aus dem Kern.</summary>
    public static Arbeiterbild Von(ArbeiterZeile z) => new(
        z.Arbeitskraft, StufeVon(z.Stufe, z.Arbeitskraft), z.Volk, FrauVon(z.Volk, z.Geschlecht), z.Aussehen.Haar);

    /// <summary>
    /// Für Figuren ohne Arbeiter des Kerns (ein ertappter Dieb, die Wache dazu, die Nachtdiebe): <paramref name="stufe"/> nach dem Rang (2: Geselle),
    /// ein Mensch (ein Ork nur, wenn die Arbeitskraft „Ork“ heißt), Geschlecht und Haar aus der Nummer.
    /// </summary>
    public static Arbeiterbild Ersatz(string arbeitskraft = "Geselle", int stufe = 2) =>
        new(arbeitskraft, stufe, arbeitskraft == "Ork" ? Volk.Ork : Volk.Mensch, null, null);

    /// <summary>Frauen gibt es unter Menschen, Zwergen, Elben und Halblingen; Orks und Oger haben nur das Männermodell.</summary>
    private static bool FrauVon(Volk volk, Geschlecht geschlecht) => geschlecht == Geschlecht.Frau && volk is not (Volk.Ork or Volk.Oger);
}
