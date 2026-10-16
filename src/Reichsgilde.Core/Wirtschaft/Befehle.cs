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

/// <summary>Das eigene Wohnhaus um eine Stufe ausbauen (z. B. Fachwerkhaus → Bürgerhaus). Kostet Gulden und Bauzeit.</summary>
public sealed record WohnhausAusbauen : Befehl;

/// <summary>Einen Hauswächter fürs Wohnhaus einstellen oder entlassen (kostet wie ein Betriebswächter).</summary>
public sealed record HauswaechterSetzen(bool Angestellt) : Befehl;

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

/// <summary>Ausbildung für ein Kind des Hauses wählen (Name aus "ausbildungen"); null bricht sie ab.</summary>
public sealed record AusbildungWaehlen(int Kind, string? Weg) : Befehl;

/// <summary>Das Oberhaupt übt ein Talent: +½ Stern, höchstens einmal je Talent und Jahr (kostet Aktionspunkte).</summary>
public sealed record TalentTrainieren(Dynastie.Talent Talent) : Befehl;

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

/// <summary>Einen Medicus rufen (kostet Gulden): heilt eine gewöhnliche Krankheit sofort, senkt bei der Pest das Sterberisiko.</summary>
public sealed record MedicusRufen(int Person) : Befehl;

/// <summary>Kräuterschnaps vom Markt in die Hausapotheke kaufen.</summary>
public sealed record SchnapsKaufen(int Stueck) : Befehl;

/// <summary>Einem Familienmitglied Kräuterschnaps aus der Hausapotheke geben: heilt leichte Krankheiten, lindert die Pest.</summary>
public sealed record SchnapsGeben(int Person) : Befehl;

/// <summary>Ein weiteres Handwerk erlernen (Lehrgeld und Aktionspunkte) – erst dann darf man dessen Werkstätten erwerben.</summary>
public sealed record BerufErlernen(string Beruf) : Befehl;

/// <summary>Die Meisterprüfung für den nächsten Grad in diesem Beruf ablegen (genug Erfahrung, Gebühr und Aktionspunkte).</summary>
public sealed record MeisterpruefungAblegen(string Beruf) : Befehl;

/// <summary>In einer fremden Kirche Ablass kaufen: Eine eigene Tat verfällt sofort, mit allen Beweisen dafür.</summary>
public sealed record AblassKaufen(string Kirche) : Befehl;

/// <summary>Der Prediger verrät, was er in der Beichte über dieses Haus gehört hat: Beweise – aber selbst ein Vergehen.</summary>
public sealed record BeichtgeheimnisBrechen(string Haus) : Befehl;

/// <summary>Das Oberhaupt liest eine Lehrschrift (aus dem Lager oder vom Markt): +½ Stern bis 3 Sterne, einmal je Jahr, 1 AP.</summary>
public sealed record LehrschriftLesen(Dynastie.Talent Talent) : Befehl;

/// <summary>Eine Hetzschrift gegen ein Haus verbreiten: dessen Ansehen sinkt bis Ende nächsten Jahres – Vergehen Verleumdung.</summary>
public sealed record HetzschriftVerbreiten(string Haus) : Befehl;

/// <summary>Die eigenen Diebe steigen heute Nacht in diesen Betrieb ein (Beute ins eigene Lager).</summary>
public sealed record Einbrechen(string Haus, string Betrieb) : Befehl;

/// <summary>Die eigenen Diebe spähen heute Nacht dieses Haus aus: Beweise für seine Taten.</summary>
public sealed record Auskundschaften(string Haus) : Befehl;

/// <summary>Schutzgeld von einem Haus verlangen (ab Unterschlupf) – wer nicht zahlt, dem brennt ein Betrieb.</summary>
public sealed record SchutzgeldFordern(string Haus, double Betrag) : Befehl;

/// <summary>Heute Nacht einen Angehörigen (Nummer der Person) verschleppen und Lösegeld fordern (ab Diebesgilde).</summary>
public sealed record Entfuehren(string Haus, int Person, double Betrag) : Befehl;

/// <summary>Auf eine Forderung von Dieben antworten: Schutzgeld oder Lösegeld zahlen – oder nicht.</summary>
public sealed record DiebesforderungBeantworten(string Dieb, Gauner.Diebesbriefart Art, bool Zahlen) : Befehl;

/// <summary>Die eigene Wache durchsucht dieses Haus: Beweise für seine Taten, Diebesgut wird beschlagnahmt.</summary>
public sealed record Razzia(string Haus) : Befehl;

/// <summary>Für heute Abend einen Barden in die Schänke holen (Gulden, keine AP): mehr Gäste, höhere Zeche, doppelte Stammtisch-Gunst.</summary>
public sealed record BardeEinladen(string Betrieb) : BetriebsBefehl(Betrieb);

/// <summary>Einen Würfeltisch im Wirtshaus aufstellen oder abbauen (ab Stufe 2): Geld je Gast, aber weniger Ansehen.</summary>
public sealed record WuerfeltischAufstellen(string Betrieb, bool Aufstellen) : BetriebsBefehl(Betrieb);

/// <summary>Die Werkstatt am Platz zur nächsten Stufe ausbauen (Gulden, Meistergrad; ein halber Tag Umbau).</summary>
public sealed record BetriebAusbauen(string Betrieb) : BetriebsBefehl(Betrieb);

/// <summary>
/// Den Betrieb verkaufen – eine Werkstatt bringt 75 % ihres Werts, eine Umlandpacht wird gekündigt.
/// Personal, Wächter und Karren gehen mit, die Lagerware wird zum Marktpreis −20 % mitverkauft.
/// Danach ist der Betrieb wieder eine freie Liegenschaft (mit seiner Stufe). Vorab: <see cref="Stadtwirtschaft.VerkaufVorschau"/>.
/// </summary>
public sealed record BetriebVerkaufen(string Betrieb) : BetriebsBefehl(Betrieb);

/// <summary>Geldleiher: Bargeld in die Kasse legen – die Schreiber verleihen es an Stadtkunden.</summary>
public sealed record KasseEinlegen(string Betrieb, double Betrag) : BetriebsBefehl(Betrieb);

/// <summary>Geldleiher: Geld aus der Kasse zurück ins Bargeld nehmen.</summary>
public sealed record KasseAbheben(string Betrieb, double Betrag) : BetriebsBefehl(Betrieb);

/// <summary>Geldleiher: Zins je Jahr für Kredite an Häuser (gilt für neue Kredite).</summary>
public sealed record ZinsSetzen(string Betrieb, double Satz) : BetriebsBefehl(Betrieb);

/// <summary>Bei einem Geldleiher Geld leihen.</summary>
public sealed record KreditAufnehmen(string Geldleiher, double Betrag, int Jahre) : Befehl;

/// <summary>Den eigenen Kredit ganz zurückzahlen (Rest und aufgelaufene Zinsen).</summary>
public sealed record KreditTilgen : Befehl;

/// <summary>Einem Heiratskandidaten Moschusduft schenken: Zuneigung über das Jahreslimit hinaus (einmal im Jahr je Kandidat).</summary>
public sealed record MoschusSchenken(int Kandidat) : Befehl;

/// <summary>Einem Ratsherrn Moschusduft schenken: Gunst ohne Bestechungsspur (einmal im Jahr je Ratsherr).</summary>
public sealed record MoschusRatsherr(string Ratsherr) : Befehl;

/// <summary>Die Blume der Zwietracht: Ein Ratsherr verliert Gunst für ein anderes Haus – eine Intrige.</summary>
public sealed record ZwietrachtSaeen(string Ratsherr, string Haus) : Befehl;

/// <summary>Heiligenduft in der Kirche stiften: mehr Ansehen bis Ende des nächsten Jahres.</summary>
public sealed record HeiligenduftStiften : Befehl;

/// <summary>Heute Nacht das Oberhaupt eines anderen Hauses mit einem Dolch (Giftdolch, Schattendolch) vergiften.</summary>
public sealed record Giftanschlag(string Haus, string Dolch) : Befehl;

/// <summary>Einer vergifteten Person Gegengift geben (aus dem eigenen Lager oder vom Markt).</summary>
public sealed record GegengiftGeben(int Person) : Befehl;

/// <summary>Das ganze Haus wechselt die Konfession (für Heiden: die Taufe). Kostet eine Gebühr und für eine Weile Ansehen.</summary>
public sealed record Konvertieren(Glaube.Konfession Ziel) : Befehl;

/// <summary>Prediger: eine Bekehrungspredigt in der eigenen Kirche – mehr Gläubige der eigenen Konfession, Gunst bei Glaubensbrüdern im Rat.</summary>
public sealed record Bekehrungspredigt : Befehl;

/// <summary>Im Rat einen Stadtglauben beantragen (null = Glaubensfreiheit).</summary>
public sealed record StadtglaubeBeantragen(Reichsgilde.Core.Glaube.Konfession? Glaube) : Befehl;

/// <summary>
/// Handel von Hand: Der Karren Nr. <paramref name="Karren"/> (0 = erster) lädt die Verkaufsware aus dem Lager, fährt zum Markt,
/// verkauft sie und kauft die Einkäufe (Marktpreis + Händlerspanne, jedes Stück hebt den Preis). Nur wenn er am Betrieb
/// wartet und der Markt bei seiner Ankunft offen hat.
/// </summary>
public sealed record Handelsauftrag(string Betrieb, int Karren, IReadOnlyDictionary<string, int> Verkauf, IReadOnlyDictionary<string, int> Kauf)
    : BetriebsBefehl(Betrieb);

/// <summary>Den vorgemerkten Handelsauftrag eines Karrens verwerfen (frei).</summary>
public sealed record HandelsauftragVerwerfen(string Betrieb, int Karren) : BetriebsBefehl(Betrieb);

/// <summary>Eine Ware im Lager zurückhalten: Der Karren verkauft sie nicht von selbst (frei).</summary>
public sealed record WareZurueckhalten(string Betrieb, string Ware, bool Zurueckhalten) : BetriebsBefehl(Betrieb);
