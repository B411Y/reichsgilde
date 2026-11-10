using System.Text.Json;
using System.Text.Json.Serialization;
using Reichsgilde.Core;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die <b>Rechnung der dynamischen Musik</b> ohne Godot (damit sie sich in den Tests prüfen lässt; <c>MusikTests</c>): welches Stück zu welcher Lage,
/// wann Pause ist, wie sich Ereignisse vor die Grundmusik schieben. Der Knoten <see cref="Musikspieler"/> führt die Befehle aus (zwei Spieler zum
/// Überblenden, Ducking, Laden der Dateien). Nichts hier berührt den Kern: Zufall nur aus dem Zufall der Oberfläche (eigene <see cref="Random"/>-Quelle
/// des Aufrufers), die Lage kommt aus den Konstanten der <see cref="Spielzeit"/>.
///
/// <para><b>Regeln</b> (Nutzer, 10.11.2026):
/// 1. Tag: im Wechsel die Stücke ohne Jahreszeit und Volk, die der laufenden Jahreszeit und das des Volkes, dem das Oberhaupt des eigenen Hauses angehört.
/// 2. Nacht (ab <see cref="Spielzeit"/>-Tagesphase „Dunkle Nacht“ bis zum Morgen): die Nachtstücke.
/// 3. Nach jedem Stück eine Pause (<see cref="PauseSekunden"/>), nie dasselbe Stück zweimal hintereinander.
/// 4. Wechsel der Lage blendet 4 bis 8 Sekunden über; ein Ereignis blendet die Grundmusik aus und danach wieder ein.
/// 5. Ereignisse haben Vorrang (<see cref="Anlass"/>): Tod, Kampf, Aufstand, Hochzeit, Sklaven (nach dem Rang).</para>
/// </summary>
public static class MusikModell
{
    // ── Daten ───────────────────────────────────────────────────────────────

    /// <summary>Ein Stück: Datei unter <c>assets/audio/musik/</c>, Anzeigetitel, Lage (tag, nacht, tod, event), Zusatz (Jahreszeit oder Ereignis) und Volk.</summary>
    public sealed record Stueck(string Datei, string Titel, string Lage, string? Zusatz, string? Volk, double Dauer, double Lufs = 0);

    private sealed class Datei
    {
        [JsonPropertyName("menue")] public string Menue { get; set; } = "";
        [JsonPropertyName("stuecke")] public List<StueckDaten> Stuecke { get; set; } = new();
    }

    private sealed class StueckDaten
    {
        [JsonPropertyName("datei")] public string Datei { get; set; } = "";
        [JsonPropertyName("titel")] public string Titel { get; set; } = "";
        [JsonPropertyName("lage")] public string Lage { get; set; } = "";
        [JsonPropertyName("zusatz")] public string? Zusatz { get; set; }
        [JsonPropertyName("volk")] public string? Volk { get; set; }
        [JsonPropertyName("dauer")] public double Dauer { get; set; }
        [JsonPropertyName("lufs")] public double Lufs { get; set; }
    }

    /// <summary>Liest <c>daten/musik.json</c>: die Stücke und das Stück des Hauptmenüs (leer, wenn es keines gibt). Kaputter Text ergibt eine leere Liste.</summary>
    public static (List<Stueck> Stuecke, string Menue) Laden(string json)
    {
        try
        {
            var d = JsonSerializer.Deserialize<Datei>(json);
            if (d is null) return (new List<Stueck>(), "");
            return (d.Stuecke.Where(s => s.Datei.Length > 0)
                .Select(s => new Stueck(s.Datei, s.Titel, s.Lage, s.Zusatz, s.Volk, s.Dauer, s.Lufs)).ToList(), d.Menue);
        }
        catch (JsonException)
        {
            return (new List<Stueck>(), "");
        }
    }

    // ── Lage ────────────────────────────────────────────────────────────────

    /// <summary>Wann der Morgen beginnt (Minuten nach Mitternacht): der Sonnenaufgang des Lichts in der Stadtansicht (06:00, siehe Stadtansicht.Licht.cs).</summary>
    public const int MorgenMinute = 6 * 60;

    /// <summary>Tag oder Nacht nach der Uhr: Nacht ist die „Dunkle Nacht“ der <see cref="Spielzeit.Tagesphase"/> (ab 20:00) und der Morgen vor dem Sonnenaufgang.</summary>
    public static bool IstNacht(double uhrMinute)
    {
        var minute = (int)Math.Floor(uhrMinute);
        var phase = new Spielzeit(Math.Clamp(minute - Spielzeit.TagesbeginnMinute, 0, Spielzeit.MinutenProTag - 1)).Tagesphase;
        return phase == Tagesphase.DunkleNacht || minute < MorgenMinute;
    }

    /// <summary>Jahreszeit als Name im Datenfeld <c>zusatz</c> (umschrieben).</summary>
    public static string ZusatzVon(Jahreszeit j) => j switch
    {
        Jahreszeit.Fruehling => "fruehling",
        Jahreszeit.Sommer => "sommer",
        Jahreszeit.Herbst => "herbst",
        _ => "winter",
    };

    /// <summary>Volk als Name im Datenfeld <c>volk</c> (Orks und Oger haben kein eigenes Stück).</summary>
    public static string? VolkName(Volk? v) => v switch
    {
        Volk.Mensch => "Menschen",
        Volk.Zwerg => "Zwerge",
        Volk.Elb => "Elben",
        Volk.Halbling => "Halblinge",
        _ => null,
    };

    // ── Auswahl ─────────────────────────────────────────────────────────────

    /// <summary>Tagesstücke: ohne Jahreszeit und Volk, dazu die der <paramref name="jahreszeit"/> und das Volksstück des eigenen Hauses.</summary>
    public static List<Stueck> TagKandidaten(IEnumerable<Stueck> alle, Jahreszeit jahreszeit, Volk? volk)
    {
        var zeit = ZusatzVon(jahreszeit);
        var name = VolkName(volk);
        return alle.Where(s => s.Lage == "tag" && ((s.Zusatz is null && s.Volk is null) || s.Zusatz == zeit || (s.Volk is not null && s.Volk == name))).ToList();
    }

    public static List<Stueck> NachtKandidaten(IEnumerable<Stueck> alle) => alle.Where(s => s.Lage == "nacht").ToList();

    /// <summary>Stücke eines Anlasses: Tod aus <c>tod</c>, die anderen aus <c>event</c> mit ihrem Zusatz.</summary>
    public static List<Stueck> AnlassKandidaten(IEnumerable<Stueck> alle, Anlass a) => a == Anlass.Tod
        ? alle.Where(s => s.Lage == "tod").ToList()
        : alle.Where(s => s.Lage == "event" && s.Zusatz == AnlassName(a)).ToList();

    /// <summary>
    /// Wählt zufällig ein Stück: nicht das zuletzt gespielte, und wenn genug da sind, auch nicht eines der <paramref name="zuletzt"/> (neueste zuletzt).
    /// <paramref name="zufall"/> liefert Zahlen in [0, 1). Gibt es kein Stück, null.
    /// </summary>
    public static Stueck? Waehlen(IReadOnlyList<Stueck> kandidaten, IReadOnlyList<string> zuletzt, Func<double> zufall)
    {
        if (kandidaten.Count == 0) return null;
        var frei = kandidaten.Where(s => !zuletzt.TakeLast(Math.Min(2, Math.Max(kandidaten.Count - 1, 1))).Contains(s.Datei)).ToList();
        if (frei.Count == 0) frei = kandidaten.Where(s => zuletzt.Count == 0 || s.Datei != zuletzt[^1]).ToList();
        if (frei.Count == 0) frei = kandidaten.ToList();
        return frei[Math.Min((int)(zufall() * frei.Count), frei.Count - 1)];
    }

    // ── Häufigkeit und Pausen ───────────────────────────────────────────────

    /// <summary>Wie oft Musik spielt (Einstellungen, Reiter Ton).</summary>
    public enum Haeufigkeit { Immer, Oft, Selten, NurEreignisse, Aus }

    public static string Name(Haeufigkeit h) => h switch
    {
        Haeufigkeit.Immer => "Immer",
        Haeufigkeit.Oft => "Oft",
        Haeufigkeit.Selten => "Selten",
        Haeufigkeit.NurEreignisse => "Nur Ereignisse",
        _ => "Aus",
    };

    /// <summary>
    /// Pause nach einem Stück in <b>Echtzeit-Sekunden</b>. Der Auftrag nennt „1 bis 3 Spielminuten“ für die kurze Stufe: Eine Spielminute dauert bei Tempo 1
    /// nur gut 0,8 s, das wären 1 bis 3 Sekunden und kaum eine Pause; hier gelten 1 bis 3 <b>Minuten</b> Echtzeit (Tempo ändert sie nicht, Musik läuft in
    /// Echtzeit). Wer es anders will, ändert nur diese Tabelle.
    /// </summary>
    public static (double Min, double Max) PauseGrenzen(Haeufigkeit h) => h switch
    {
        Haeufigkeit.Immer => (60, 180),     // 1–3 min
        Haeufigkeit.Oft => (180, 360),      // 3–6 min
        Haeufigkeit.Selten => (480, 900),   // 8–15 min
        _ => (60, 180),
    };

    public static double PauseSekunden(Haeufigkeit h, double zufall)
    {
        var (min, max) = PauseGrenzen(h);
        return min + (max - min) * Math.Clamp(zufall, 0, 1);
    }

    /// <summary>Dauer der Überblendung bei einem Wechsel der Lage: 4 bis 8 Sekunden.</summary>
    public static double WechselBlende(double zufall) => 4 + 4 * Math.Clamp(zufall, 0, 1);

    // ── Anlässe ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Ereignisse, die sich vor die Grundmusik schieben. Der Rang (kleiner = wichtiger) entscheidet, welches ein anderes unterbricht. Tod, Hochzeit
    /// und Kampf sind <b>Augenblicke</b> (das Stück läuft dann zu Ende), Sklaven und Aufstand <b>Zustände</b> (das Stück endet, wenn der Zustand vorbei ist).
    /// </summary>
    public enum Anlass { Keiner, Tod, Kampf, Aufstand, Hochzeit, Sklaven }

    public static int Rang(Anlass a) => a switch { Anlass.Tod => 1, Anlass.Kampf => 2, Anlass.Aufstand => 3, Anlass.Hochzeit => 4, Anlass.Sklaven => 5, _ => 99 };

    public static bool IstZustand(Anlass a) => a is Anlass.Sklaven or Anlass.Aufstand;

    public static string AnlassName(Anlass a) => a switch
    {
        Anlass.Tod => "tod", Anlass.Kampf => "kampf", Anlass.Aufstand => "aufstand", Anlass.Hochzeit => "hochzeit", Anlass.Sklaven => "sklaven", _ => "",
    };

    /// <summary>
    /// Welcher Anlass gehört zu einer Meldung des eigenen Hauses (<see cref="KlangAnsagenErkennung.Erkennen"/> hat Art und Haus schon geprüft)? Tod im
    /// Haus, Heirat, und Überfall, Einbruch (Raub) und Feuer, die gelangen, ergeben Kampf. „Kampf der Wache“ und „Brand löschen“ meldet der Kern
    /// noch nicht (offen, siehe Bereichsdatei).
    /// </summary>
    public static Anlass AnlassAusAnsage(string? ansageKennung) => ansageKennung switch
    {
        KlangAnsagenErkennung.Id.TodOberhaupt or KlangAnsagenErkennung.Id.TodFamilie => Anlass.Tod,
        KlangAnsagenErkennung.Id.Heirat => Anlass.Hochzeit,
        KlangAnsagenErkennung.Id.Ueberfall or KlangAnsagenErkennung.Id.Diebstahl or KlangAnsagenErkennung.Id.Feuer => Anlass.Kampf,
        _ => Anlass.Keiner,
    };

    // ── Die Steuerung ───────────────────────────────────────────────────────

    /// <summary>Was die Steuerung dem Spieler befiehlt.</summary>
    public enum BefehlArt { Starten, Ausblenden }

    /// <summary>
    /// <see cref="BefehlArt.Starten"/>: <see cref="Stueck"/> einblenden über <see cref="Einblende"/> s, alles andere über <see cref="AltAus"/> s ausblenden.
    /// <see cref="BefehlArt.Ausblenden"/>: alles über <see cref="AltAus"/> s ausblenden.
    /// </summary>
    public readonly record struct Befehl(BefehlArt Art, Stueck? Stueck, double Einblende, double AltAus, Anlass Anlass = Anlass.Keiner, bool Wechsel = false);

    /// <summary>Was die Steuerung je Schritt wissen muss.</summary>
    public readonly record struct Eingabe(double UhrMinute, Jahreszeit Jahreszeit, Volk? Volk, Haeufigkeit Haeufigkeit, Anlass Zustand = Anlass.Keiner);

    /// <summary>
    /// Der Ablauf der Musik als Zustandsmaschine: <see cref="Schritt"/> bekommt die vergangene Echtzeit und die Eingaben und gibt Befehle zurück.
    /// Die Uhr liefert der Aufrufer (<c>dt</c>); nichts hier hängt an Godot.
    /// </summary>
    public sealed class Steuerung
    {
        public const double AnlassGueltig = 10;       // so lange wartet ein gemeldeter Augenblick auf seinen Platz
        public const double AnlassSperre = 120;       // Wiederholsperre nach dem Ende des Stücks, Echtzeit-Sekunden
        public const double ZustandMindest = 20;      // ein Zustandsstück läuft mindestens so lange
        public const double AnlassEinblende = 2.5, AnlassAusblende = 3;
        public const double GrundEinblende = 4, EreignisEnde = 2, AnfangsPause = 4;

        private readonly IReadOnlyList<Stueck> _alle;
        private readonly Func<double> _zufall;
        private readonly List<string> _zuletzt = new();
        private double _jetzt, _pause = AnfangsPause, _laufZeit;
        private readonly Dictionary<Anlass, double> _gemeldetBis = new(), _gesperrtBis = new();

        public Steuerung(IReadOnlyList<Stueck> stuecke, Func<double> zufall)
        {
            _alle = stuecke;
            _zufall = zufall;
        }

        /// <summary>Das gerade laufende Stück (null in der Pause).</summary>
        public Stueck? Laufend { get; private set; }
        /// <summary>Welcher Anlass spielt gerade (<see cref="Anlass.Keiner"/>: Grundmusik oder nichts).</summary>
        public Anlass LaufenderAnlass { get; private set; }
        /// <summary>Tag (false) oder Nacht (true), für die das laufende Grundstück gewählt wurde.</summary>
        public bool? LaufendeNacht { get; private set; }
        /// <summary>Echtzeit-Sekunden bis zum nächsten Stück (nur in der Pause sinnvoll).</summary>
        public double PauseRest => Laufend is null ? Math.Max(_pause, 0) : 0;
        /// <summary>Wie viele Sekunden das laufende Stück schon läuft.</summary>
        public double LaufZeit => _laufZeit;

        /// <summary>Ein Augenblick geschah (Tod, Hochzeit, Kampf): er wartet <see cref="AnlassGueltig"/> Sekunden auf seinen Platz.</summary>
        public void Melden(Anlass a)
        {
            if (a != Anlass.Keiner) _gemeldetBis[a] = _jetzt + AnlassGueltig;
        }

        /// <summary>Zum Ausprobieren: das nächste Grundstück sofort (ohne Pause).</summary>
        public void PauseBeenden() => _pause = 0;

        /// <summary>Die Pause neu würfeln (nach dem Stück, oder wenn die Häufigkeit gewechselt hat: nicht länger als ihr Höchstwert).</summary>
        private void NeuePause(Haeufigkeit h) => _pause = PauseSekunden(h, _zufall());

        public List<Befehl> Schritt(double dt, Eingabe e, bool stueckZuEnde)
        {
            var aus = new List<Befehl>();
            _jetzt += dt;
            if (Laufend is not null) _laufZeit += dt;

            if (stueckZuEnde && Laufend is not null)
            {
                var anlass = LaufenderAnlass;
                if (anlass != Anlass.Keiner) _gesperrtBis[anlass] = _jetzt + AnlassSperre;
                Laufend = null;
                LaufenderAnlass = Anlass.Keiner;
                LaufendeNacht = null;
                if (anlass != Anlass.Keiner) _pause = EreignisEnde;
                else NeuePause(e.Haeufigkeit);
            }

            if (e.Haeufigkeit == Haeufigkeit.Aus)
            {
                if (Laufend is not null) Beenden(aus, 3);
                _pause = Math.Min(_pause, 0.5);
                return aus;
            }

            // Ein Zustand, der vorbei ist, beendet sein Stück (nach der Mindestzeit)
            if (Laufend is not null && IstZustand(LaufenderAnlass) && e.Zustand != LaufenderAnlass && _laufZeit >= ZustandMindest)
            {
                _gesperrtBis[LaufenderAnlass] = _jetzt + AnlassSperre;
                Beenden(aus, 6);
                _pause = EreignisEnde;
            }

            // Ereignisse
            var wunsch = Anlass.Keiner;
            foreach (var (a, bis) in _gemeldetBis.ToList())
            {
                if (bis < _jetzt) { _gemeldetBis.Remove(a); continue; }
                if (Rang(a) < Rang(wunsch)) wunsch = a;
            }
            if (e.Zustand != Anlass.Keiner && Rang(e.Zustand) < Rang(wunsch)) wunsch = e.Zustand;
            if (wunsch != Anlass.Keiner && wunsch != LaufenderAnlass && (Laufend is null || LaufenderAnlass == Anlass.Keiner || Rang(wunsch) < Rang(LaufenderAnlass))
                && _gesperrtBis.GetValueOrDefault(wunsch) <= _jetzt)
            {
                var stueck = Waehlen(AnlassKandidaten(_alle, wunsch), _zuletzt, _zufall);
                if (stueck is not null)
                {
                    _gemeldetBis.Remove(wunsch);
                    Starten(aus, stueck, AnlassEinblende, AnlassAusblende, wunsch, null, false);
                    return aus;
                }
                _gemeldetBis.Remove(wunsch); // es gibt kein Stück dazu: vergessen
            }

            // Grundmusik (nicht während eines Ereignisses; bei „nur Ereignisse“ gar nicht)
            if (LaufenderAnlass != Anlass.Keiner) return aus;
            if (e.Haeufigkeit == Haeufigkeit.NurEreignisse)
            {
                if (Laufend is not null) Beenden(aus, 4);
                return aus;
            }
            var nacht = IstNacht(e.UhrMinute);
            if (Laufend is not null)
            {
                if (LaufendeNacht is { } n && n != nacht)
                {
                    var neu = Waehlen(nacht ? NachtKandidaten(_alle) : TagKandidaten(_alle, e.Jahreszeit, e.Volk), _zuletzt, _zufall);
                    if (neu is not null)
                    {
                        var blende = WechselBlende(_zufall());
                        Starten(aus, neu, blende, blende, Anlass.Keiner, nacht, true);
                    }
                    else LaufendeNacht = nacht; // keine Stücke der neuen Lage: das laufende zu Ende spielen
                }
                return aus;
            }
            // Pause: Häufigkeit hat sich geändert, die Pause nicht länger als der Höchstwert
            _pause = Math.Min(_pause, PauseGrenzen(e.Haeufigkeit).Max);
            _pause -= dt;
            if (_pause > 0) return aus;
            var stk = Waehlen(nacht ? NachtKandidaten(_alle) : TagKandidaten(_alle, e.Jahreszeit, e.Volk), _zuletzt, _zufall);
            if (stk is null) { _pause = 30; return aus; }
            Starten(aus, stk, GrundEinblende, 0, Anlass.Keiner, nacht, false);
            return aus;
        }

        private void Starten(List<Befehl> aus, Stueck stueck, double ein, double altAus, Anlass anlass, bool? nacht, bool wechsel)
        {
            Laufend = stueck;
            LaufenderAnlass = anlass;
            LaufendeNacht = nacht;
            _laufZeit = 0;
            _zuletzt.Add(stueck.Datei);
            if (_zuletzt.Count > 6) _zuletzt.RemoveAt(0);
            aus.Add(new Befehl(BefehlArt.Starten, stueck, ein, altAus, anlass, wechsel));
        }

        private void Beenden(List<Befehl> aus, double sekunden)
        {
            Laufend = null;
            LaufenderAnlass = Anlass.Keiner;
            LaufendeNacht = null;
            aus.Add(new Befehl(BefehlArt.Ausblenden, null, 0, sekunden));
        }
    }
}
