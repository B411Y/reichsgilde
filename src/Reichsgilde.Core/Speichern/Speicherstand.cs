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
    public const int AktuellesFormat = 1;

    public int Format { get; init; } = AktuellesFormat;

    /// <summary>Wann gespeichert wurde (nur zur Anzeige im Ladedialog).</summary>
    public DateTime Gespeichert { get; init; }

    /// <summary>Kurzbeschreibung für den Ladedialog, z. B. „Familie Hammerschmid · Sommer 1405".</summary>
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
}

public sealed record KontoStand
{
    public string Inhaber { get; init; } = "";
    public double Vermoegen { get; init; }
    public int Stand { get; init; }
    public int Schuldenjahre { get; init; }
    public bool IstBankrott { get; init; }
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
    public long? WareWartetSeit { get; init; }
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
}
