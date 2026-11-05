using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Torwache (Darstellung, 5.11.2026): Die Torwächter des Kerns (<see cref="Torwache"/>, echte benannte Einwohner, nicht in der Liste der Passanten)
/// stehen am Stadttor, auf der Stadtseite links und rechts der Durchfahrt (Platz 0 links, 1 rechts, weitere daneben), in Waffenrock in Stadtfarben,
/// Eisenhut und mit der Hellebarde in der linken Hand; nachts mit Laterne in der rechten. Alles folgt nur aus der Uhr (<see cref="Torwache.Dienst"/>):
/// Beim Schichtwechsel kommt die neue Mannschaft aus den Gassen (Weg so lang, wie man in <c>Weg</c> Spielminuten geht, höchstens bis zum Markt) und
/// die alte geht zurück; bei Pause, Sprung und Laden stimmt es deshalb immer. Die Kerndaten werden nur gelesen.
/// <list type="bullet">
/// <item><b>Leben am Posten</b>: langsame Stehschleife; ab und zu (fest aus Nummer und Spielminute, kein Zufall) ein paar Schritte hin und her;
/// naht ein Passant oder Karren der Durchfahrt, wendet sich der Wächter ihm zu (nur Bild).</item>
/// <item><b>Leistung</b>: Figuren entstehen erst, wenn ein Wächter im Dienst ist und sein Tor im Bild liegt (höchstens zwei neue je Bild); Tore außerhalb
/// des Bildes werden nicht gerechnet; die Pose kommt aus <see cref="FigurImBildBewegen"/> (Sparstufen wie bei den Passanten); der Weg je Tor wird einmal
/// gesucht und gemerkt.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Startoption <c>--torwache=0</c>: keine Torwächter zeigen (zum Vergleichen der Leistung).</summary>
    private static readonly bool TorwacheAus = Wasserarten.Startoption("torwache") is "0" or "aus" or "nein";

    /// <summary>Ein Torwächter in der Stadtansicht: Figur, Knoten, Laterne und Haltung.</summary>
    private sealed class Torposten
    {
        public required Torwaechter Wache { get; init; }
        public required Node3D Knoten { get; init; }
        public Figur? Figur { get; set; }
        public Traglicht? Licht { get; set; }
        public bool Sichtbar { get; set; }
        /// <summary>Blickrichtung (Drehung um Y), wird sanft nachgeführt.</summary>
        public float Blick { get; set; }
        public bool BlickGesetzt { get; set; }
    }

    /// <summary>Die Lage eines Tores für die Wache: Standplätze, Blickrichtungen und der Weg in die Stadt (je Tor einmal berechnet).</summary>
    private sealed class Torlage
    {
        public Vector3 Ort { get; init; }
        public Vector3 Anker { get; init; }
        /// <summary>Einheitsvektoren in der Waagrechten: nach draußen, in die Stadt (Gegenteil) und quer zur Durchfahrt.</summary>
        public Vector3 Aussen { get; init; }
        public Vector3 Quer { get; init; }
        /// <summary>Die Gasse von der Stadtseite der Durchfahrt zum Markt (ohne die Standplätze).</summary>
        public Vector3[] Gasse { get; init; } = Array.Empty<Vector3>();
        public Dictionary<int, (Vector3[] Pfad, float Laenge)> Wege { get; } = new();
        /// <summary>Wem sich die Wächter zuwenden (Passant oder Karren in der Nähe der Durchfahrt), und wann zuletzt gesucht wurde.</summary>
        public Vector3? Gast { get; set; }
        public double Gesucht { get; set; } = -1;
    }

    private readonly Dictionary<int, Torposten> _torposten = new();
    private readonly Dictionary<string, Torlage?> _torlagen = new();
    private Node3D? _torwacheEbene;
    private int _torBauten;

    /// <summary>Wie weit hinter der Mitte der Durchfahrt (Meter, in die Stadt) die Wächter stehen: die Torbau-Innenwand liegt bei 2,6 m.</summary>
    private const float TorPostenTiefe = 4.0f;
    /// <summary>Wie weit neben der Mitte der Durchfahrt sie stehen: der Torbau ragt 1,8 bis 3 m seitlich, die Durchfahrt bleibt frei.</summary>
    private const float TorPostenSeite = 3.3f;
    private const float TorPostenAbstand = 1.3f;
    /// <summary>Ein Tor innerhalb dieser Entfernung (Meter) vom Bildrand wird gerechnet.</summary>
    private const float TorBildRand = 22f;
    /// <summary>Wer in dieser Entfernung (Meter) von der Durchfahrt geht oder fährt, zieht den Blick der Wächter an.</summary>
    private const float TorGastRadius = 7f;
    /// <summary>Dauer eines Abschnitts (Spielminuten), in dem ein Wächter ein paar Schritte gehen kann, und wie weit (Meter).</summary>
    private const int TorSchrittMinuten = 12;
    private const float TorSchrittWeite = 1.3f;

    /// <summary>Die Lage des Tores mit diesem Namen (null, wenn die Karte es nicht kennt).</summary>
    private Torlage? TorlageVon(string name)
    {
        var tor = _tore.FirstOrDefault(t => t.Name == name);
        if (tor.Name is null) return null;
        if (_torlagen.TryGetValue(name, out var bekannt) && bekannt is not null && bekannt.Ort == tor.Ort) return bekannt;
        var aussen = new Vector3(tor.Aussen.X, 0, tor.Aussen.Z);
        if (aussen.LengthSquared() < 1e-6f) return _torlagen[name] = null;
        aussen = aussen.Normalized();
        // Mitte der Durchfahrt: der Anker „Anker_Tuer“ liegt 3 m vor dem Torbau (Feldseite), die Torburg steht auf dem Ort des Tores
        var anker = tor.Ort;
        var quer = aussen.Cross(Vector3.Up).Normalized();
        var gasse = Array.Empty<Vector3>();
        if (_netz.Knoten.Count > 0)
        {
            var beginn = anker - aussen * (TorPostenTiefe + 1f);
            var pfad = _netz.Pfad(_netz.KnotenBei(beginn), _netz.KnotenBei(_markt));
            var liste = new List<Vector3> { beginn };
            foreach (var punkt in pfad)
                if (liste[^1].DistanceTo(punkt) > 1.0f) liste.Add(punkt);
            gasse = liste.ToArray();
        }
        return _torlagen[name] = new Torlage { Ort = tor.Ort, Anker = anker, Aussen = aussen, Quer = quer, Gasse = gasse };
    }

    /// <summary>Der Standplatz eines Wächters am Tor (auf dem Boden): Platz 0 links, 1 rechts, 2 und 3 jeweils weiter außen.</summary>
    private Vector3 TorStandplatz(Torlage lage, int platz)
    {
        var seite = platz % 2 == 0 ? -1f : 1f;
        var p = lage.Anker - lage.Aussen * TorPostenTiefe + lage.Quer * (seite * (TorPostenSeite + platz / 2 * TorPostenAbstand));
        return AufFuss(p);
    }

    /// <summary>Der Weg vom Standplatz in die Gasse (gemerkt je Tor und Platz): Standplatz, dann die Gasse zum Markt.</summary>
    private (Vector3[] Pfad, float Laenge) TorWeg(Torlage lage, int platz)
    {
        if (lage.Wege.TryGetValue(platz, out var bekannt)) return bekannt;
        var stand = TorStandplatz(lage, platz);
        var pfad = new List<Vector3> { stand };
        foreach (var punkt in lage.Gasse)
            if (pfad[^1].DistanceTo(punkt) > 1.0f) pfad.Add(punkt);
        var feld = pfad.ToArray();
        return lage.Wege[platz] = (feld, Gassennetz.Laenge(feld));
    }

    /// <summary>Das Aussehen eines Torwächters: Stadtfarben, Eisenhut, Hellebarde; Volk und Geschlecht aus der Person.</summary>
    private FigurAussehen TorwaechterAussehen(Einwohner e)
    {
        var haare = Haarfarben(e.Volk);
        var alt = _simulation is { } sim && e.Alter(sim.Zeit.Jahr) >= GrauAb;
        return new FigurAussehen(e.Volk, e.Geschlecht == Geschlecht.Frau, Stadtrot, new Color(0.20f, 0.19f, 0.21f), HautVon(e.Volk),
            alt ? GrauesHaar : haare[e.Nummer % haare.Length], new Color(0.45f, 0.45f, 0.48f), Kopfbedeckung.Eisenhut, Last.Hellebarde, Waffenrock: true);
    }

    /// <summary>Wie viele Meter es ein Wächter in <paramref name="weg"/> Spielminuten schafft (Gehtempo wie die Passanten, eingestellte Rundendauer).</summary>
    private float TorWegLaenge(int weg) =>
        (float)(Gehtempo.MeterJeSpielminute(Gehtempo.Gehen, _simulation!.Takt.RundenDauerSekunden) * weg);

    /// <summary>
    /// Jedes Bild: Zeigt die Torwächter nach dem Dienstplan des Kerns (Posten, Anmarsch, Abmarsch). Wächter, deren Tor nicht im Bild liegt, sind
    /// versteckt und kosten nichts; Figuren und Laternen entstehen erst, wenn sie gebraucht werden.
    /// </summary>
    private void TorwacheAktualisieren(Stadtwirtschaft w, Spielzeit zeit, double bruchteil, double delta)
    {
        var uhr = TorMessung ? System.Diagnostics.Stopwatch.StartNew() : null;
        TorwacheRechnen(w, zeit, bruchteil, delta);
        if (uhr is null) return;
        _torMs += uhr.Elapsed.TotalMilliseconds;
        _torBilder++;
        if (_torBilder >= 600)
        {
            GD.Print($"[torwache] Rechenzeit {_torMs / _torBilder:0.000} ms je Bild ({_torBilder} Bilder), {_torposten.Values.Count(p => p.Sichtbar)} Wächter sichtbar, {_torposten.Count} Figuren gebaut");
            (_torMs, _torBilder) = (0, 0);
        }
    }

    /// <summary>Startoption <c>--messung=…</c> (oder <c>--torwachemessung=1</c>): die Rechenzeit der Torwache je Bild alle 600 Bilder ins Protokoll.</summary>
    private static readonly bool TorMessung = Wasserarten.Startoption("messung") is not null || Wasserarten.Startoption("torwachemessung") is "1";
    private double _torMs;
    private int _torBilder;

    private void TorwacheRechnen(Stadtwirtschaft w, Spielzeit zeit, double bruchteil, double delta)
    {
        if (_simulation is null || TorwacheAus || w.Einwohnerschaft is not { IstAktiv: true } ew || !ew.Torwache.IstAktiv || _tore.Count == 0)
        {
            if (_torposten.Count > 0) TorwacheVerbergen();
            return;
        }
        var gezeigt = Fenster.PassantenMenge > 0 && _raum is null && _innen is null;
        if (!gezeigt)
        {
            if (_torposten.Count > 0) TorwacheVerbergen();
            return;
        }
        var minute = zeit.UhrzeitInMinuten + bruchteil;
        var tag = zeit.Jahr;
        var sekunden = _simulation.Takt.IstPausiert ? 0 : delta;
        var an = _lampenAn > TragenAb;
        var weg = ew.Daten.Torwache?.Weg ?? 15;
        var laenge = TorWegLaenge(weg);
        var tempoMal = (float)_simulation.Takt.Tempo;
        _torBauten = 0;

        foreach (var wache in ew.Torwache.Waechter)
        {
            var nummer = wache.Person.Nummer;
            _torposten.TryGetValue(nummer, out var posten);
            var verstecken = true;
            if (PassantGezeigt(nummer) && TorlageVon(wache.Tor) is { } lage && ImBild(lage.Anker, TorBildRand))
            {
                var (dienst, anteil) = ew.Torwache.Dienst(wache, minute, tag);
                if (dienst != Wachdienst.Frei)
                {
                    posten ??= TorpostenBauen(wache);
                    verstecken = !TorpostenStellen(posten, lage, dienst, (float)anteil, minute, laenge, sekunden, tempoMal, an);
                }
            }
            if (verstecken && posten is { Sichtbar: true }) TorpostenVerstecken(posten);
        }
    }

    private void TorwacheVerbergen()
    {
        foreach (var posten in _torposten.Values)
            if (posten.Sichtbar) TorpostenVerstecken(posten);
    }

    private void TorpostenVerstecken(Torposten posten)
    {
        posten.Knoten.Visible = posten.Sichtbar = false;
        if (posten.Licht is { } licht) TraglichtSchalten(licht, false);
    }

    private Torposten TorpostenBauen(Torwaechter wache)
    {
        if (_torwacheEbene is null)
        {
            _torwacheEbene = new Node3D { Name = "Torwache" };
            AddChild(_torwacheEbene);
        }
        var knoten = new Node3D { Name = $"Torwaechter_{wache.Person.Nummer}", Visible = false };
        _torwacheEbene.AddChild(knoten);
        var posten = new Torposten { Wache = wache, Knoten = knoten };
        _torposten[wache.Person.Nummer] = posten;
        return posten;
    }

    /// <summary>Setzt einen Wächter an seine Stelle. Gibt false zurück, wenn er (noch) nicht zu zeigen ist.</summary>
    private bool TorpostenStellen(Torposten posten, Torlage lage, Wachdienst dienst, float anteil, double minute, float laenge, double sekunden, float tempoMal, bool laterne)
    {
        var wache = posten.Wache;
        if (posten.Figur is null)
        {
            if (_torBauten >= 2) return false; // höchstens zwei neue Figuren je Bild
            _torBauten++;
            posten.Figur = FigurEinsetzen(posten.Knoten, TorwaechterAussehen(wache.Person), wache.Person.Nummer);
            if (posten.Figur is null) return false;
        }
        var figur = posten.Figur;
        var stand = TorStandplatz(lage, wache.Platz);
        var bewegung = "Stehen";
        var takt = 1f;
        float blick;
        Vector3 ort = stand;

        if (dienst == Wachdienst.Posten)
        {
            // Grundhaltung: schräg zur Durchfahrt hin und nach draußen; naht jemand, ihm zu
            var seite = wache.Platz % 2 == 0 ? -1f : 1f;
            var grund = lage.Aussen * 0.7f - lage.Quer * seite * 0.7f;
            var ziel = grund;
            if (TorGast(lage, sekunden) is { } gast)
            {
                var zu = gast - stand;
                zu.Y = 0;
                if (zu.LengthSquared() > 0.25f) ziel = zu.Normalized();
            }
            blick = Mathf.Atan2(ziel.X, ziel.Z);
            // ein paar Schritte in die Stadt und zurück (fest aus Nummer und Spielminute)
            var abschnitt = (long)((minute + wache.Person.Nummer * 5) / TorSchrittMinuten);
            if (Streuung(wache.Person.Nummer, abschnitt) % 4 == 0)
            {
                var u = (float)((minute + wache.Person.Nummer * 5) / TorSchrittMinuten - abschnitt);
                var s = u < 0.25f ? 0 : u < 0.4f ? (u - 0.25f) / 0.15f : u < 0.55f ? 1 : u < 0.7f ? 1 - (u - 0.55f) / 0.15f : 0;
                var geht = u is >= 0.25f and < 0.4f or >= 0.55f and < 0.7f;
                if (s > 0)
                {
                    ort = AufFuss(stand - lage.Aussen * (s * TorSchrittWeite));
                    if (geht)
                    {
                        var vorwaerts = u < 0.5f;
                        var richtung = vorwaerts ? -lage.Aussen : lage.Aussen;
                        blick = Mathf.Atan2(richtung.X, richtung.Z);
                        bewegung = "Gehen";
                        takt = Gehtempo.Gehen * tempoMal / (GehenTempo * figur.Beinfaktor);
                    }
                }
            }
        }
        else
        {
            // Anmarsch aus der Stadt oder Abmarsch dorthin: ein Stück der Gasse, so lang wie der Weg in der Zeit
            var (pfad, ganz) = TorWeg(lage, wache.Platz);
            if (ganz < 1f) return false;
            var strecke = Mathf.Min(laenge, ganz - 0.5f);
            var meter = (dienst == Wachdienst.Anmarsch ? 1f - anteil : anteil) * strecke;
            var hier = Gassennetz.PunktAuf(pfad, meter / ganz, ganz);
            var weiter = Gassennetz.PunktAuf(pfad, Mathf.Min(1f, (meter + 0.5f) / ganz), ganz);
            var richtung = (dienst == Wachdienst.Anmarsch ? hier - weiter : weiter - hier);
            richtung.Y = 0;
            richtung = richtung.LengthSquared() > 1e-6f ? richtung.Normalized() : lage.Aussen;
            blick = Mathf.Atan2(richtung.X, richtung.Z);
            // Rechtsverkehr wie die Passanten, am Standplatz selbst ohne Versatz
            var rechts = new Vector3(-richtung.Z, 0, richtung.X) * (0.5f * Mathf.Min(1f, meter / 3f));
            ort = meter < 0.05f ? stand : AufFuss(hier + rechts);
            bewegung = "Gehen";
            takt = Gehtempo.Gehen * tempoMal / (GehenTempo * figur.Beinfaktor);
        }

        if (!posten.Sichtbar)
        {
            posten.Knoten.Visible = posten.Sichtbar = true;
            if (!posten.BlickGesetzt) { posten.Blick = blick; posten.BlickGesetzt = true; }
        }
        // sanft drehen (rund 6 je Sekunde); beim Gehen gleich in Gehrichtung
        posten.Blick = bewegung == "Gehen" ? blick : Mathf.LerpAngle(posten.Blick, blick, (float)Math.Min(1.0, sekunden * 6.0));
        posten.Knoten.Position = ort;
        posten.Knoten.Rotation = new Vector3(0, posten.Blick, 0);

        BewegungSetzen(figur, bewegung, takt);
        FigurImBildBewegen(figur, ort, sekunden);

        if (laterne && posten.Licht is null)
            posten.Licht = TraglichtBauen(figur, wache.Person.Volk, Lichtart.Handlaterne, 7000 + wache.Person.Nummer % 1000);
        if (posten.Licht is { } licht) TraglichtSchalten(licht, laterne);
        return true;
    }

    /// <summary>
    /// Wer ist gerade nahe der Durchfahrt? Der nächste sichtbare Passant oder Karren innerhalb von <see cref="TorGastRadius"/> Metern – nur alle
    /// 0,4 Sekunden gesucht, je Tor einmal für beide Wächter. Null, wenn niemand da ist.
    /// </summary>
    private Vector3? TorGast(Torlage lage, double sekunden)
    {
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (jetzt - lage.Gesucht < 0.4) return lage.Gast;
        lage.Gesucht = jetzt;
        lage.Gast = null;
        var beste = TorGastRadius * TorGastRadius;
        foreach (var p in _passanten.Values)
        {
            if (!p.Knoten.Visible) continue;
            var d = p.Knoten.Position - lage.Anker;
            var q = d.X * d.X + d.Z * d.Z;
            if (q < beste) { beste = q; lage.Gast = p.Knoten.Position; }
        }
        foreach (var k in _karren.Values)
        {
            if (!k.Wurzel.Visible) continue;
            var d = k.Wurzel.Position - lage.Anker;
            var q = d.X * d.X + d.Z * d.Z;
            if (q < beste) { beste = q; lage.Gast = k.Wurzel.Position; }
        }
        return lage.Gast;
    }

    /// <summary>Welcher Torwächter ist an dieser Bildschirmstelle (wie <see cref="PassantBei"/>)? Nähere Treffer als <paramref name="besterAbstand"/> gewinnen.</summary>
    private string? TorwaechterBei(Vector2 bildschirm, ref float besterAbstand)
    {
        string? beste = null;
        foreach (var (nummer, p) in _torposten)
        {
            if (!p.Sichtbar) continue;
            var mitte = p.Knoten.GlobalPosition + new Vector3(0, 0.9f, 0);
            if (StadtKamera.IsPositionBehind(mitte)) continue;
            var abstand = StadtKamera.UnprojectPosition(mitte).DistanceTo(bildschirm);
            if (abstand < besterAbstand) { besterAbstand = abstand; beste = "einwohner:" + nummer; }
        }
        return beste;
    }

    /// <summary>Text der Auswahltafel für einen Torwächter: Rolle, Volk, Alter, Tor und was er gerade tut (keine Wohnhaus-Zeile).</summary>
    private string TorwaechterText(Einwohnerschaft ew, Torwaechter wache, string volk, int jahr)
    {
        var e = wache.Person;
        var tor = wache.Tor;
        var (dienst, _) = ew.Torwache.DienstJetzt(wache);
        var tun = dienst switch
        {
            Wachdienst.Posten => $"Steht Wache am {tor}",
            Wachdienst.Anmarsch => $"Kommt zur Ablösung ans {tor}",
            Wachdienst.Abmarsch => "Geht nach der Wache heim",
            _ => "Hat dienstfrei",
        };
        return string.Join("\n", e.Name, "", $"{ew.RollenName(e)} · {volk}, {e.Alter(jahr)} Jahre · Stadtwache am {tor}", tun + ".");
    }
}
