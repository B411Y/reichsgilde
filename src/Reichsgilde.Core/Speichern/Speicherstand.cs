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
    public const int AktuellesFormat = 2; // 2: Spieltag 05–22 Uhr (1020 Minuten) statt 24 Stunden

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
}

public sealed record SitzStand
{
    public string? Ratsherr { get; init; }
    public int? Konto { get; init; }
}

public sealed record GunstStand
{
    public string Ratsherr { get; init; } = "";
    public int Konto { get; init; }
    public double Wert { get; init; }
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
}
