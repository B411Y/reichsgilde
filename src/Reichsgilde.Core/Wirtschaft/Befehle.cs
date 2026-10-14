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
public abstract record Befehl;

/// <summary>Ein Befehl an einen bestimmten Betrieb – der Absender muss ihn besitzen.</summary>
public abstract record BetriebsBefehl(string Betrieb) : Befehl;

/// <summary>
/// Im Rathaus den Aufstieg in den nächsthöheren Stand beantragen.
/// Voraussetzung: Vermögen mindestens die Schwelle des neuen Standes; die Gebühr wird sofort fällig.
/// </summary>
public sealed record StandErwerben : Befehl;

/// <summary>
/// Eine freie Werkstatt kaufen oder eine Grube pachten (Name aus "liegenschaften").
/// Voraussetzungen: Mindeststand erreicht, Kaufpreis und die Kosten des ersten Jahres bezahlbar.
/// </summary>
public sealed record LiegenschaftErwerben(string Name) : Befehl;

/// <summary>Bewerbung um ein Amt im Stadtrat (eine pro Jahr, gewählt wird am Jahresende).</summary>
public sealed record AmtBewerben(string Amt) : Befehl;

/// <summary>Ein Geschenk an einen Ratsherrn – kostet Gulden, bringt seine Gunst.</summary>
public sealed record Schenken(string Ratsherr, double Betrag) : Befehl;

/// <summary>Antrag im Rat, das Fron- und Sklavenstatut zu ändern (nur für Ratsmitglieder, einer pro Jahr).</summary>
public sealed record StatutBeantragen(Fronstatut Statut) : Befehl;

/// <summary>Ein Geschenk an einen Heiratskandidaten (Nummer der Person) – bringt Zuneigung, höchstens bis zur Jahresgrenze.</summary>
public sealed record Werben(int Kandidat, double Betrag) : Befehl;

/// <summary>Heiraten: Zuneigung und Ansehen müssen reichen, die Hochzeit wird sofort bezahlt.</summary>
public sealed record Heiraten(int Kandidat) : Befehl;

/// <summary>Testament: Dieses Kind (Nummer der Person) soll das Haus erben.</summary>
public sealed record ErbenBestimmen(int Person) : Befehl;

/// <summary>Einen Spitzel auf ein anderes Haus ansetzen (bis Jahresende; Bericht am Gerichtstag).</summary>
public sealed record SpitzelAnsetzen(string Haus) : Befehl;

/// <summary>Anklage gegen ein Haus erheben – mit allen Beweisen, die man gegen es hat. Verhandelt wird am Jahresende.</summary>
public sealed record Anklagen(string Haus) : Befehl;

/// <summary>Hinterzimmer: eine Bande anheuern. Betrieb nur bei Diebstahl und Sabotage (ein Überfall gilt dem ganzen Haus).</summary>
public sealed record Beauftragen(string Haus, Gauner.Gaunerstueck Art, string? Betrieb = null) : Befehl;

/// <summary>Schweigegeld verlangen – nur mit genug Beweisen gegen das Haus.</summary>
public sealed record Erpressen(string Haus, double Betrag) : Befehl;

/// <summary>Auf einen Erpresserbrief antworten: zahlen (die Beweise verbrennen) oder nicht.</summary>
public sealed record ErpressungBeantworten(string Erpresser, bool Zahlen) : Befehl;

/// <summary>Einen Wächter für den Betrieb anstellen oder entlassen (Lohn am Jahresende).</summary>
public sealed record WaechterSetzen(string Betrieb, bool Angestellt) : BetriebsBefehl(Betrieb);

/// <summary>Die Gießerei stellt ab sofort diese Ware her.</summary>
public sealed record ProduktWaehlen(string Betrieb, string Ware) : BetriebsBefehl(Betrieb);

/// <summary>Die Mine teilt ihre Arbeitszeit so auf (0 = nur Eisen, 1 = nur Silber).</summary>
public sealed record FoerderungAufteilen(string Betrieb, double Silberanteil) : BetriebsBefehl(Betrieb);

/// <summary>Setzt die Zahl einer Arbeitskraft-Art auf <paramref name="Anzahl"/> (0 = alle entlassen).</summary>
public sealed record PersonalSetzen(string Betrieb, string Arbeitskraft, int Anzahl) : BetriebsBefehl(Betrieb);

/// <summary>Kauft einen Karren dieses Typs für den Betrieb.</summary>
public sealed record KarrenKaufen(string Betrieb, string Typ) : BetriebsBefehl(Betrieb);

/// <summary>Der Karren mit dieser Nummer (ab 0) fährt künftig mit dieser Eskorte.</summary>
public sealed record EskorteWaehlen(string Betrieb, int Karren, string Eskorte) : BetriebsBefehl(Betrieb);

/// <summary>Darf der Karren mit dieser Nummer nachts ins Umland fahren?</summary>
public sealed record NachtfahrtErlauben(string Betrieb, int Karren, bool Erlaubt) : BetriebsBefehl(Betrieb);

/// <summary>Antwort auf einen Befehl: ausgeführt oder abgelehnt – immer mit einem Satz für den Spieler.</summary>
public sealed record Befehlsergebnis(bool Erfolg, string Meldung)
{
    public static Befehlsergebnis Ok(string meldung) => new(true, meldung);
    public static Befehlsergebnis Abgelehnt(string grund) => new(false, grund);
}
