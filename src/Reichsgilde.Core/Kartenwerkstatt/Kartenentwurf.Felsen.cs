using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Felsstücke im Kartenentwurf (Felspinsel F3)
//
//  Felsstücke (Bauen/Felsstuecke.cs) stehen in der Liste „felsen.stuecke“, das Grundgestein in „felsen.grundgestein“. Gesetzt werden sie mit dem
//  Felspinsel (Bauen/Felspinsel.cs): Streuen als Strich (Beginnen, Weiter je Mausbewegung, Beenden), Stempeln mit einem Klick, Radieren als
//  Strich. Beim Ziehen ändert sich die Liste sofort (die Darstellung zeichnet mit, jedes Stück wird als Gesetzt/Gelöscht gemeldet); erst
//  „Beenden“ legt den ganzen Strich als einen Schritt in die Geschichte – Rückgängig nimmt ihn ganz zurück.
//  Felsstücke sperren nichts: Die Prüfung meldet nur Hinweise (unbekannte Form oder Gesteinsart, außerhalb der Karte).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Felsstück im Entwurf: dieselben Felder wie <see cref="Felsstueck"/>, aber veränderbar (nur über die Befehle des Entwurfs).</summary>
public sealed class EntwurfFels : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Fels;
    public override string Bezeichnung => Felsform?.Name ?? Form;

    public string Form { get; internal set; } = "";

    /// <summary>Gesteinsart, wenn sie vom Grundgestein abweicht (sonst null).</summary>
    public string? Gestein { get; internal set; }

    public double X { get; internal set; }
    public double Z { get; internal set; }
    public double Drehung { get; internal set; }
    public double Massstab { get; internal set; } = 1;
    public double Neigung { get; internal set; }
    public double Einsinken { get; internal set; } = Felsformen.EinsinkenVorgabe;
    public double KippenX { get; internal set; }
    public double KippenZ { get; internal set; }

    public Kartenort Punkt => new(X, Z);

    /// <summary>Die Form (null, wenn es sie nicht gibt).</summary>
    public Felsform? Felsform
    {
        get
        {
            // zwischengespeichert: Streuen fragt den Radius aller Stücke bei jedem Versuch
            if (!ReferenceEquals(_formFuer, Form)) (_formFuer, _form) = (Form, Felsformen.Finde(Form));
            return _form;
        }
    }

    private string? _formFuer;
    private Felsform? _form;

    /// <summary>Der Radius der Grundfläche (Meter, mit Maßstab) – für den Abstand beim Streuen.</summary>
    public double Radius => (Felsform?.Radius ?? 1) * Massstab;

    /// <summary>Das Stück in den Typen der Stadtkarte.</summary>
    public Felsstueck AlsFelsstueck() => new()
    {
        Form = Form, Gestein = Gestein, Punkt = new[] { X, Z }, Drehung = Drehung, Massstab = Massstab, Neigung = Neigung, Einsinken = Einsinken,
        Kippen = KippenX == 0 && KippenZ == 0 ? null : new[] { KippenX, KippenZ },
    };

    internal static EntwurfFels Aus(Felsstueck f, JsonObject? vorlage) => new()
    {
        Form = f.Form, Gestein = f.Gestein, X = f.Punkt is { Length: 2 } ? f.Punkt[0] : 0, Z = f.Punkt is { Length: 2 } ? f.Punkt[1] : 0,
        Drehung = f.Drehung, Massstab = f.Massstab, Neigung = f.Neigung, Einsinken = f.Einsinken,
        KippenX = f.Kippen is { Length: 2 } k ? k[0] : 0, KippenZ = f.Kippen is { Length: 2 } k2 ? k2[1] : 0, Vorlage = vorlage,
    };

    internal static EntwurfFels Aus(Felslage l) => new()
    {
        Form = l.Form, Gestein = l.Gestein, X = l.X, Z = l.Z, Drehung = l.Drehung, Massstab = l.Massstab, Neigung = l.Neigung, Einsinken = l.Einsinken,
        KippenX = l.KippenX, KippenZ = l.KippenZ,
    };

    internal override EntwurfTeil KopieTeil() => new EntwurfFels
    {
        Form = Form, Gestein = Gestein, X = X, Z = Z, Drehung = Drehung, Massstab = Massstab, Neigung = Neigung, Einsinken = Einsinken,
        KippenX = KippenX, KippenZ = KippenZ, Vorlage = Vorlage,
    };

    internal override void UebernimmTeil(EntwurfTeil werte)
    {
        var o = (EntwurfFels)werte;
        (Form, Gestein, X, Z, Drehung, Massstab, Neigung, Einsinken, KippenX, KippenZ, Vorlage) =
            (o.Form, o.Gestein, o.X, o.Z, o.Drehung, o.Massstab, o.Neigung, o.Einsinken, o.KippenX, o.KippenZ, o.Vorlage);
    }

    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfFels o && Form == o.Form && Gestein == o.Gestein && X == o.X
        && Z == o.Z && Drehung == o.Drehung && Massstab == o.Massstab && Neigung == o.Neigung && Einsinken == o.Einsinken && KippenX == o.KippenX
        && KippenZ == o.KippenZ;

    internal override JsonObject AlsJson() => Zusammensetzen(Vorlage,
        ("form", Form), ("gestein", Gestein), ("punkt", JsonPunkt(new Kartenort(X, Z))), ("drehung", Drehung), ("massstab", Massstab),
        ("neigung", Neigung), ("einsinken", Einsinken),
        ("kippen", KippenX == 0 && KippenZ == 0 ? null : new JsonArray(JsonValue.Create(KippenX), JsonValue.Create(KippenZ))));
}

/// <summary>Das Grundgestein der Karte als Teil (nur einmal je Karte; für Rückgängig und die Meldung <see cref="Kartenteil.Grundgestein"/>).</summary>
public sealed class EntwurfGrundgestein : EntwurfTeil
{
    public override Kartenteil Teil => Kartenteil.Grundgestein;
    public override string Bezeichnung => "Grundgestein";

    /// <summary>Die Gesteinsart (null: keine in der Datei, es gilt <see cref="Felsformen.GrundgesteinVorgabe"/>).</summary>
    public string? Gestein { get; internal set; }

    /// <summary>Die Gesteinsart, die gilt.</summary>
    public string Wirksam => Felsformen.IstGestein(Gestein) ? Gestein! : Felsformen.GrundgesteinVorgabe; // ein unbekanntes gilt nicht (Prüfhinweis)

    internal override EntwurfTeil KopieTeil() => new EntwurfGrundgestein { Gestein = Gestein };
    internal override void UebernimmTeil(EntwurfTeil werte) => Gestein = ((EntwurfGrundgestein)werte).Gestein;
    internal override bool GleicheWerte(EntwurfTeil anderer) => anderer is EntwurfGrundgestein o && o.Gestein == Gestein;
    internal override JsonObject AlsJson() => throw new InvalidOperationException("Das Grundgestein steht in „felsen“ (Kartenentwurf.ZuJson).");
}

/// <summary>Ein Befehl, der schon ausgeführt ist (ein Felsstrich beim Ziehen): Das erste Ausführen tut nichts, jedes weitere (Wiederholen) alles.</summary>
internal sealed class SchonAusgefuehrt(IKartenbefehl befehl) : IKartenbefehl
{
    private bool _erstes = true;

    public string Beschreibung => befehl.Beschreibung;

    public void Ausfuehren(Kartenentwurf entwurf)
    {
        if (_erstes) _erstes = false;
        else befehl.Ausfuehren(entwurf);
    }

    public void Zuruecknehmen(Kartenentwurf entwurf) => befehl.Zuruecknehmen(entwurf);
}

public sealed partial class Kartenentwurf
{
    /// <summary>Die Felsstücke (<c>felsen.stuecke</c>) in der Reihenfolge der Datei.</summary>
    public IReadOnlyList<EntwurfFels> Felsen => _felsen;

    /// <summary>Das Grundgestein der Karte (<c>felsen.grundgestein</c>).</summary>
    public EntwurfGrundgestein Grundgestein => _grundgestein;

    /// <summary>Setzt das Grundgestein (null: keins, es gilt die Vorgabe). Ein Schritt mit Rückgängig.</summary>
    /// <exception cref="ArgumentException">Wenn es die Gesteinsart nicht gibt.</exception>
    public void GrundgesteinSetzen(string? gestein)
    {
        if (gestein is not null && !Felsformen.IstGestein(gestein)) throw new ArgumentException($"Eine Gesteinsart „{gestein}“ gibt es nicht.", nameof(gestein));
        FelsStrichBeenden();
        Aendern(_grundgestein, "geändert", g => g.Gestein = gestein);
    }

    /// <summary>Die Gesteinsart, mit der ein Stück gezeichnet wird: seine eigene oder das Grundgestein.</summary>
    public string GesteinVon(EntwurfFels f) => Felsformen.IstGestein(f.Gestein) ? f.Gestein! : _grundgestein.Wirksam;

    /// <summary>Der Pinsel begrenzt, ein Gestein gleich dem Grundgestein fällt weg („gestein“ steht nur bei Abweichung in der Datei).</summary>
    private Felspinsel FelsBegrenzt(Felspinsel pinsel)
    {
        var p = pinsel.Begrenzt();
        return p.Gestein == _grundgestein.Wirksam ? p with { Gestein = null } : p;
    }

    /// <summary>Löscht Felsstücke in einem Schritt; nichts, wenn keins im Entwurf ist.</summary>
    public void FelsenLoeschen(IEnumerable<EntwurfFels> felsen)
    {
        FelsStrichBeenden();
        var befehle = felsen.Distinct(ReferenceEqualityComparer.Instance).Cast<EntwurfFels>().Select(f => (f, i: IndexVon(f))).Where(x => x.i >= 0)
            .OrderByDescending(x => x.i).Select(x => (IKartenbefehl)new TeilLoeschen(x.f, x.i)).ToList();
        if (befehle.Count == 0) return;
        Geschichte.Ausfuehren(befehle.Count == 1 ? befehle[0] : new Sammelbefehl($"{befehle.Count} Felsstücke gelöscht", befehle));
    }

    // ── Stempel ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Was ein Stempel an diesem Punkt setzen würde (ein Stück oder die Gruppe des Pinsels) – ohne etwas zu ändern: die Vorschau unter der
    /// Maus. Gleicher Punkt, Pinsel, Startwert und Stand – dieselben Stücke wie <see cref="FelsStempeln"/>. Leer, wenn der Filter hier nicht
    /// wirkt (Faktor unter ½) oder der Punkt außerhalb der Karte liegt.
    /// </summary>
    public IReadOnlyList<Felslage> FelsStempelVorschau(Felspinsel pinsel, Kartenort ort, double? drehung = null, int? startwert = null)
    {
        var p = FelsBegrenzt(pinsel);
        if (!Kartengrenzen.Aussen.Enthaelt(ort) || Felsstreuung.Filterfaktor(p.Filter, _hoehen, _boden, ort.X, ort.Z) < 0.5) return Array.Empty<Felslage>();
        var zufall = new Zufallsquelle(startwert ?? FelsStartwert(ort));
        return Felsstreuung.Treffer(p, zufall, _hoehen, ort.X, ort.Z, drehung is { } d ? Kartenraster.Drehung(d) : null)
            .Where(l => Kartengrenzen.Aussen.Enthaelt(l.X, l.Z)).ToList();
    }

    /// <summary>
    /// Stempelt ein Stück oder die Gruppe des Pinsels an diesen Punkt (<paramref name="drehung"/>: feste Drehung in Grad; null: zufällig bzw.
    /// hangabwärts). Ohne Startwert einer aus dem Punkt und der Zahl der Stücke. Ein Schritt; leer, wenn nichts gesetzt wurde.
    /// </summary>
    public IReadOnlyList<EntwurfFels> FelsStempeln(Felspinsel pinsel, Kartenort ort, double? drehung = null, int? startwert = null)
    {
        FelsStrichBeenden();
        var neue = FelsStempelVorschau(pinsel with { Radierer = false }, ort, drehung, startwert).Select(EntwurfFels.Aus).ToList();
        if (neue.Count == 0) return neue;
        var befehle = neue.Select((f, i) => (IKartenbefehl)new TeilSetzen(f, _felsen.Count + i)).ToList();
        var name = Felsformen.Gruppe(pinsel.Gruppe) is { } g ? $"Gruppe {g.Name}" : neue[0].Bezeichnung;
        Geschichte.Ausfuehren(new Sammelbefehl($"{name} gestempelt", befehle));
        return neue;
    }

    /// <summary>Der Startwert eines Strichs oder Stempels ohne eigenen: aus dem ersten Punkt (cm) und der Zahl der Stücke.</summary>
    public int FelsStartwert(Kartenort ort) =>
        unchecked((int)NaturVerteilung.Streuung((int)Math.Round(ort.X * 100), (long)Math.Round(ort.Z * 100) * 31 + _felsen.Count));

    // ── Strich: Streuen und Radieren ─────────────────────────────────────────

    private Felsstrich? _felsstrich;

    /// <summary>Läuft gerade ein Felsstrich?</summary>
    public bool FelsStrichLaeuft => _felsstrich is not null;

    /// <summary>
    /// Beginnt einen Felsstrich (Streuen, oder Radieren mit <see cref="Felspinsel.Radierer"/>); ein laufender wird vorher beendet. Ohne Startwert
    /// einer aus dem ersten Punkt (<see cref="FelsStartwert"/>). Danach <see cref="FelsStrichWeiter"/> je Mausbewegung.
    /// </summary>
    public void FelsStrichBeginnen(Felspinsel pinsel, int? startwert = null)
    {
        FelsStrichBeenden();
        _felsstrich = new Felsstrich(this, FelsBegrenzt(pinsel), startwert);
    }

    /// <summary>
    /// Führt den Strich bis zu diesem Punkt weiter (der erste Punkt setzt den ersten Tupfer, danach alle <see cref="Felspinsel.Tupfabstand"/>
    /// Meter einer). Die Stücke ändern sich sofort, jedes wird gemeldet. Gibt zurück, wie viele Stücke dazukamen oder wegfielen.
    /// Ohne laufenden Strich (nie begonnen, oder Rückgängig, Wiederholen bzw. Speichern haben ihn beendet) geschieht nichts (0).
    /// </summary>
    public int FelsStrichWeiter(Kartenort p)
    {
        if (_felsstrich is not { } strich) return 0;
        var n = strich.Weiter(p);
        if (n > 0) MeldungenAusloesen();
        return n;
    }

    /// <summary>Beendet den Strich: Er wird ein Schritt in der Geschichte. False: es lief keiner oder er änderte nichts.</summary>
    public bool FelsStrichBeenden()
    {
        if (_felsstrich is not { } strich) return false;
        _felsstrich = null;
        if (strich.Befehle.Count == 0) return false;
        var (neu, weg) = (strich.Befehle.Count(b => b is TeilSetzen), strich.Befehle.Count(b => b is TeilLoeschen));
        var text = strich.Pinsel.Radierer ? $"{weg} Felsstücke radiert" : $"{neu} Felsstücke gestreut";
        Geschichte.Ausfuehren(new SchonAusgefuehrt(new Sammelbefehl(text, strich.Befehle)));
        return true;
    }

    /// <summary>Ein ganzer Felsstrich in einem Aufruf (Kartenskript, Tests): Beginnen, alle Punkte, Beenden – ein Schritt.</summary>
    public bool FelsStreuen(Felspinsel pinsel, IReadOnlyList<Kartenort> strich, int? startwert = null)
    {
        FelsStrichBeginnen(pinsel, startwert);
        foreach (var p in strich) FelsStrichWeiter(p);
        return FelsStrichBeenden();
    }

    /// <summary>Ein laufender Felsstrich: setzt seine Tupfer in festen Abständen und merkt sich jeden Befehl in der Reihenfolge, in der er wirkte.</summary>
    private sealed class Felsstrich
    {
        private readonly Kartenentwurf _e;
        private readonly int? _startwert;
        private Zufallsquelle? _zufall;
        private Kartenort? _letzter;
        private double _rest;

        public Felsstrich(Kartenentwurf entwurf, Felspinsel pinsel, int? startwert) => (_e, Pinsel, _startwert) = (entwurf, pinsel, startwert);

        public Felspinsel Pinsel { get; }

        /// <summary>Die Befehle des Strichs in der Reihenfolge, in der sie schon ausgeführt sind.</summary>
        public List<IKartenbefehl> Befehle { get; } = new();

        public int Weiter(Kartenort p)
        {
            var vorher = Befehle.Count;
            if (_letzter is not { } von)
            {
                _zufall = new Zufallsquelle(_startwert ?? _e.FelsStartwert(p));
                Tupfen(p);
                _letzter = p;
                return Befehle.Count - vorher;
            }
            var laenge = von.Abstand(p);
            var schritt = Pinsel.Tupfabstand;
            var weg = schritt - _rest;
            while (weg <= laenge + 1e-9)
            {
                var t = laenge == 0 ? 1 : weg / laenge;
                Tupfen(new Kartenort(von.X + (p.X - von.X) * t, von.Z + (p.Z - von.Z) * t));
                weg += schritt;
            }
            _rest = laenge - (weg - schritt);
            _letzter = p;
            return Befehle.Count - vorher;
        }

        private void Ausfuehren(IKartenbefehl b)
        {
            b.Ausfuehren(_e);
            Befehle.Add(b);
        }

        private void Tupfen(Kartenort m)
        {
            var p = Pinsel;
            var r2 = p.Radius * p.Radius;
            bool ImKreis(double x, double z) => (x - m.X) * (x - m.X) + (z - m.Z) * (z - m.Z) <= r2;
            if (p.Radierer)
            {
                // von hinten nach vorn: Rückgängig setzt jedes an seine alte Stelle zurück
                for (var i = _e._felsen.Count - 1; i >= 0; i--)
                {
                    var f = _e._felsen[i];
                    if (ImKreis(f.X, f.Z) && Felsstreuung.Filterfaktor(p.Filter, _e._hoehen, _e._boden, f.X, f.Z) >= 0.5) Ausfuehren(new TeilLoeschen(f, i));
                }
                return;
            }
            var zufall = _zufall!;
            var ziel = p.Dichte * Math.PI * r2;
            var belegt = _e._felsen.Where(f => ImKreis(f.X, f.Z)).Sum(f => Math.PI * f.Radius * f.Radius);
            var versuche = (int)Math.Clamp(Math.Ceiling(3 * (ziel - belegt) / Math.Max(1e-3, p.KleinsterPlatz)), Felspinsel.VersucheMin, Felspinsel.VersucheMax);
            var fehl = 0;
            for (var versuch = 0; versuch < versuche && belegt < ziel && fehl < Felspinsel.FehlversucheMax; versuch++)
            {
                fehl++; // ein Treffer setzt es unten zurück
                // ein Punkt im Kreis (verwerfen außerhalb – ohne Winkelfunktionen)
                var (dx, dz) = ((2 * zufall.NextDouble() - 1) * p.Radius, (2 * zufall.NextDouble() - 1) * p.Radius);
                if (dx * dx + dz * dz > r2) continue;
                var (x, z) = (m.X + dx, m.Z + dz);
                if (!_e.Kartengrenzen.Aussen.Enthaelt(x, z)) continue;
                var faktor = Felsstreuung.Filterfaktor(p.Filter, _e._hoehen, _e._boden, x, z);
                if (faktor < 1 && zufall.NextDouble() >= faktor) continue;
                var lagen = Felsstreuung.Treffer(p, zufall, _e._hoehen, x, z);
                if (lagen.Any(l => !_e.Kartengrenzen.Aussen.Enthaelt(l.X, l.Z) || ZuNah(l))) continue;
                fehl = 0;
                foreach (var l in lagen)
                {
                    Ausfuehren(new TeilSetzen(EntwurfFels.Aus(l), _e._felsen.Count));
                    if (ImKreis(l.X, l.Z)) belegt += Math.PI * l.Radius * l.Radius;
                }
            }
        }

        /// <summary>Liegt das Stück zu nah an einem anderen (Kreise der Grundflächen überdecken sich mehr als erlaubt)?</summary>
        private bool ZuNah(Felslage l)
        {
            foreach (var f in _e._felsen)
            {
                var d = (1 - Felspinsel.Ueberlappung) * (l.Radius + f.Radius);
                if ((f.X - l.X) * (f.X - l.X) + (f.Z - l.Z) * (f.Z - l.Z) < d * d) return true;
            }
            return false;
        }
    }
}
