using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Gemeinsame Grundlage der drei Tafeln zum Selbstfahren der Karren (Transportmenü <see cref="KarrenFahrtAnzeige"/>, Ladefenster
/// <see cref="KarrenLadeAnzeige"/>, Handelsfenster <see cref="KarrenHandelAnzeige"/>). Wie jede <see cref="InnenTafel"/> ändern sie nichts
/// selbst: jeder Knopf schickt einen Befehl (<see cref="KarrenLosschicken"/>, <see cref="KarrenBeladen"/> …); gesperrte Knöpfe nennen den Grund
/// aus den Verbots-Abfragen des Kerns. Farben, Breiten und Schriftgrößen kommen aus dem Thema der Fenster (<see cref="FensterStil"/>,
/// <c>fenster_thema.tres</c>, Maße „karren_…“).
///
/// Ein Karren wird über seinen Heimatbetrieb und seine Nummer dort angesprochen (<see cref="Eintrag"/>); die Tafeln zeigen alle Karren des Hauses.
/// </summary>
public abstract class KarrenTafel : InnenTafel
{
    /// <summary>Ein Karren des Hauses mit Heimatbetrieb und Nummer dort – so adressieren ihn die Befehle.</summary>
    protected sealed record Eintrag(Betrieb Heimat, int Nr, Karren Karren)
    {
        public string Schluessel => $"{Heimat.Name}#{Nr}";
    }

    /// <summary>Die Hauptszene öffnet eine andere Karrentafel („fahrt“, „laden“, „handel“) für den Karren mit diesem Schlüssel.</summary>
    public event Action<string, Betrieb, string?>? Wechseln;

    /// <summary>Welcher Karren beim Öffnen vorgewählt ist (Schlüssel „Betrieb#Nummer“).</summary>
    public string? Vorwahl { get; set; }

    protected KarrenTafel() : base(FensterStil.Mass("karren_breite")) { }

    protected static Color Schrift => FensterStil.Farbe("tinte");
    protected static Color Leise => FensterStil.Farbe("tinte_leise");
    protected static Color Akzent => FensterStil.Farbe("akzent");
    protected static Color Warnung => FensterStil.Farbe("warnung");
    protected static Color Gut => FensterStil.Farbe("gut");

    protected static int Masz(string name) => FensterStil.Mass("karren_" + name);

    protected void Weiter(string tafel, Betrieb b, string? schluessel) => Wechseln?.Invoke(tafel, b, schluessel);

    protected IEnumerable<Eintrag> MeineKarren(Stadtwirtschaft w) =>
        w.Betriebe.Where(b => b.Konto == Spieler).SelectMany(b => b.Karren.Select((k, i) => new Eintrag(b, i, k)));

    protected static string LadungText(Karren k) =>
        k.Ladung.Count == 0 ? "leer" : string.Join(", ", k.Ladung.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{Zahlenformat.Ganz(x.Value)} {x.Key}"));

    protected static string LadungStand(Karren k) => string.Join(",", k.Ladung.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}:{x.Value}"));

    /// <summary>Eine Kopfzeile mit Überschrift, Betrieb und Hinweis (Schrift aus dem Thema).</summary>
    protected void Titel(string titel, Betrieb b, string hinweis)
    {
        Hauptmenue.Text(Inhalt, titel, Masz("titel"), Akzent, zentriert: true);
        Hauptmenue.Text(Inhalt, $"{b.Name} · {b.Artdaten.Name}", Masz("schrift"), Schrift, zentriert: true);
        if (hinweis != "") Hauptmenue.Text(Inhalt, hinweis, Masz("klein"), Leise, zentriert: true, umbruchBreite: Masz("breite") - 60);
        Inhalt.AddChild(new HSeparator());
    }

    protected Label Zeile(Container eltern, string text, Color? farbe = null, float breite = 0, bool rechts = false)
    {
        var label = Hauptmenue.Text(eltern, text, Masz("schrift"), farbe ?? Schrift);
        label.VerticalAlignment = VerticalAlignment.Center;
        if (breite > 0) label.CustomMinimumSize = new Vector2(breite, 0);
        if (rechts) label.HorizontalAlignment = HorizontalAlignment.Right;
        return label;
    }

    protected static Button Knopf(Container eltern, string text, string tipp, Action aktion, string? grund = null)
    {
        var knopf = Hauptmenue.Knopf(eltern, text, aktion);
        knopf.TooltipText = grund ?? tipp;
        knopf.Disabled = grund is not null;
        return knopf;
    }

    /// <summary>Ein Zahlfeld (ganze Zahlen); der Wert bleibt in <paramref name="speicher"/>, auch wenn die Tafel sich neu aufbaut.</summary>
    protected static Zahlfeld Zahl(Container eltern, int wert, int hoechst, Action<int> geaendert, string tipp)
    {
        var feld = new Zahlfeld { MinValue = 1, MaxValue = Math.Max(1, hoechst), Step = 1, Value = Math.Clamp(wert, 1, Math.Max(1, hoechst)), CustomMinimumSize = new Vector2(Masz("feld"), 0), TooltipText = tipp };
        feld.ValueChanged += v => geaendert((int)v);
        eltern.AddChild(feld);
        return feld;
    }

    protected static string Waehrung(Stadtwirtschaft w) => w.Daten.Allgemein.Waehrung;
}
