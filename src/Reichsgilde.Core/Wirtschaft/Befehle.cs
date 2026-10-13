namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein Befehl ist der Wunsch eines Spielers (oder einer KI), etwas an seinen Betrieben zu ändern.
///
/// Warum Befehle statt direkter Methodenaufrufe?
///   • Eine Stelle prüft alles: Gehört der Betrieb dem Absender? Reicht das Geld? Erlaubt es das Statut?
///   • Befehle enthalten nur Namen und Zahlen – keine Objekte. Im Multiplayer schickt der Client
///     genau diese kleinen Datensätze an den Server, der sie prüft und ausführt.
///   • KI-Rivalen benutzen denselben Weg. Damit ist „die KI schummelt nicht" technisch gesichert.
///
/// Ein „record" ist in C# eine Klasse, die nur Daten trägt: Zwei Befehle mit gleichen Werten sind gleich.
/// </summary>
public abstract record Befehl(string Betrieb);

/// <summary>Die Gießerei stellt ab sofort diese Ware her.</summary>
public sealed record ProduktWaehlen(string Betrieb, string Ware) : Befehl(Betrieb);

/// <summary>Die Mine teilt ihre Arbeitszeit so auf (0 = nur Eisen, 1 = nur Silber).</summary>
public sealed record FoerderungAufteilen(string Betrieb, double Silberanteil) : Befehl(Betrieb);

/// <summary>Setzt die Zahl einer Arbeitskraft-Art auf <paramref name="Anzahl"/> (0 = alle entlassen).</summary>
public sealed record PersonalSetzen(string Betrieb, string Arbeitskraft, int Anzahl) : Befehl(Betrieb);

/// <summary>Kauft einen Karren dieses Typs für den Betrieb.</summary>
public sealed record KarrenKaufen(string Betrieb, string Typ) : Befehl(Betrieb);

/// <summary>Der Karren mit dieser Nummer (ab 0) fährt künftig mit dieser Eskorte.</summary>
public sealed record EskorteWaehlen(string Betrieb, int Karren, string Eskorte) : Befehl(Betrieb);

/// <summary>Darf der Karren mit dieser Nummer nachts ins Umland fahren?</summary>
public sealed record NachtfahrtErlauben(string Betrieb, int Karren, bool Erlaubt) : Befehl(Betrieb);

/// <summary>Antwort auf einen Befehl: ausgeführt oder abgelehnt – immer mit einem Satz für den Spieler.</summary>
public sealed record Befehlsergebnis(bool Erfolg, string Meldung)
{
    public static Befehlsergebnis Ok(string meldung) => new(true, meldung);
    public static Befehlsergebnis Abgelehnt(string grund) => new(false, grund);
}
