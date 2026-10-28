using Godot;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>Die Ebenen der Kartenwerkstatt, die sich ein- und ausblenden lassen (Knopf „Ebenen“ oben links).</summary>
public enum Werkstattebene
{
    /// <summary>Bauzonen (Werkstatt-, Wohn- und Einwohnerplätze, Vorstadt, Mauerring) samt Kapazitätsanzeige.</summary>
    Bauzonen,
    /// <summary>Wald-, Feld- und Seeflächen (Umriss und Füllung) mit der Natur darin: gestreute Pflanzen, Äcker und einzeln gesetzte Pflanzen.</summary>
    Natur,
    /// <summary>Streuflächen (Büsche, Gras, Steine, Holz) mit ihrer Streu.</summary>
    Requisiten,
    /// <summary>Linien und Punkte der Bearbeitung: Markierungen der Wege und Mauern, Pfeile der Gewässer, Kartenrand.</summary>
    Linien,
    /// <summary>Alle Namen (Gebäude, Flächen, Wege, Tore …).</summary>
    Beschriftungen,
}

/// <summary>
/// Ebenen ein- und ausblenden – ein Teil der <c>partial class Stadtansicht</c> für die Kartenwerkstatt. Der Zustand gilt nur für die Sitzung
/// (die Karte ändert sich nicht); <see cref="WerkstattEbenenAnwenden"/> setzt ihn am Ende jedes Bildes auf die Knoten, damit auch neu gebaute
/// Knoten gleich stimmen. Die Bedienung (Knopf, Liste, Taste H) steht in Kartenwerkstatt.Ebenen.cs.
/// </summary>
public partial class Stadtansicht
{
    private readonly Dictionary<Werkstattebene, bool> _wsEbenen = Enum.GetValues<Werkstattebene>().ToDictionary(e => e, _ => true);

    public bool WerkstattEbeneSichtbar(Werkstattebene ebene) => _wsEbenen[ebene];

    public void WerkstattEbeneSetzen(Werkstattebene ebene, bool sichtbar) => _wsEbenen[ebene] = sichtbar;

    /// <summary>Setzt die Sichtbarkeit aller Knoten nach den Ebenen (nur wo sie sich ändert).</summary>
    private void WerkstattEbenenAnwenden()
    {
        static void Setzen(Node3D? knoten, bool sichtbar)
        {
            if (knoten is not null && IsInstanceValid(knoten) && knoten.Visible != sichtbar) knoten.Visible = sichtbar;
        }
        bool An(Werkstattebene e) => _wsEbenen[e];

        foreach (var (flaeche, knoten) in _wsFlaechen)
            Setzen(knoten, flaeche switch
            {
                EntwurfBauzone => An(Werkstattebene.Bauzonen),
                EntwurfNaturflaeche { Art: "streu" } => An(Werkstattebene.Requisiten),
                _ => An(Werkstattebene.Natur),
            });
        Setzen(_wsKapazitaet, An(Werkstattebene.Bauzonen));
        foreach (var (flaeche, knoten) in _wsNatur) Setzen(knoten, flaeche.Art == "streu" ? An(Werkstattebene.Requisiten) : An(Werkstattebene.Natur));
        foreach (var knoten in _wsFelder.Values) Setzen(knoten, An(Werkstattebene.Natur));
        Setzen(_wsPflanzenKnoten, An(Werkstattebene.Natur));

        var linien = An(Werkstattebene.Linien);
        foreach (var knoten in _wsWege.Values) Setzen(knoten.GetNodeOrNull<Node3D>("Markierung"), linien);
        foreach (var knoten in _wsMauern.Values) Setzen(knoten.GetNodeOrNull<Node3D>("Markierung"), linien);
        Setzen(_wsGewaesserMarken, linien);
        Setzen(GetNodeOrNull<Node3D>("Kartenrand"), linien);

        // Namen ohne Kennung (an gewählten Flächen, Wegen, Mauern) sind sonst immer sichtbar; die mit Kennung steuert BeschriftungenZeigen
        var namen = An(Werkstattebene.Beschriftungen);
        foreach (var label in _beschriftungen)
            if (IsInstanceValid(label) && !label.HasMeta("kennung") && label.Visible != namen) label.Visible = namen;
    }
}
