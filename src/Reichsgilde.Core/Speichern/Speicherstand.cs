using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Speichern;

// ─────────────────────────────────────────────────────────────────────────────
//  Das Format der Speicherdatei.
//
//  Jede Klasse hier ist ein reines Datenpaket („DTO", data transfer object):
//  nur Namen und Zahlen, keine Logik, keine Verweise zwischen Objekten.
//  Verweise werden als Nummer (Index) oder Name gespeichert – so bleibt die Datei
//  lesbar, und eine spätere Programmversion kann sie gezielt umwandeln.
//
//  Ändert sich das Format, wird <see cref="Speicherstand.AktuellesFormat"/> erhöht.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Ein kompletter Spielstand.</summary>
public sealed record Speicherstand
{
    public const int AktuellesFormat = 3; // 2: Spieltag 05–22 Uhr (1020 Minuten) statt 24 Stunden; 3: 04–23 Uhr (1140 Minuten, Uhrumstellung.cs)

    public int Format { get; init; } = AktuellesFormat;

    /// <summary>Wann gespeichert wurde (nur zur Anzeige im Ladedialog).</summary>
    public DateTime Gespeichert { get; init; }

    /// <summary>Kurzbeschreibung für den Ladedialog, z. B. „Haus Hammerschmid · Sommer 1405".</summary>
    public string Titel { get; init; } = "";

    public Partieeinstellungen Einstellungen { get; init; } = Partieeinstellungen.Standard;
    public long VergangeneMinuten { get; init; }
    public double Tempo { get; init; } = 1;
    public bool IstBeendet { get; init; }
    public string? Endgrund { get; init; }

    /// <summary>Welches Konto der Mensch spielt (Nummer in <see cref="WirtschaftStand.Konten"/>).</summary>
    public int? Spielerkonto { get; init; }

    /// <summary>
    /// Die Wirtschaftsdaten, mit denen gespielt wird – im Spielstand mitgespeichert.
    /// So läuft eine gespeicherte Partie mit genau ihren Zahlen weiter, auch wenn wirtschaft.json
    /// inzwischen geändert wurde.
    /// </summary>
    public Wirtschaftsdaten? Daten { get; init; }

    /// <summary>Die Karte der Stadt, wie bei <see cref="Daten"/> mitgespeichert (null: ohne Karte oder alter Spielstand – dann die aktuelle).</summary>
    public Bauen.Stadtkarte? Karte { get; init; }

    public WirtschaftStand? Wirtschaft { get; init; }

    /// <summary>
    /// Befehle, die eingereiht, aber noch nicht ausgeführt sind (Mehrspieler M2, <see cref="Mehrspieler.Befehlsbote"/>) – mit Ziel-Tick, Haus und
    /// laufender Nummer. Fehlt das Feld (alte Spielstände, leere Schlange), ist die Schlange leer. Rechnet eine spätere Umstellung die Uhr um
    /// (wie <see cref="Uhrumstellung"/>), gehört der Ziel-Tick dazu.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<Mehrspieler.EingereihterBefehl>? Warteschlange { get; init; }

    /// <summary>
    /// Hat in dieser Partie je ein Entwicklerbefehl gewirkt (<see cref="Entwickler.Entwicklerhilfe.Benutzt"/>)? Steht im Kopf, damit die
    /// Laden-Liste es zeigen kann (<see cref="Speicherung.Kopf"/>). Fehlt das Feld (alte Spielstände, nie benutzt), heißt es nein.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool MitEntwicklerhilfe { get; init; }

    /// <summary>
    /// Die Sperre der Entwicklerbefehle (<see cref="Entwickler.Entwicklerhilfe.Erlaubt"/>) – eine Regel der Partie, damit ein Befehlsprotokoll
    /// beim Abspielen gleich abläuft. Fehlt das Feld, sind sie gesperrt; im Einzelspiel setzt die Oberfläche sie nach dem Laden neu.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EntwicklerErlaubt { get; init; }
}

public sealed record WirtschaftStand
{
    public Fronstatut Statut { get; init; }
    public long ZufallWuerfe { get; init; }
    public long ReihenfolgeWuerfe { get; init; }
    public List<KontoStand> Konten { get; init; } = new();
    public List<BetriebStand> Betriebe { get; init; } = new();
    public Dictionary<string, MarktstandStand> Markt { get; init; } = new();
    public List<RivaleStand> Rivalen { get; init; } = new();

    /// <summary>Der Stadtrat (null in Spielständen von vor dem Rat – dann beginnt er wie bei Spielstart).</summary>
    public RatStand? Rat { get; init; }

    /// <summary>Familien und Brautschau (null in Spielständen von vor der Dynastie – dann ohne Familien weiter).</summary>
    public FamilienbuchStand? Familien { get; init; }

    /// <summary>Das Stadtgericht (null in Spielständen von vor dem Gericht).</summary>
    public JustizStand? Justiz { get; init; }

    /// <summary>Die Unterwelt (null in Spielständen von vor den Gaunern).</summary>
    public UnterweltStand? Gauner { get; init; }

    /// <summary>Krankheiten und Pest (null in Spielständen von vor der Gesundheit).</summary>
    public GesundheitStand? Gesundheit { get; init; }

    /// <summary>Gästebücher, Barden und Würfeltische (null in Spielständen von vor dem Wirt).</summary>
    public GastwirtschaftStand? Gastwirtschaft { get; init; }

    /// <summary>Kirchenbücher, Beichtwissen, gelesene und verbreitete Schriften (null in Spielständen von vor dem Prediger).</summary>
    public KirchenStand? Kirche { get; init; }

    /// <summary>Aufträge der Diebe, Schutzgeld- und Lösegeldforderungen, Diebesbücher (null in Spielständen von vor dem Dieb).</summary>
    public DiebeStand? Diebe { get; init; }

    /// <summary>Sold, Kopfgeld, Geleitgeld und Razzien je Haus (null in Spielständen von vor dem Gardisten).</summary>
    public WacheStand? Wache { get; init; }

    /// <summary>Kassen, Kredite und Bankbuch der Geldleiher (null in Spielständen von vor dem Geldleiher).</summary>
    public BankStand? Bank { get; init; }

    /// <summary>Giftanschläge der Nacht, Moschus-Geschenke des Jahres, Heiligenduft (null in Spielständen von vor dem Parfumeur).</summary>
    public ParfumStand? Parfum { get; init; }

    /// <summary>Anteile der Konfessionen, Stadtglaube und sein Antrag (null in Spielständen von vor dem Glauben).</summary>
    public GlaubeStand? Glaube { get; init; }

    /// <summary>Die Einwohner der Bürger-KI (null in Spielständen von vor der Bürger-KI).</summary>
    public Buerger.EinwohnerschaftStand? Einwohner { get; init; }

    /// <summary>Verkaufte Betriebe, die wieder zu haben sind – mit Stufe und Kaufpreis (null: keine; alte Spielstände).</summary>
    public List<FreieLiegenschaftStand>? Abgegeben { get; init; }

    /// <summary>Grundstücke: Wohnhäuser, Neubauten, Bauplätze (null: ohne Bauwesen oder alter Spielstand – dann die Startplätze der Karte).</summary>
    public BauenStand? Bauen { get; init; }

    /// <summary>Würfe des Zufalls der Werkstätten (Unfälle; 0 in alten Spielständen).</summary>
    public long WerkstattWuerfe { get; init; }

    /// <summary>Belag der Wege, Bauaufträge, Wegekasse (null: ohne Wegebau oder alter Spielstand – dann der Belag der Karte).</summary>
    public WegebauStand? Wegebau { get; init; }

    /// <summary>Schiffe und Märkte der fernen Städte (null: ohne Schiffshandel oder alter Spielstand – dann ohne Schiffe, Märkte ausgeglichen).</summary>
    public SchiffahrtStand? Schiffahrt { get; init; }

    /// <summary>Die Stadtkasse (null: ohne Kasse oder alter Spielstand – dann der Startbestand der Karte und der Steuersatz der Daten).</summary>
    public StadtkasseStand? Stadtkasse { get; init; }

    /// <summary>Anträge zu Steuer, Bau und Fest, Bauvorhaben und Fest der Stadt (null: ohne Kasse oder alter Spielstand).</summary>
    public RatsantraegeStand? Ratsantraege { get; init; }

    /// <summary>Befugnisse der Ämter: freier Antrag, Streife, Strafmaß (null: ohne Kasse oder alter Spielstand).</summary>
    public BefugnisseStand? Befugnisse { get; init; }

    /// <summary>Gründe der Meinungen aus Taten (PR #231). Null: alter Spielstand – die Gunst der Ratsherren (<see cref="RatStand.Gunst"/>) wird zum Grund „Geschenke“.</summary>
    public MeinungenStand? Meinungen { get; init; }
}

/// <summary>Die Stadtkasse (siehe <see cref="Reichsgilde.Core.Politik.Stadtkasse"/>).</summary>
public sealed record StadtkasseStand
{
    public double Stand { get; init; }
    public int SteuersatzProzent { get; init; }
    public KassenbuchStand DiesesJahr { get; init; } = new();
    public KassenbuchStand? Vorjahr { get; init; }
}

public sealed record KassenbuchStand
{
    public int Jahr { get; init; }
    public Dictionary<Politik.Kassenposten, double> Posten { get; init; } = new();
}

/// <summary>Anträge zu Steuer, Bau und Fest (siehe <see cref="Reichsgilde.Core.Politik.Ratsantraege"/>).</summary>
public sealed record RatsantraegeStand
{
    public List<RatsantragStand> Antraege { get; init; } = new();
    public List<RatsantragStimmeStand> Stimmen { get; init; } = new();
    public List<StadtbaustelleStand> Baustellen { get; init; } = new();
    public List<string> Gebaut { get; init; } = new();
    public List<AntragAnsehenStand> Ansehen { get; init; } = new();
    public int? FestJahr { get; init; }
}

public sealed record RatsantragStand
{
    public Politik.Antragsart Art { get; init; }
    public int Konto { get; init; }
    public int Prozent { get; init; }
    public string Bauvorhaben { get; init; } = "";
}

public sealed record RatsantragStimmeStand
{
    public Politik.Antragsart Art { get; init; }
    public int Konto { get; init; }
    public bool Ja { get; init; }
}

public sealed record StadtbaustelleStand
{
    public string Kennung { get; init; } = "";
    public int? Konto { get; init; }
    public long FertigAb { get; init; }
    public double Kosten { get; init; }
}

public sealed record AntragAnsehenStand
{
    public int Konto { get; init; }
    public double Wert { get; init; }
    public int Bis { get; init; }
}

/// <summary>Befugnisse der Ämter (siehe <see cref="Reichsgilde.Core.Politik.Amtsbefugnisse"/>).</summary>
public sealed record BefugnisseStand
{
    public int? FreierAntragKonto { get; init; }
    public int FreierAntragJahr { get; init; }
    public int? StreifeKonto { get; init; }
    public Politik.Stadtviertel? StreifeViertel { get; init; }
    public List<StrafmassStand> Strafmass { get; init; } = new();
}

public sealed record StrafmassStand
{
    public int Anklaeger { get; init; }
    public int Beklagter { get; init; }
    public int Richter { get; init; }
    public int Prozent { get; init; }
}

/// <summary>Der Schiffshandel (siehe <see cref="Reichsgilde.Core.Wirtschaft.Schiffahrt"/>).</summary>
public sealed record SchiffahrtStand
{
    public List<SchiffStand> Schiffe { get; init; } = new();

    /// <summary>Je ferner Stadt der Druck je Ware (gehandelte Stück, die ihr Markt noch nicht verdaut hat).</summary>
    public Dictionary<string, Dictionary<string, double>> Druck { get; init; } = new();

    /// <summary>So viele Schiffe wurden schon getauft (für den Namen des nächsten).</summary>
    public int Getauft { get; init; }
}

public sealed record SchiffStand
{
    public string Name { get; init; } = "";
    public string Typ { get; init; } = "";

    /// <summary>Nummer des Kontos in <see cref="WirtschaftStand.Konten"/>.</summary>
    public int Konto { get; init; }

    public string Hafen { get; init; } = "";
    public SchiffZustand Zustand { get; init; }
    public long Beginn { get; init; }
    public long Ende { get; init; }
    public Dictionary<string, int> Ladung { get; init; } = new();
    public string Geleit { get; init; } = "Keine";

    /// <summary>Der Auftrag: ferne Stadt (null: kein Auftrag), Betrieb, Hin- und Rückfracht, Wiederholen.</summary>
    public string? Ziel { get; init; }

    public string? Betrieb { get; init; }
    public Dictionary<string, int>? Hinfracht { get; init; }
    public Dictionary<string, int>? Rueckfracht { get; init; }
    public bool Wiederholen { get; init; }
    public double ErtragLaufend { get; init; }
    public double? ErtragVorjahr { get; init; }
    public int Fahrten { get; init; }
}

/// <summary>Der Belag der Wege im Spiel (siehe <see cref="Reichsgilde.Core.Bauen.Wegebau"/>).</summary>
public sealed record WegebauStand
{
    /// <summary>Je Weg der Karte die Stücke als Läufe gleicher Werte (vom Anfang des Weges an).</summary>
    public List<List<WegLaufStand>> Wege { get; init; } = new();

    /// <summary>Einfache Karrenfahrten im laufenden Jahr je Betrieb.</summary>
    public Dictionary<string, int> Fahrten { get; init; } = new();

    public Dictionary<string, int> FahrtenVorjahr { get; init; } = new();
    public List<WegbaustelleStand> Baustellen { get; init; } = new();
    public double Wegekasse { get; init; }
    public int? AntragKonto { get; init; }
    public int AntragWeg { get; init; }
    public double AntragVon { get; init; }
    public double AntragBis { get; init; }

    /// <summary>Kontonummer → bis zu welchem Jahr das Ansehen aus einem Antrag „Weg pflastern“ gilt.</summary>
    public Dictionary<string, int> AnsehenBis { get; init; } = new();
}

/// <summary><see cref="Anzahl"/> Stücke hintereinander mit gleichen Werten.</summary>
public sealed record WegLaufStand
{
    public int Anzahl { get; init; }
    public Bauen.Belagstufe Stufe { get; init; }
    public double Austritt { get; init; }
    public double Zustand { get; init; }
    public double Karren { get; init; }
}

/// <summary>Ein laufender Bauauftrag an einem Weg (Bauherr: Kontonummer, null = die Stadt).</summary>
public sealed record WegbaustelleStand
{
    public int Weg { get; init; }
    public double Von { get; init; }
    public double Bis { get; init; }
    public int VonStueck { get; init; }
    public int BisStueck { get; init; }
    public Bauen.Belagstufe Stufe { get; init; }
    public int? Bauherr { get; init; }
    public long FertigAb { get; init; }
    public double Kosten { get; init; }
}

/// <summary>Die Grundstücke des Bauwesens (siehe <see cref="Reichsgilde.Core.Bauen.Bauwesen"/>).</summary>
public sealed record BauenStand
{
    public int NaechsteNummer { get; init; } = 1;
    public List<GrundstueckStand> Grundstuecke { get; init; } = new();
}

/// <summary>Ein Grundstück: Fläche, Art, Besitzer (Kontonummer) und eine laufende Baustelle.</summary>
public sealed record GrundstueckStand
{
    public int Nummer { get; init; }
    public double X { get; init; }
    public double Z { get; init; }
    public double Breite { get; init; }
    public double Tiefe { get; init; }
    public int Drehung { get; init; }
    public Bauen.Grundstuecksart Art { get; init; }
    public Bauen.Lage? Lage { get; init; }
    public string? Kennung { get; init; }
    public int? Besitzer { get; init; }
    public int Stufe { get; init; }
    public string? Betrieb { get; init; }
    public Betriebsart? Betriebsart { get; init; }
    public Bauen.Bauarbeit? Arbeit { get; init; }
    public long? FertigAb { get; init; }

    /// <summary>Leerstand: seit dieser Spielminute (null: kein Leerstand – oder ein alter Spielstand, dann zählt er ab dem Laden).</summary>
    public long? LeerSeit { get; init; }
}

/// <summary>Ein verkaufter Betrieb, der wieder frei ist (siehe <see cref="Stadtwirtschaft.FreieLiegenschaften"/>).</summary>
public sealed record FreieLiegenschaftStand
{
    public string Name { get; init; } = "";
    public Betriebsart Art { get; init; }
    public double Kaufpreis { get; init; }
    public string MindestStand { get; init; } = "";

    /// <summary>Einrichtung, die im Haus geblieben ist (null: keine).</summary>
    public List<string>? Einrichtung { get; init; }
}

public sealed record GlaubeStand
{
    public long ZufallWuerfe { get; init; }
    public Dictionary<Glaube.Konfession, double> Anteile { get; init; } = new();
    public Glaube.Konfession? Stadtglaube { get; init; }
    public int? AntragKonto { get; init; }
    public Glaube.Konfession? AntragGlaube { get; init; }
    public Dictionary<string, int> KonvertiertBis { get; init; } = new();
    public Dictionary<string, int> BekehrtIm { get; init; } = new();
    public Dictionary<string, double> AbgabenVorjahr { get; init; } = new();

    /// <summary>Kontonummer (als Text) → Stimme zum Antrag auf einen Stadtglauben (true = dafür; ältere Spielstände: keine).</summary>
    public Dictionary<string, bool> Stimmen { get; init; } = new();
}

public sealed record ParfumStand
{
    public long ZufallWuerfe { get; init; }
    public List<GiftauftragStand> Auftraege { get; init; } = new();

    /// <summary>„Kontonummer|K:Kandidat" bzw. „Kontonummer|R:Ratsherr" – Moschus dieses Jahr schon geschenkt.</summary>
    public List<string> Geschenkt { get; init; } = new();
    public Dictionary<string, int> HeiligBis { get; init; } = new();
    public Dictionary<string, int> Anschlaege { get; init; } = new();
}

public sealed record GiftauftragStand
{
    public int Auftraggeber { get; init; }
    public int Ziel { get; init; }
    public string Dolch { get; init; } = "";
}

public sealed record BankStand
{
    public long ZufallWuerfe { get; init; }
    public int NaechsterKredit { get; init; }
    public Dictionary<string, double> Kassen { get; init; } = new();
    public Dictionary<string, double> Zinssaetze { get; init; } = new();
    public List<KreditStand> Kredite { get; init; } = new();
    public Dictionary<string, Bank.Bankbuch> DiesesJahr { get; init; } = new();
    public Dictionary<string, Bank.Bankbuch> Vorjahr { get; init; } = new();
}

public sealed record KreditStand
{
    public int Nummer { get; init; }
    public int Geber { get; init; }
    public int Nehmer { get; init; }
    public double Betrag { get; init; }
    public double Rest { get; init; }
    public double Satz { get; init; }
    public int JahreOffen { get; init; }
    public double Aufgelaufen { get; init; }
    public int Jahr { get; init; }
}

public sealed record WacheStand
{
    public long ZufallWuerfe { get; init; }
    public Dictionary<string, Ordnung.Wachbuch> DiesesJahr { get; init; } = new();
    public Dictionary<string, Ordnung.Wachbuch> Vorjahr { get; init; } = new();
}

public sealed record DiebeStand
{
    public long ZufallWuerfe { get; init; }
    public List<DiebesauftragStand> Auftraege { get; init; } = new();
    public List<DiebesbriefStand> Briefe { get; init; } = new();

    /// <summary>Konto-Nummer → Beute und Fänge der Taschendiebe.</summary>
    public Dictionary<string, Gauner.Diebesbuch> DiesesJahr { get; init; } = new();
    public Dictionary<string, Gauner.Diebesbuch> Vorjahr { get; init; } = new();
}

public sealed record DiebesauftragStand
{
    public int Dieb { get; init; }
    public int Ziel { get; init; }
    public Gauner.Diebesstueck Art { get; init; }
    public string? Betrieb { get; init; }
    public int? Person { get; init; }
    public double Betrag { get; init; }

    /// <summary>
    /// Hatte der Auftrag in dieser Nacht schon Erfolg (Gaunerei je Nachtstunde)? Sonst fiele ein misslungener späterer Versuch nach dem Laden
    /// anders auf als ohne Speichern. Fehlt in alten Spielständen (false – wie dort beim Laden).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Erfolgreich { get; init; }
}

public sealed record DiebesbriefStand
{
    public Gauner.Diebesbriefart Art { get; init; }
    public int Dieb { get; init; }
    public int Opfer { get; init; }
    public double Betrag { get; init; }
    public int Jahr { get; init; }
    public int? Geisel { get; init; }
}

public sealed record KirchenStand
{
    public long ZufallWuerfe { get; init; }
    public Dictionary<string, Kirche.Kirchenbuch> DiesesJahr { get; init; } = new();
    public Dictionary<string, Kirche.Kirchenbuch> Vorjahr { get; init; } = new();

    /// <summary>[Konto des Predigers, Nummer der Spur]</summary>
    public List<int[]> Beichtwissen { get; init; } = new();

    /// <summary>Konto-Nummer → Jahr, in dem das Oberhaupt eine Lehrschrift las.</summary>
    public Dictionary<string, int> Gelesen { get; init; } = new();

    /// <summary>Konto-Nummer → bis zu diesem Jahr wirkt eine Hetzschrift.</summary>
    public Dictionary<string, int> GehetztBis { get; init; } = new();
}

public sealed record GastwirtschaftStand
{
    public Dictionary<string, double> Offen { get; init; } = new();
    public Dictionary<string, Wirtschaft.Gaestebuch> DiesesJahr { get; init; } = new();
    public Dictionary<string, Wirtschaft.Gaestebuch> Vorjahr { get; init; } = new();
    public List<string> Barde { get; init; } = new();
    public List<string> Wuerfeltisch { get; init; } = new();
}

public sealed record UnterweltStand
{
    public long ZufallWuerfe { get; init; }
    public List<AuftragStand> Auftraege { get; init; } = new();
    public List<HinterhaltStand> Hinterhalte { get; init; } = new();
    public List<ForderungStand> Forderungen { get; init; } = new();
    public List<NachtereignisStand> DiesesJahr { get; init; } = new();
    public List<NachtereignisStand> LetztesJahr { get; init; } = new();

    /// <summary>Betriebsname → Jahr des letzten Angriffs.</summary>
    public Dictionary<string, int> LetzterAngriff { get; init; } = new();
}

public sealed record AuftragStand
{
    public int Auftraggeber { get; init; }
    public int Ziel { get; init; }
    public Gauner.Gaunerstueck Art { get; init; }
    public string? Betrieb { get; init; }

    /// <summary>
    /// Hatte der Auftrag in dieser Nacht schon Erfolg (Gaunerei je Nachtstunde)? Sonst fiele ein misslungener späterer Versuch nach dem Laden
    /// anders auf als ohne Speichern. Fehlt in alten Spielständen (false – wie dort beim Laden).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Erfolgreich { get; init; }
}

public sealed record HinterhaltStand
{
    public int Auftraggeber { get; init; }
    public int Ziel { get; init; }
    public long BisMinute { get; init; }
}

public sealed record ForderungStand
{
    public int Erpresser { get; init; }
    public int Opfer { get; init; }
    public double Betrag { get; init; }
    public int Jahr { get; init; }
}

public sealed record NachtereignisStand
{
    public int? Fuer { get; init; }
    public string Text { get; init; } = "";
}

public sealed record JustizStand
{
    public long ZufallWuerfe { get; init; }
    public int NaechsteSpur { get; init; }
    public List<SpurStand> Spuren { get; init; } = new();
    public List<BeweisStand> Beweise { get; init; } = new();
    public List<SpitzelStand> Spitzel { get; init; } = new();
    public List<VerfahrenStand> Verfahren { get; init; } = new();

    /// <summary>Kontonummer (als Text) → bis zu welchem Jahr das Haus am Pranger steht.</summary>
    public Dictionary<string, int> PrangerBis { get; init; } = new();

    /// <summary>Kontonummer (als Text) → bis zu welchem Jahr das Oberhaupt im Kerker sitzt.</summary>
    public Dictionary<string, int> KerkerBis { get; init; } = new();
    public List<string> LetzterGerichtstag { get; init; } = new();

    /// <summary>Häuser, über die am letzten Gerichtstag ein Spitzel berichtet hat (Kontonummern).</summary>
    public List<int> Beobachtet { get; init; } = new();
    public List<UrteilStand> LetzteUrteile { get; init; } = new();
}

public sealed record SpurStand
{
    public int Nummer { get; init; }
    public int Taeter { get; init; }
    public Justiz.Vergehen Art { get; init; }
    public string Betroffener { get; init; } = "";
    public double Betrag { get; init; }
    public int Jahr { get; init; }
}

public sealed record BeweisStand
{
    public int Inhaber { get; init; }
    public int Spur { get; init; }
}

public sealed record SpitzelStand
{
    public int Auftraggeber { get; init; }
    public int Ziel { get; init; }
}

public sealed record VerfahrenStand
{
    public int Anklaeger { get; init; }
    public int Beklagter { get; init; }
    public int Jahr { get; init; }
    public List<RichterStand> Richter { get; init; } = new();
}

public sealed record RichterStand
{
    public string Rolle { get; init; } = "";
    public string? Ratsherr { get; init; }
    public int? Konto { get; init; }
}

public sealed record UrteilStand
{
    public int Anklaeger { get; init; }
    public int Beklagter { get; init; }
    public bool Schuldig { get; init; }
    public int Schuldstimmen { get; init; }
    public string Text { get; init; } = "";
}

public sealed record FamilienbuchStand
{
    public int Jahr { get; init; }
    public long ZufallWuerfe { get; init; }
    public int NaechsteNummer { get; init; }
    public long TalentWuerfe { get; init; }

    /// <summary>Würfe für das Aussehen der Personen (0 in Spielständen von vor den Porträts).</summary>
    public long AussehenWuerfe { get; init; }

    /// <summary>Würfe für erworbene Charakterzüge (0 in Spielständen von vor den Charakterzügen, PR #227).</summary>
    public long ZugWuerfe { get; init; }

    /// <summary>Version der Gen-Vorlage (<c>erbgut.json</c>) beim Speichern – null in Spielständen von vor dem Erbgut.</summary>
    public int? ErbgutVersion { get; init; }

    /// <summary>
    /// Die Kennungen der Gene in der Reihenfolge, in der <see cref="PersonStand.Erbgut"/> sie schreibt (null vor dem Erbgut). Beim Laden
    /// fallen Gene weg, die die Vorlage nicht mehr kennt; fehlende entstehen aus dem Startwert der Person.
    /// </summary>
    public List<string>? ErbgutGene { get; init; }
    public List<PersonStand> Personen { get; init; } = new();
    public List<FamilieStand> Familien { get; init; } = new();
    public List<KandidatStand> Kandidaten { get; init; } = new();
    public List<ZuneigungStand> Zuneigung { get; init; } = new();
    public List<EreignisStand> DiesesJahr { get; init; } = new();
    public List<EreignisStand> LetztesJahr { get; init; } = new();
}

public sealed record PersonStand
{
    public int Nummer { get; init; }
    public string Vorname { get; init; } = "";
    public string Familienname { get; init; } = "";
    public string Geburtsname { get; init; } = "";
    public Volk Volk { get; init; }
    public Geschlecht Geschlecht { get; init; }
    public int Geburtsjahr { get; init; }
    public int? Todesjahr { get; init; }
    public int? Vater { get; init; }
    public int? Mutter { get; init; }

    /// <summary>Sterne je Talent in der Reihenfolge des Talent-Enums (null in alten Spielständen – dann Grundwerte des Volkes).</summary>
    public List<double>? Talente { get; init; }
    public string? Ausbildung { get; init; }
    public string? Abschluss { get; init; }

    /// <summary>Jahr, in dem jedes Talent zuletzt geübt wurde (0 = nie; null in alten Spielständen).</summary>
    public List<int>? Geuebt { get; init; }

    public ErkrankungStand? Krankheit { get; init; }
    public string? Todesursache { get; init; }

    /// <summary>Glaube heute und Glaube der Herkunft (null in Spielständen von vor dem Glauben – dann der Startglaube des Volkes).</summary>
    public Glaube.Konfession? Glaube { get; init; }
    public Glaube.Konfession? Herkunftsglaube { get; init; }

    /// <summary>Aussehen im Porträt (null in Spielständen von vor den Porträts – dann fest aus Nummer und Volk).</summary>
    public int? Gesicht { get; init; }
    public Dynastie.Haarfarbe? Haar { get; init; }
    public Dynastie.Augenfarbe? Augen { get; init; }

    /// <summary>
    /// Erbgut für den 3D-Kopf: je Gen aus <see cref="FamilienbuchStand.ErbgutGene"/> vier Hexadezimalziffern, erst das sichtbare, dann das
    /// verdeckte Allel („8A7F…“). null in Spielständen von vor dem Erbgut – dann beim Laden vererbt oder gewürfelt, fest aus dem Startwert.
    /// </summary>
    public string? Erbgut { get; init; }

    /// <summary>
    /// Kennungen der Charakterzüge (PR #227). null in Spielständen von vor den Charakterzügen – dann werden sie beim Laden
    /// nachgewürfelt, fest nach Personennummer (wie bei einer neuen Partie mit demselben Startwert).
    /// </summary>
    public List<string>? Zuege { get; init; }
}

public sealed record ErkrankungStand
{
    public string Name { get; init; } = "";
    public int Bis { get; init; }
    public bool Medicus { get; init; }
    public bool Schnaps { get; init; }
}

public sealed record GesundheitStand
{
    public long ZufallWuerfe { get; init; }
    public int NaechstePest { get; init; }
    public bool Pestjahr { get; init; }

    /// <summary>Kräuterschnaps in der Hausapotheke, je Konto (Index).</summary>
    public Dictionary<int, int> Apotheke { get; init; } = new();
}

public sealed record FamilieStand
{
    public int Konto { get; init; }
    public Volk Volk { get; init; }
    public string Nachname { get; init; } = "";
    public int Oberhaupt { get; init; }
    public int? Ehepartner { get; init; }
    public int? Testament { get; init; }
    public List<int> Mitglieder { get; init; } = new();
    public List<int> Ahnen { get; init; } = new();
}

public sealed record KandidatStand
{
    public int Person { get; init; }
    public int Stand { get; init; }
}

public sealed record ZuneigungStand
{
    public int Konto { get; init; }
    public int Kandidat { get; init; }
    public double Wert { get; init; }
    public double DiesesJahr { get; init; }
}

public sealed record EreignisStand
{
    public int Konto { get; init; }
    public string Text { get; init; } = "";
}

public sealed record RatStand
{
    /// <summary>Amt → wer es hält.</summary>
    public Dictionary<string, SitzStand> Aemter { get; init; } = new();
    public List<GunstStand> Gunst { get; init; } = new();

    /// <summary>Kontonummer (als Text) → Amt, um das sich das Haus bewirbt.</summary>
    public Dictionary<string, string> Bewerbungen { get; init; } = new();
    public int? AntragKonto { get; init; }
    public Fronstatut? AntragStatut { get; init; }
    public List<string> LetzteSitzung { get; init; } = new();

    /// <summary>Festgelegte Stimmen bei Wahlen (ältere Spielstände: keine).</summary>
    public List<WahlStimmeStand> WahlStimmen { get; init; } = new();

    /// <summary>Kontonummer (als Text) → Stimme zum Antrag auf ein Statut (true = dafür; ältere Spielstände: keine).</summary>
    public Dictionary<string, bool> StatutStimmen { get; init; } = new();
}

/// <summary>Eine festgelegte Wahlstimme: Wähler, Amt und entweder ein Haus (Kandidat) oder der Ratsherr im Amt.</summary>
public sealed record WahlStimmeStand
{
    public int Konto { get; init; }
    public string Amt { get; init; } = "";
    public int? Kandidat { get; init; }
    public string? Ratsherr { get; init; }
}

public sealed record SitzStand
{
    public string? Ratsherr { get; init; }
    public int? Konto { get; init; }
}

/// <summary>Die Gunst eines Ratsherrn bei einem Haus – nur noch in alten Spielständen (seit PR #231 ein Grund der Meinung).</summary>
public sealed record GunstStand
{
    public string Ratsherr { get; init; } = "";
    public int Konto { get; init; }
    public double Wert { get; init; }
}

/// <summary>Die Meinungen (PR #231): nur die verblassenden Gründe aus Taten – die festen werden beim Laden neu berechnet.</summary>
public sealed record MeinungenStand
{
    public List<MeinungsgrundStand> Gruende { get; init; } = new();

    /// <summary>Aktivitäten dieses Jahres (je Art und Paar einmal; Geschenke mit Anzahl).</summary>
    public List<MeinungJahrStand> DiesesJahr { get; init; } = new();

    /// <summary>Kontonummer (als Text) → Streit im Haus kostet Ansehen bis zu diesem Jahr.</summary>
    public Dictionary<string, int> StreitBis { get; init; } = new();

    /// <summary>Würfe des eigenen Zufallsgenerators (Gefahr der Aktivitäten, Streit).</summary>
    public long Wuerfe { get; init; }
}

/// <summary>Ein Grund aus einer Tat: <see cref="Von"/> denkt über <see cref="Ueber"/> (Kennungen „P12“, „R:Name“, „K:Haus“).</summary>
public sealed record MeinungsgrundStand
{
    public string Von { get; init; } = "";
    public string Ueber { get; init; } = "";
    public string Art { get; init; } = "";
    public string Text { get; init; } = "";
    public double Wert { get; init; }
}

public sealed record MeinungJahrStand
{
    public string Art { get; init; } = "";
    public string Von { get; init; } = "";
    public string Ueber { get; init; } = "";
    public int Anzahl { get; init; }
}

public sealed record KontoStand
{
    public string Inhaber { get; init; } = "";

    /// <summary>Wappen des Hauses (null in Spielständen von vor dem Wappen – dann aus dem Namen gewürfelt).</summary>
    public Dynastie.Wappen? Wappen { get; init; }
    public double Vermoegen { get; init; }

    /// <summary>Verliehenes Geld und offene Kredite (fehlen in Spielständen von vor dem Geldleiher – dann 0).</summary>
    public double Forderungen { get; init; }
    public double Schulden { get; init; }
    public int Stand { get; init; }
    public int Schuldenjahre { get; init; }
    public bool IstBankrott { get; init; }
    public bool IstErloschen { get; init; }
    public int Aktionspunkte { get; init; }

    /// <summary>Stufe des Wohnhauses (null in Spielständen von vor dem Wohnhaus – dann die Stufe, die der Stand verlangt).</summary>
    public int? WohnhausStufe { get; init; }

    /// <summary>Laufender Ausbau des Wohnhauses: Spielminute, ab der die nächste Stufe gilt.</summary>
    public long? WohnhausFertigAb { get; init; }

    /// <summary>Nach einem Brand: Spielminute, bis zu der das Wohnhaus instand gesetzt wird.</summary>
    public long? WohnhausInstandBis { get; init; }

    /// <summary>Hauswächter am Wohnhaus (fehlt in alten Spielständen = keiner).</summary>
    public bool Hauswaechter { get; init; }

    /// <summary>Gekaufte Einrichtung des Wohnhauses (Kennungen aus „wohnen.einrichtung“, PR #224; null = keine).</summary>
    public List<string>? WohnhausEinrichtung { get; init; }

    /// <summary>Zuschlag auf das Ansehen aus dem Entwicklerfenster (fehlt = 0, nur geschrieben, wenn gesetzt).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double EntwicklerAnsehen { get; init; }

    /// <summary>Beherrschte Handwerke (null in alten Spielständen – dann die Berufe der eigenen Betriebe).</summary>
    public List<string>? Berufe { get; init; }

    /// <summary>Meistergrad je Beruf (fehlt in alten Spielständen – dann Grad 1).</summary>
    public Dictionary<string, int>? Meistergrade { get; init; }

    /// <summary>Gesammelte Erfahrungsstunden je Beruf.</summary>
    public Dictionary<string, double>? Erfahrung { get; init; }
    public BilanzStand Laufend { get; init; } = new();
    public BilanzStand? Vorjahr { get; init; }
}

public sealed record BilanzStand
{
    public int Jahr { get; init; }
    public double Umsatz { get; init; }
    public double Zutaten { get; init; }
    public double Personal { get; init; }
    public double Fixkosten { get; init; }
    public double Steuer { get; init; }
    public double Schaden { get; init; }
    public double Transport { get; init; }
    public double Raubwert { get; init; }
    public double Investitionen { get; init; }
    public double Standesgebuehren { get; init; }
    public double Amtseinkommen { get; init; }
    public double Geschenke { get; init; }
    public double Familie { get; init; }
    public double Gericht { get; init; }
    public double Gaunerei { get; init; }
    public double Zinsen { get; init; }

    /// <summary>Erlös aus verkauften Betrieben (0 in alten Spielständen).</summary>
    public double Verkaeufe { get; init; }
    public Dictionary<string, int> Hergestellt { get; init; } = new();
}

public sealed record BetriebStand
{
    public string Name { get; init; } = "";
    public Betriebsart Art { get; init; }

    /// <summary>Nummer des Besitzer-Kontos in der Kontenliste.</summary>
    public int Konto { get; init; }

    public Dictionary<string, int> Belegung { get; init; } = new();
    public Dictionary<string, int> Lager { get; init; } = new();
    public string Produkt { get; init; } = "";
    public double Silberanteil { get; init; }
    public double Fortschritt { get; init; }
    public double FortschrittEisen { get; init; }
    public double FortschrittSilber { get; init; }
    public string? Stillstand { get; init; }
    public string? LetzterStillstand { get; init; }
    public int StillstandMinuten { get; init; }
    public int StillstandMinutenVorjahr { get; init; }
    public List<KarrenStand> Karren { get; init; } = new();

    /// <summary>Waren, die der Karren nicht von selbst verkauft (null: keine; alte Spielstände).</summary>
    public List<string>? Zurueckgehalten { get; init; }
    public bool Waechter { get; init; }
    public int SabotageMinuten { get; init; }
    public int UmbauMinuten { get; init; }

    /// <summary>Rechnung des Betriebs im laufenden Jahr und im Vorjahr (null: alte Spielstände bzw. erstes Jahr).</summary>
    public BetriebsrechnungStand? Rechnung { get; init; }
    public BetriebsrechnungStand? RechnungVorjahr { get; init; }

    /// <summary>Die einzelnen Arbeiter (null: alter Spielstand – dann aus <see cref="Belegung"/>, alle an der Ware des Betriebs, Laune neutral).</summary>
    public List<ArbeiterStand>? Arbeiter { get; init; }
    public int? NaechsteArbeiterNummer { get; init; }

    /// <summary>Werkstatt: Fortschritt je Ware (null: alter Spielstand – dann gehört <see cref="Fortschritt"/> dem <see cref="Produkt"/>).</summary>
    public Dictionary<string, double>? FortschrittJeWare { get; init; }

    /// <summary>Lohnstufe (null: alter Spielstand, üblich) und ihr Mittel über die Arbeitsminuten des laufenden Jahres.</summary>
    public Lohnstufe? Lohnstufe { get; init; }
    public double LohnFaktorSumme { get; init; }
    public int LohnMinuten { get; init; }

    /// <summary>Gekaufte Einrichtung (Kennungen; null: keine).</summary>
    public List<string>? Einrichtung { get; init; }

    /// <summary>Nach einem Unfall ist die Laune im Betrieb bis zu dieser Spielminute gedrückt.</summary>
    public long? UnfallLauneBis { get; init; }

    /// <summary>Gesparte Zutaten des Vorarbeiters „Sparsam“, noch kein ganzes Stück (PR #224; null: nichts).</summary>
    public Dictionary<string, double>? RohstoffErsparnis { get; init; }
}

/// <summary>Ein Arbeiter eines Betriebs (siehe <see cref="Wirtschaft.Arbeiter"/>).</summary>
public sealed record ArbeiterStand
{
    public int Nummer { get; init; }
    public string Arbeitskraft { get; init; } = "";
    public string? Ware { get; init; }
    public double Laune { get; init; } = 50;
    public long? AusfallBis { get; init; }
    public long EingestelltUm { get; init; }
    public long? PraemieBis { get; init; }
    public int UnzufriedenStunden { get; init; }

    /// <summary>
    /// Name, Volk, Geschlecht, Aussehen und Alter beim Einstellen (PR #224; null in älteren Spielständen – dann fest aus Betrieb und Nummer
    /// gewürfelt, wie beim Einstellen).
    /// </summary>
    public string? Name { get; init; }
    public Volk? Volk { get; init; }
    public Dynastie.Geschlecht? Geschlecht { get; init; }
    public int? Gesicht { get; init; }
    public Dynastie.Haarfarbe? Haar { get; init; }
    public Dynastie.Augenfarbe? Augen { get; init; }
    public double? AlterBeimEinstellen { get; init; }

    /// <summary>Erfahrung in Stunden (null in älteren Spielständen – dann die Schwelle seiner Stufe: Ein Geselle bleibt Geselle mit voller Erfahrung).</summary>
    public double? Erfahrung { get; init; }

    /// <summary>Eigenschaft des Vorarbeiters und die gezogenen zur Wahl (fehlen sonst).</summary>
    public string? Eigenschaft { get; init; }
    public List<string>? EigenschaftWahl { get; init; }
}

/// <summary>Die Rechnung eines Betriebs (siehe <see cref="Betriebsrechnung"/>).</summary>
public sealed record BetriebsrechnungStand
{
    public int Jahr { get; init; }
    public bool Ganzjaehrig { get; init; }
    public double Umsatz { get; init; }
    public double Zutaten { get; init; }
    public double Personal { get; init; }
    public double Fixkosten { get; init; }
    public double Steuer { get; init; }
    public double Schaden { get; init; }
    public double Transport { get; init; }

    /// <summary>Von Dieben geholt (Ware bzw. Kasse) und Schaden durch Feuer – nur zur Information (0 in alten Spielständen).</summary>
    public double Raub { get; init; }
    public double Brandschaden { get; init; }
}

public sealed record KarrenStand
{
    public string Name { get; init; } = "";
    public string Typ { get; init; } = "";
    public KarrenZustand Zustand { get; init; }
    public long Ankunft { get; init; }
    public Dictionary<string, int> Ladung { get; init; } = new();
    public string Eskorte { get; init; } = "Keine";
    public bool NachtsFahren { get; init; }

    /// <summary>Wartet am geschlossenen Stadttor bis zu dieser Spielminute (null: wartet nicht; fehlt in alten Spielständen).</summary>
    public long? TorWartenBis { get; init; }

    /// <summary>Der Rückweg ins Umland beginnt erst, wenn das Tor aufgeht.</summary>
    public bool RueckfahrtWartet { get; init; }
    public long? WareWartetSeit { get; init; }

    /// <summary>Einkäufe eines Handelsauftrags, solange der Karren zum Markt unterwegs ist (null: kein Auftrag).</summary>
    public Dictionary<string, int>? Auftrag { get; init; }

    /// <summary>Vorgemerkter Handelsauftrag (null: keiner).</summary>
    public Dictionary<string, int>? VormerkungVerkauf { get; init; }
    public Dictionary<string, int>? VormerkungKauf { get; init; }

    /// <summary>Fahrten zum Markt in diesem Jahr (PR #205; alte Spielstände: 0).</summary>
    public int FahrtenImJahr { get; init; }

    /// <summary>Fahrten zum Markt im Vorjahr (PR #205; null: das Vorjahr nicht ganz gezählt – zählt dann nicht als ungenutzt).</summary>
    public int? FahrtenVorjahr { get; init; }

    /// <summary>Zählt <see cref="FahrtenImJahr"/> seit Jahresbeginn? (PR #205; alte Spielstände: nein – das laufende Jahr zählt dann nicht)</summary>
    public bool FahrtenGanzesJahr { get; init; }
}

public sealed record MarktstandStand
{
    public double[] VerkaufteStueckProStunde { get; init; } = Array.Empty<double>();
    public int AktuellesFach { get; init; }
    public int MinuteInStunde { get; init; }
}

public sealed record RivaleStand
{
    public int Konto { get; init; }
    public KiProfil Profil { get; init; } = KiProfil.Standard;

    /// <summary>Betriebsname → Stunden seit der letzten Umstellung.</summary>
    public Dictionary<string, int> StundenSeitWechsel { get; init; } = new();

    public List<string> Entscheidungen { get; init; } = new();
    public int StundenImJahr { get; init; }
    public int GaunereienImJahr { get; init; }

    /// <summary>Auf wen die KI dieses bzw. letztes Jahr einen Spitzel angesetzt hat (Inhaber; Spiel-KI, PR #208 – fehlt in alten Spielständen).</summary>
    public List<string> SpitzelDiesesJahr { get; init; } = new();
    public List<string> SpitzelVorjahr { get; init; } = new();

    /// <summary>Betriebsname → ganze Jahre in Folge mit Verlust (Spiel-KI, PR #108; fehlt in alten Spielständen).</summary>
    public Dictionary<string, int> Verlustjahre { get; init; } = new();

    /// <summary>Betriebsname → Lagerwert zum letzten Jahresende (zum Grundpreis), für die Verlustrechnung der KI.</summary>
    public Dictionary<string, double> Lagerwert { get; init; } = new();

    /// <summary>Betriebsname → Stunden des laufenden Jahres ganz ohne Personal (Spiel-KI; fehlt in alten Spielständen).</summary>
    public Dictionary<string, int> StundenOhnePersonal { get; init; } = new();

    /// <summary>„Beruf/Ort“ eines verkauften Betriebs → bis zu diesem Jahr (ausschließlich) kauft und baut die KI dort nichts.</summary>
    public Dictionary<string, int> Gesperrt { get; init; } = new();

    /// <summary>In diesem Jahr fand die KI keinen Platz für eine neue Werkstatt (null: kein solches Jahr).</summary>
    public int? KeinWerkstattplatzJahr { get; init; }
}
