using Godot;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Schicht 4 der Passanten (Teil der <c>partial class Stadtansicht</c>, zu Stadtansicht.Passanten.cs; Nutzer, 1.11.2026): Nach dem Stellen aller Figuren
/// rechnet <see cref="Passantenausweichen"/> für jede laufende, wie weit sie seitlich ausweicht und ob sie langsamer wird; das gilt ab dem nächsten Bild
/// (<see cref="Stadtansicht.Laufen"/> setzt den Versatz, der Rückstand verschiebt sie auf dem Weg). Wer steht (Markt, Gasse), ist Hindernis.
/// Dazu die Messung mit <c>--passantennaehe=1</c>: Nahkontakte (unter 0,5 m) und Überschneidungen (Abstand unter den beiden Körpern).
/// </summary>
public partial class Stadtansicht
{
    private readonly Passantenausweichen _ausweichen = new();
    private Passantenausweichen.Figur[] _ausweichFiguren = new Passantenausweichen.Figur[256];
    private readonly List<Passant> _ausweichListe = new();

    /// <summary>Holt sie nach dem Ausweichen um so viel schneller (Anteil ihres Tempos) auf, bis der Rückstand weg ist.</summary>
    private const float Aufholen = 0.2f;

    /// <summary>Mehr Rückstand (Meter) gibt es nicht: Kommt sie so lange nicht vorbei, geht sie weiter (sonst stünde sie hinter einem, der dort stehen bleibt).</summary>
    private const float RueckstandHoechstens = 6f;

    /// <summary>Halbe Körperbreite (Meter) nach Volk: so nah dürfen sich zwei Figuren kommen, bevor sie sich überschneiden.</summary>
    private static float KoerperRadius(Volk volk) => volk switch
    {
        Volk.Halbling => 0.17f,
        Volk.Zwerg => 0.22f,
        Volk.Ork => 0.26f,
        Volk.Oger => 0.32f,
        _ => 0.22f,
    };

    /// <summary>Ein Schritt Ausweichen für alle sichtbaren Figuren (in der Pause nicht).</summary>
    private void AusweichenRechnen(float sekunden, double minuteGenau)
    {
        if (SpurV1 || sekunden <= 0) return;
        _ausweichListe.Clear();
        foreach (var p in _passanten.Values)
            if ((p.Knoten.Visible || p.WartetDrinnen && p.Stand is null) && (p.Stand is not null || p.Pfad.Length > 0)) _ausweichListe.Add(p);
        if (_ausweichFiguren.Length < _ausweichListe.Count) _ausweichFiguren = new Passantenausweichen.Figur[_ausweichListe.Count * 2];
        for (var i = 0; i < _ausweichListe.Count; i++)
        {
            var p = _ausweichListe[i];
            var laeuft = p.Stand is null;
            _ausweichFiguren[i] = new Passantenausweichen.Figur
            {
                X = p.Knoten.Position.X, Z = p.Knoten.Position.Z, Radius = p.Radius, Laeuft = laeuft, Nummer = p.Nummer,
                Vx = laeuft ? p.Geschwindigkeit.X : 0, Vz = laeuft ? p.Geschwindigkeit.Z : 0,
                Rx = p.RechtsJetzt.X, Rz = p.RechtsJetzt.Z, Links = p.RaumLinks, Rechts = p.RaumRechts,
                Seitlich = p.Seitlich, Seite = p.Seite, Bremse = laeuft ? p.Bremse : 1, Haltezeit = p.Haltezeit,
            };
        }
        _ausweichen.Schritt(_ausweichFiguren, _ausweichListe.Count, sekunden);
        var ticksJeSekunde = _simulation!.Takt.TicksProSekunde;
        for (var i = 0; i < _ausweichListe.Count; i++)
        {
            var p = _ausweichListe[i];
            if (p.Stand is not null) continue;
            ref readonly var f = ref _ausweichFiguren[i];
            p.SeitRate = (f.Seitlich - p.Seitlich) / sekunden;
            (p.Seitlich, p.Seite, p.Bremse, p.Haltezeit) = (f.Seitlich, f.Seite, f.Bremse, f.Haltezeit);
            // gebremst bleibt sie hinter ihrem Plan zurück, frei holt sie auf (Meter auf dem Boden)
            var tempo = (float)((p.Mitte?.Bodenlaenge ?? p.Laenge) * ticksJeSekunde / Math.Max(p.LaufEnde - p.LaufStart, 1e-6));
            // (nach dem Planende wächst er nicht mehr – sonst ginge sie rückwärts –, und sie holt ihn in ihrem eigenen, gebremsten Tempo auf)
            if (minuteGenau >= p.LaufEnde) p.Rueckstand = Mathf.Max(0, p.Rueckstand - p.Bremse * tempo * sekunden);
            else if (p.Bremse < 0.999f) p.Rueckstand += (1 - p.Bremse) * tempo * sekunden;
            else if (p.Rueckstand > 0) p.Rueckstand = Mathf.Max(0, p.Rueckstand - Aufholen * tempo * sekunden);
            if (p.Rueckstand > RueckstandHoechstens) (p.Rueckstand, p.Bremse) = (RueckstandHoechstens, 1);
        }
    }

    // ── Messung (Startoption --passantennaehe=1, nur Protokoll) ──
    private static readonly bool NaeheMessen = Wasserarten.Startoption("passantennaehe") is "1" or "an" or "ja";
    private const float NaeheAbstand = 0.5f;
    private readonly HashSet<long> _naheBeruehrt = new(), _ueberschnitten = new(), _ueberschnittenStehend = new();
    private double _naheSekunden, _naheBilder, _naheLaeufer, _ueberBilder, _ueberBilderImBild;
    private long _naheNeuGesamt, _naheNeuImBild, _naheGegen, _naheGleich, _ueberNeu, _ueberNeuImBild, _ueberStehendNeu, _ueberStehendNeuImBild, _ueberAmRand, _ueberAufPlatz, _ueberStehendAufPlatz;

    /// <summary>
    /// Zählt jede neue Berührung zweier laufender Figuren (Abstand auf dem Boden unter 0,5 m; eine Berührung zählt einmal, bis sie sich wieder
    /// trennen), getrennt nach „im Bild“ und gegenläufig (Blickrichtungen mehr als 90° auseinander) oder gleichläufig. Dazu Überschneidungen: zwei Laufende
    /// (oder eine Laufende und eine Stehende) näher als ihre beiden Körper (<see cref="KoerperRadius"/>) – als neue Fälle und als Paar-Bilder.
    /// In der Pause zählt nichts.
    /// </summary>
    private void NaeheZaehlen(double sekunden)
    {
        if (sekunden <= 0) return;
        _naheSekunden += sekunden;
        _naheBilder++;
        var sichtbar = _passanten.Where(k => k.Value.Knoten.Visible).OrderBy(k => k.Key).ToList();
        var laeufer = sichtbar.Where(k => k.Value.Stand is null).ToList();
        var stehend = sichtbar.Where(k => k.Value.Stand is not null).ToList();
        _naheLaeufer += laeufer.Count(k => ImBild(k.Value.Knoten.Position));
        var (jetzt, ueberJetzt, stehendJetzt) = (new HashSet<long>(), new HashSet<long>(), new HashSet<long>());
        for (var i = 0; i < laeufer.Count; i++)
        {
            var a = laeufer[i].Value;
            for (var j = i + 1; j < laeufer.Count; j++)
            {
                var b = laeufer[j].Value;
                var d = a.Knoten.Position - b.Knoten.Position;
                var abstand2 = d.X * d.X + d.Z * d.Z;
                if (abstand2 >= NaeheAbstand * NaeheAbstand && abstand2 >= (a.Radius + b.Radius) * (a.Radius + b.Radius)) continue;
                var schluessel = (long)laeufer[i].Key * 1_000_000 + laeufer[j].Key;
                var imBild = ImBild(a.Knoten.Position) && ImBild(b.Knoten.Position);
                if (abstand2 < (a.Radius + b.Radius) * (a.Radius + b.Radius))
                {
                    ueberJetzt.Add(schluessel);
                    _ueberBilder++;
                    if (imBild) _ueberBilderImBild++;
                    if (!_ueberschnitten.Contains(schluessel))
                    {
                        _ueberNeu++;
                        if (imBild) _ueberNeuImBild++;
                        if (imBild && AufDemPflaster(a.Knoten.Position) && AufDemPflaster(b.Knoten.Position)) _ueberAufPlatz++; // Markt v3: nur auf dem Platz
                        if (Mathf.Min(a.AmRand, b.AmRand) < 2) _ueberAmRand++;
                    }
                }
                if (abstand2 >= NaeheAbstand * NaeheAbstand) continue;
                jetzt.Add(schluessel);
                if (_naheBeruehrt.Contains(schluessel)) continue;
                _naheNeuGesamt++;
                if (imBild) _naheNeuImBild++;
                if (Mathf.Cos(a.Knoten.Rotation.Y - b.Knoten.Rotation.Y) < 0) _naheGegen++; else _naheGleich++;
            }
            foreach (var (nummer, s) in stehend)
            {
                var d = a.Knoten.Position - s.Knoten.Position;
                if (d.X * d.X + d.Z * d.Z >= (a.Radius + s.Radius) * (a.Radius + s.Radius)) continue;
                var schluessel = (long)laeufer[i].Key * 1_000_000 + nummer;
                stehendJetzt.Add(schluessel);
                if (_ueberschnittenStehend.Contains(schluessel)) continue;
                _ueberStehendNeu++;
                if (ImBild(a.Knoten.Position) && AufDemPflaster(a.Knoten.Position)) _ueberStehendAufPlatz++;
                if (ImBild(a.Knoten.Position)) _ueberStehendNeuImBild++;
            }
        }
        _naheBeruehrt.Clear();
        _ueberschnitten.Clear();
        _ueberschnittenStehend.Clear();
        _naheBeruehrt.UnionWith(jetzt);
        _ueberschnitten.UnionWith(ueberJetzt);
        _ueberschnittenStehend.UnionWith(stehendJetzt);
    }

    /// <summary>Die Messzeile <c>[passantennaehe]</c> (alle 10 Sekunden, Summen seit dem Start).</summary>
    private void NaeheMelden()
    {
        var minuten = Math.Max(_naheSekunden, 1) / 60.0;
        var stand = SpurAlt ? "alt" : SpurV1 ? "v1" : "neu";
        GD.Print($"[passantennaehe] Spur {stand}, Tempo {_simulation!.Takt.Tempo}: seit {_naheSekunden:0} s unter {NaeheAbstand} m: {_naheNeuGesamt} Begegnungen " +
                 $"({_naheNeuGesamt / minuten:0.0} je Minute; im Bild {_naheNeuImBild}, {_naheNeuImBild / minuten:0.0} je Minute; " +
                 $"davon gegenläufig {_naheGegen}, gleichläufig {_naheGleich}), im Bild laufen im Mittel {_naheLaeufer / Math.Max(_naheBilder, 1):0.0} Figuren; " +
                 $"Überschneidungen Laufende: {_ueberNeu} ({_ueberNeu / minuten:0.0} je Minute; im Bild {_ueberNeuImBild}, {_ueberNeuImBild / minuten:0.0} je Minute; " +
                 $"Paar-Bilder {_ueberBilder:0}, im Bild {_ueberBilderImBild:0}; davon an Anfang oder Ziel {_ueberAmRand}), mit Stehenden {_ueberStehendNeu} (im Bild {_ueberStehendNeuImBild}, {_ueberStehendNeuImBild / minuten:0.0} je Minute); " +
                 $"auf dem Pflaster eines Marktplatzes im Bild: Laufende {_ueberAufPlatz} ({_ueberAufPlatz / minuten:0.0} je Minute), mit Stehenden {_ueberStehendAufPlatz} ({_ueberStehendAufPlatz / minuten:0.0} je Minute)");
    }
}
