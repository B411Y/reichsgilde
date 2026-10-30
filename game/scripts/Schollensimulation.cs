using System;
using System.Collections.Generic;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Bewegung der treibenden Eisschollen (Schollen 2, <c>docs/wasser/phase3.md</c>): eine eigene 2D-Simulation in den Laufachsen des Flusses
/// (Stelle s stromab, quer q), ohne Knoten und ohne Physik-Engine – sie rechnet nur mit <see cref="Mathf"/> und <see cref="Vector2"/>, damit der
/// Nachbau im Notizordner sie unverändert einbinden kann. <see cref="Schollenfeld"/> legt sie an und zeichnet die Schollen.
/// <list type="bullet">
/// <item><b>Form:</b> Der Umriss jeder Scholle kommt wie im Shader (<c>eis_scholle.gdshaderinc</c>, <c>umriss</c> mit zackigem Rand) aus ihrer
/// Saat; daraus beim Bau Fläche, Schwerpunkt, Trägheit und ein oder zwei Stoßkreise (länglich: zwei auf der Hauptachse). Gerechnet wird um den
/// Schwerpunkt; der Ursprung des Netzes liegt daneben.</item>
/// <item><b>Löser:</b> weiche Kontakte nach dem „Soft Step“ von Box2D v3 (eigene Umsetzung der veröffentlichten Formeln): fester Schritt 1/20 s
/// (Sammler, gezeichnet wird dazwischen gemittelt), Kontakte einmal je Bild gesucht (mit Vorlauf), Warmstart, ein Durchgang mit begrenztem
/// Ausschieben (Eis 0,3 m/s), Lage fortschreiben, ein Durchgang ohne Ausschieben – so bleibt kein Tempo vom Ausschieben übrig. Kein Rückprall;
/// Reibung am Berührpunkt mit Haften und Gleiten (die Drehung zählt mit) und Rollwiderstand; im dichten Eis (zwei belastete Nachbarn) klingt die
/// Drehung schnell ab.</item>
/// <item><b>Wasser:</b> Zug quadratisch im Tempo (Form- und Reibungswiderstand nach Damsgaard u. a. 2018) mit kleinem linearen Anteil; die
/// Strömung ist in der Mitte schneller, an Brücken in den Bögen schneller und im Lee der Pfeiler langsamer (Kehrwasser), vor dem Pfeilerkopf
/// teilt sie sich; die Drehung strebt zur halben Wirbelstärke des Querprofils (Uferseiten drehen gegensinnig).</item>
/// <item><b>Pfeiler:</b> Steinpfeiler als Vieleck mit spitzem Eisbrecher stromauf (Kopf glatt, Flanke rau), Joche, Prahm und Kähne als Kapseln
/// (bewegte mit ihrem Tempo, gekappt). Vor einer Brücke bildet sich so von selbst ein Stau; er löst sich durch Brüche, durch Schollen, die an der
/// Staukante untertauchen, und – nur wenn ein Pfeiler lange unter Last steht – über ein Notventil.</item>
/// <item><b>Bruch:</b> beim harten Aufprall oder unter Druck an einem Hindernis, mit einer Chance; die Stücke sind Ausschnitte derselben Scholle
/// um den Ursprung ihres Netzes (der Shader schneidet sie), sie liegen genau aneinander und kommen aus einem Vorrat freier Plätze.</item>
/// <item><b>Schmelzen:</b> im Hochwinter nur Abrieb (Ecken runder, Rand nass); im Eisgang folgt jede Scholle einem Ziel aus dem Tauwetter (kleiner,
/// dünner, grau), gedrosselt in Echtzeit; zu kleine sinken bündig ab und vergehen. Was vergeht oder das Ende des Laufs erreicht, kommt oben neu.</item>
/// </list>
/// Gestoßen wird nur im Fenster um den Blickpunkt (±160 m, eine nahe Brücke ganz); weiter weg treiben die Schollen nur, an Pfeilern seitlich vorbei.
/// Keine Speicheranforderung je Bild: Alle Felder entstehen beim Bau. Reine Darstellung, nicht im Kern, nicht deterministisch.
/// </summary>
public sealed class Schollensimulation
{
    /// <summary>Ein Punkt der Mittellinie (je Meter): Ort, Höhe des Spiegels, halbe Breite, Fließrichtung (Einheitsvektor).</summary>
    public readonly record struct Laufpunkt(Vector2 Ort, float Spiegel, float Halb, Vector2 Richtung);

    /// <summary>Form eines Hindernisses: Kapsel entlang der längeren Seite oder Steinpfeiler mit Eisbrecher stromauf (Vieleck).</summary>
    public enum Hindernisform : byte { Kapsel, Eisbrecher }

    /// <summary>Ein Hindernis in Laufachsen: quer (m), Stelle (m), halbe Ausdehnung quer und längs, Tempo (bewegte; m/s) und Form.</summary>
    public readonly record struct Hindernis(float Quer, float Stelle, float HalbQuer, float HalbLaengs, float VQuer = 0f, float VStelle = 0f,
        Hindernisform Form = Hindernisform.Kapsel);

    private struct Scholle
    {
        public float S0, Anteil, Halbmesser, Dicke, Winkel, Dreh, Schwelle;
        public int Saat;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werte (öffentlich, damit der Nachbau sie abstimmen kann)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Fester Schritt der Simulation (s) und höchstens so viele je Bild (darunter läuft sie langsamer als die Uhr).</summary>
    public const float H = 1f / 15f;
    public const int MaxSchritte = 3;

    /// <summary>Weichheit der Kontakte: Frequenz (Hz) und Dämpfung (Box2D: contactHertz, contactDampingRatio).</summary>
    public static float KontaktHz = 10f, Daempfung = 5f;

    /// <summary>Höchstes Ausschiebetempo (m/s): Eis gegen Eis, an Hindernissen, an der Eiskante.</summary>
    public static float SchubEis = 0.3f, SchubHindernis = 0.5f, SchubKante = 0.5f;

    /// <summary>Reibung (Haften, Gleiten ab 2 cm/s Schlupf): Eis–Eis, Eis–Stein an der Flanke, am glatten Kopf, Eis–Holz, an der Eiskante.</summary>
    public static float MuEisHaft = 0.45f, MuEisGleit = 0.30f, MuSteinHaft = 0.35f, MuSteinGleit = 0.12f, MuKopfHaft = 0.2f, MuKopfGleit = 0.06f,
        MuHolzHaft = 0.3f, MuHolzGleit = 0.12f, MuKanteHaft = 0.25f, MuKanteGleit = 0.15f;

    /// <summary>Rollwiderstand (Anteil des Hebels): Eis gegen Eis (eckige Schollen rollen nicht), sonst.</summary>
    public static float RollEis = 0.4f, RollSonst = 0.2f;

    /// <summary>Höchstes Massenverhältnis je Kontakt (sonst drückt der Löser kleine Schollen in große).</summary>
    public static float MassenVerhaeltnis = 30f;

    /// <summary>Wasserzug: Beiwerte des Form- und des Reibungswiderstands (Damsgaard u. a. 2018) und Zeitkonstante des linearen Anteils (s, + je m).</summary>
    public static float ZugForm = 0.14f, ZugFlaeche = 1.6e-4f, ZugLinear = 15f, ZugLinearJeM = 15f;

    /// <summary>Drehung: Zeitkonstante zur Wunschdrehung (s, + je m) und im dichten Eis (s); Obergrenze frei bzw. mit Hindernis (rad/s, über 1 m geteilt durch den Halbmesser).</summary>
    public static float DrehTau = 12f, DrehTauJeM = 5f, DrehSperreTau = 0.5f, DrehGrenzeFrei = 0.08f, DrehGrenzeHindernis = 0.25f;

    public static bool Warmstart = true;

    /// <summary>Bruch: Faktor auf alle Chancen (Startoption <c>--schollenbruch</c>), Grenze für den Druck (Pa · Dicke · Breite), Rate (1/s).</summary>
    public static float BruchFaktor = 1f, BruchSigma = 4000f, BruchRate = 0.5f;

    /// <summary>Untertauchen an der Staukante an (Faktor auf die Chance).</summary>
    public static float TauchFaktor = 1f;

    /// <summary>Strömung an Brücken (Bögen, Kopf, Kehrwasser) und Nachgeben eines Pfeilers unter Dauerlast (Sekunden: zufällig von … bis).</summary>
    public static bool BrueckenFeld = true;
    public static float FeldTeilen = 0.1f, FeldWirbel = 0.12f, FeldBogen = 0.15f;
    public static float LastVon = 20f, LastBis = 40f;

    private const float StossFenster = 160f, Zelle = 3f, UferAbstand = 0.8f, Rand = 0.12f;

    /// <summary>
    /// Kontakte neu suchen spätestens nach so viel Simulationszeit (s); dazwischen gilt die Liste weiter (Warmstart von selbst). Der Vorlauf jedes
    /// Kontakts reicht für <see cref="SuchVorlauf"/> s beim Tempo, mit dem sich die beiden nähern.
    /// </summary>
    private const float SuchAbstand = 0.15f, SuchVorlauf = 0.2f;
    private const int Ecken = 24;
    public const int SaatZahl = 2048;
    private const float Tau = Mathf.Tau;

    // Steinpfeiler in Laufachsen um seine Mitte (tools/modelle/wasser.py: Körper ±2,7 m längs, ±1 m quer, Eisbrecher stromauf bis 4,5 m, Vorkopf
    // stromab bis 3,7 m); Kanten 0 und 5 sind der Kopf
    private static readonly float[] PfS = { -4.5f, -2.7f, 2.7f, 3.7f, 2.7f, -2.7f };
    private static readonly float[] PfQ = { 0f, 1f, 1f, 0f, -1f, -1f };
    private static readonly float[] PfNs = new float[6], PfNq = new float[6];

    static Schollensimulation()
    {
        for (var k = 0; k < 6; k++)
        {
            var m = (k + 1) % 6;
            var (es, eq) = (PfS[m] - PfS[k], PfQ[m] - PfQ[k]);
            var l = MathF.Sqrt(es * es + eq * eq);
            var (ns, nq) = (eq / l, -es / l);
            if (ns * (PfS[k] + PfS[m]) + nq * (PfQ[k] + PfQ[m]) < 0) (ns, nq) = (-ns, -nq); // nach außen
            (PfNs[k], PfNq[k]) = (ns, nq);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Felder
    // ═════════════════════════════════════════════════════════════════════════

    private readonly Laufpunkt[] _lauf;
    private readonly float _laenge, _tempo, _randeis, _halbMax;
    private readonly Scholle[] _schollen;
    private readonly List<Hindernis> _feste;
    private readonly int _n, _plaetze;
    private uint _zufall;
    private double _zeit;
    private float _sammler;

    // Formen je Saat (Einheit: Halbmesser der Scholle; Kreise relativ zum Schwerpunkt, im Netz)
    private readonly float[] _umX = new float[SaatZahl * Ecken], _umY = new float[SaatZahl * Ecken];
    private readonly FormWerte[] _formen = new FormWerte[SaatZahl];

    private struct FormWerte
    {
        public float A, Cx, Cy, J, Huelle, Laenge;
        public int Nk;
        public float K0x, K0y, K0r, K1x, K1y, K1r;
    }

    // je Platz (Stammschollen 0 … n−1, dahinter der Vorrat für Bruchstücke)
    private readonly int[] _stamm;                 // Stammscholle (für Saat, Dicke, Schwelle …); −1 = frei
    private readonly float[] _s, _q, _vs, _vq, _w, _om, _sAlt, _qAlt, _wAlt;
    private readonly float[] _r0, _rGleich, _rg, _rs, _r, _dk, _mTau, _mAbrieb, _invM, _invI, _drag, _dragLin, _drehK;
    private readonly FormWerte[] _form;
    private readonly float[] _sekA, _sekB;         // Ausschnitt um den Netz-Ursprung (Winkel im Netz); −1 = ganz
    private readonly byte[] _gen;
    private readonly float[] _sperre, _duldung;
    private readonly byte[] _tauchen;              // 0 normal, 1 taucht ab, 2 unter Wasser, 3 taucht auf, 4 vergeht
    private readonly float[] _tauchZeit;
    private readonly bool[] _frisch, _ohneWarm;
    private readonly int[] _kontaktZahl, _belastet;
    private readonly bool[] _anHindernis;
    private readonly float[] _tiefe, _druck, _schiebS, _schiebQ, _schiebW;
    // Aufprall im Bild (für Bruch und Untertauchen)
    private readonly float[] _aufprallV, _aufprallPs, _aufprallPq, _aufprallNs, _aufprallNq, _stossV, _stossNs, _stossNq;
    private readonly float[] _druckMax, _druckPs, _druckPq, _druckNs, _druckNq;
    private readonly int[] _stossMit;
    // Stoßkreise in Laufachsen (je Platz zwei; Halbmesser 0 = keiner)
    private readonly float[] _wx, _wy, _wr;
    private readonly int[] _frei;                  // Stapel freier Vorratsplätze
    private readonly bool[] _neu;                  // Größe, Masse und Zug neu rechnen
    private readonly float[] _huelle;              // Hülle der Stoßkreise (m)
    private readonly int[] _taucher;
    private int _taucherZahl;
    private int _freiZahl;

    /// <summary>Zustand je Platz für Zeichnung und Messung (<see cref="Schollenmessung.Aus"/> …) und Ereignisse im Bild.</summary>
    public readonly byte[] Zustand, Ereignis;

    /// <summary>Gezeichnete Lage je Platz: Schwerpunkt (Laufachsen), Winkel des Netzes in Laufachsen, Halbmesser, Dicke, Schmelzgrad, Kippen (rad,
    /// Vorderkante nach unten negativ) und Absinken (m); den Ursprung des Netzes liefert <see cref="Ursprung"/>.</summary>
    public readonly float[] ZS, ZQ, ZW, ZR, ZDicke, ZM, ZKipp, ZSenk;

    // Gitter
    private readonly int[] _kopf, _naechster, _fenster, _sortiert, _zelleVon, _pI;  // Gitter: Anfang je Zelle, Zähler, sortierte Plätze
    private readonly float[] _pS, _pQ, _pH, _pVs, _pVq;
    private readonly int _zellenS, _zellenQ;
    private int _fensterZahl;
    private float _huelleMax;

    // Kontakte: Schlüssel und Stöße doppelt (Warmstart über Bilder), die vorbereitete Lage einfach
    private sealed class Kontaktfeld
    {
        public readonly int[] A, B;
        public readonly byte[] Art, KA, KB, Beruehrt;
        public readonly short[] Index;
        public readonly float[] Ln, Lt, Lr;
        public readonly int[] Ab, Zahl;

        public Kontaktfeld(int k, int plaetze)
        {
            (A, B, Art, KA, KB, Beruehrt, Index) = (new int[k], new int[k], new byte[k], new byte[k], new byte[k], new byte[k], new short[k]);
            (Ln, Lt, Lr) = (new float[k], new float[k], new float[k]);
            (Ab, Zahl) = (new int[plaetze], new int[plaetze]);
        }
    }

    private const byte ArtPaar = 0, ArtPfeiler = 1, ArtKapsel = 2, ArtBewegt = 3, ArtKante = 4;
    private Kontaktfeld _kNeu, _kAlt;
    private readonly int _kMax;
    private int _kZahl;
    private readonly float[] _cNs, _cNq, _cRAs, _cRAq, _cRBs, _cRBq, _cS0, _cMn, _cMt, _cMr, _cVBs, _cVBq, _cIma, _cIia, _cImb, _cIib,
        _cMuH, _cMuG, _cRoll, _cSchub, _cReff, _cTol, _cVn0;

    // Brücken (aus den festen Hindernissen) und das Notventil je Pfeiler
    private readonly float[] _brueckeS;
    private readonly int[][] _brueckePfeiler;
    private readonly float[] _pfLast, _pfSchwelle, _pfKraft, _pfMaxKraft;
    private readonly float _hindernisVon = float.MaxValue, _hindernisBis = float.MinValue; // Stellen der festen Hindernisse
    private readonly int[] _pfMaxScholle;
    private List<Hindernis> _beweglich = new();

    /// <summary>Stelle des Blickpunkts im letzten Bild und das Stoßfenster.</summary>
    public float SBlick { get; private set; }

    private float _fensterVon, _fensterBis, _brueckeVon = 1, _brueckeBis = 0;
    private float _dichte, _eisRand, _tauen, _bruchKonto = 3f, _seitSuche = 1f;
    private bool _suchen = true; // Liste der Kontakte gilt nicht mehr (neu im Fenster, Bruch, Tauchen, Platz frei oder neu belegt)
    private bool _erstesBild = true;
    private int _treibenRunde, _bildNr, _pendelnBild = -1, _suchNr;
    private readonly int[] _inListe; // Nummer der Suche, in der die Scholle zuletzt einen Kontakt bekam
    private readonly float[] _pendeln;
    private readonly float _drehSperreK = MathF.Exp(-H / DrehSperreTau);

    /// <summary>Für die Messung (null ohne <c>--schollenmessung</c>).</summary>
    public Schollenmessung? Messung { get; }

    /// <summary>Wie viele Stammschollen es gibt, wie viele Plätze (mit Vorrat) und wie viele im letzten Bild aktiv bzw. im Fenster waren.</summary>
    public int Anzahl => _n;
    public int Plaetze => _plaetze;
    public int Aktiv { get; private set; }
    public int ImFenster => _fensterZahl;
    public int Kontakte => _kZahl;
    public int Teilschritte { get; private set; }
    public double SimulationMs { get; private set; }
    public double SucheMs { get; private set; }
    public double LoesenMs { get; private set; }

    /// <summary>Protokoll je Bruch (nur mit <c>--schollenbruch</c>): Zeit, Ort und Stücke.</summary>
    public Action<string>? BruchProtokoll;

    /// <param name="lauf">Mittellinie je Meter (Index = Stelle in m).</param>
    /// <param name="tempo">Strömung (m/s); die Schollen treiben mit 0,9 davon.</param>
    /// <param name="randeis">Reichweite des Randeises bei vollem Eis (m).</param>
    /// <param name="saat">Startwert aus der Karte.</param>
    /// <param name="kleinste">Kleinster Halbmesser (nach Grafikstufe).</param>
    /// <param name="feste">Feste Hindernisse (Brückenpfeiler) in Laufachsen.</param>
    /// <param name="messung">Ob <see cref="Messung"/> angelegt wird.</param>
    public Schollensimulation(Laufpunkt[] lauf, float tempo, float randeis, int saat, float kleinste, List<Hindernis> feste, bool messung = false)
    {
        _lauf = lauf;
        _laenge = lauf.Length - 1;
        _tempo = Mathf.Max(tempo, 0.05f) * 0.9f;
        _randeis = randeis;
        _feste = feste;
        foreach (var p in lauf) _halbMax = Mathf.Max(_halbMax, p.Halb);
        _schollen = Verteilen(saat, kleinste);
        _n = _schollen.Length;
        _plaetze = _n + Math.Max(64, _n * 15 / 100);
        _zufall = (uint)saat * 2654435761u + 7u;
        var p0 = _plaetze;
        _stamm = new int[p0];
        (_s, _q, _vs, _vq, _w, _om, _sAlt, _qAlt, _wAlt) = (new float[p0], new float[p0], new float[p0], new float[p0], new float[p0], new float[p0], new float[p0], new float[p0], new float[p0]);
        (_r0, _rGleich, _rg, _rs, _r, _dk, _mTau, _mAbrieb) = (new float[p0], new float[p0], new float[p0], new float[p0], new float[p0], new float[p0], new float[p0], new float[p0]);
        (_invM, _invI, _drag, _dragLin, _drehK) = (new float[p0], new float[p0], new float[p0], new float[p0], new float[p0]);
        _form = new FormWerte[p0];
        (_sekA, _sekB, _gen) = (new float[p0], new float[p0], new byte[p0]);
        (_sperre, _duldung, _tauchen, _tauchZeit) = (new float[p0], new float[p0], new byte[p0], new float[p0]);
        (_frisch, _ohneWarm, _kontaktZahl, _belastet, _anHindernis) = (new bool[p0], new bool[p0], new int[p0], new int[p0], new bool[p0]);
        (_tiefe, _druck, _schiebS, _schiebQ, _schiebW) = (new float[p0], new float[p0], new float[p0], new float[p0], new float[p0]);
        (_aufprallV, _aufprallPs, _aufprallPq, _aufprallNs, _aufprallNq) = (new float[p0], new float[p0], new float[p0], new float[p0], new float[p0]);
        (_stossV, _stossNs, _stossNq, _stossMit) = (new float[p0], new float[p0], new float[p0], new int[p0]);
        (_druckMax, _druckPs, _druckPq, _druckNs, _druckNq) = (new float[p0], new float[p0], new float[p0], new float[p0], new float[p0]);
        (_wx, _wy, _wr) = (new float[2 * p0], new float[2 * p0], new float[2 * p0]);
        (Zustand, Ereignis) = (new byte[p0], new byte[p0]);
        (ZS, ZQ, ZW, ZR, ZDicke, ZM, ZKipp, ZSenk) = (new float[p0], new float[p0], new float[p0], new float[p0], new float[p0], new float[p0],
            new float[p0], new float[p0]);
        _frei = new int[p0];
        (_neu, _huelle, _taucher, _pendeln, _inListe) = (new bool[p0], new float[p0], new int[p0], new float[p0], new int[p0]);
        (_sortiert, _zelleVon, _pI) = (new int[p0], new int[p0], new int[p0]);
        (_pS, _pQ, _pH, _pVs, _pVq) = (new float[p0], new float[p0], new float[p0], new float[p0], new float[p0]);
        _fenster = new int[p0];
        _zellenS = (int)MathF.Ceiling(_laenge / Zelle) + 1;
        _zellenQ = (int)MathF.Ceiling(2f * _halbMax / Zelle) + 1;
        _kopf = new int[_zellenS * _zellenQ + 2];
        _naechster = new int[_zellenS * _zellenQ + 2];
        _kMax = p0 * 8;
        (_kNeu, _kAlt) = (new Kontaktfeld(_kMax, p0), new Kontaktfeld(_kMax, p0));
        float[] F() => new float[_kMax];
        (_cNs, _cNq, _cRAs, _cRAq, _cRBs, _cRBq, _cS0, _cMn, _cMt, _cMr) = (F(), F(), F(), F(), F(), F(), F(), F(), F(), F());
        (_cVBs, _cVBq, _cIma, _cIia, _cImb, _cIib, _cMuH, _cMuG, _cRoll, _cSchub, _cReff, _cTol, _cVn0) = (F(), F(), F(), F(), F(), F(), F(), F(), F(), F(), F(), F(), F());

        // Formen aller Saaten (einmal; die Formberechnung steht im Ladeprotokoll)
        for (var sa = 0; sa < SaatZahl; sa++) FormBerechnen(sa);

        // Stammschollen einsetzen, Vorrat frei
        for (var i = 0; i < _n; i++)
        {
            ref readonly var sch = ref _schollen[i];
            Einsetzen(i, i, sch.S0, sch.Anteil * (Platz(sch.S0, 1f) - sch.Halbmesser), sch.Winkel, frisch: true);
            _rg[i] = 1f;
        }
        for (var i = _plaetze - 1; i >= _n; i--)
        {
            _stamm[i] = -1;
            _frei[_freiZahl++] = i;
        }

        // Brücken: Pfeiler innerhalb von 12 m Stelle; je Pfeiler das Notventil
        var idx = new List<int>();
        for (var k = 0; k < feste.Count; k++) idx.Add(k);
        idx.Sort((a, b) => feste[a].Stelle.CompareTo(feste[b].Stelle));
        var gruppen = new List<List<int>>();
        foreach (var k in idx)
        {
            if (gruppen.Count == 0 || feste[k].Stelle - feste[gruppen[^1][^1]].Stelle > 12f) gruppen.Add(new());
            gruppen[^1].Add(k);
        }
        (_brueckeS, _brueckePfeiler) = (new float[gruppen.Count], new int[gruppen.Count][]);
        for (var b = 0; b < gruppen.Count; b++)
        {
            var summe = 0f;
            foreach (var k in gruppen[b]) summe += feste[k].Stelle;
            _brueckeS[b] = summe / gruppen[b].Count;
            gruppen[b].Sort((x, y) => feste[x].Quer.CompareTo(feste[y].Quer));
            _brueckePfeiler[b] = gruppen[b].ToArray();
        }
        (_pfLast, _pfSchwelle, _pfKraft, _pfMaxKraft, _pfMaxScholle) = (new float[feste.Count], new float[feste.Count], new float[feste.Count], new float[feste.Count], new int[feste.Count]);
        foreach (var h in feste)
        {
            var weit = MathF.Max(MathF.Max(h.HalbLaengs, h.HalbQuer), 4.6f);
            (_hindernisVon, _hindernisBis) = (MathF.Min(_hindernisVon, h.Stelle - weit), MathF.Max(_hindernisBis, h.Stelle + weit));
        }
        for (var k = 0; k < feste.Count; k++) _pfSchwelle[k] = LastVon + (LastBis - LastVon) * Zufall();

        if (messung)
        {
            var daten = new Schollenmessung.Daten
            {
                Plaetze = _plaetze, S = ZS, Q = ZQ, Vs = _vs, Vq = _vq, Om = _om, R0 = _rGleich, R = ZR, Zustand = Zustand, Ereignis = Ereignis,
                KreisS = _wx, KreisQ = _wy, KreisR = _wr, KreisSchritt = 2, Laenge = _laenge,
            };
            var pf = new List<(float, float, float)>();
            foreach (var h in feste) pf.Add((h.Quer, h.Stelle, h.Form == Hindernisform.Eisbrecher ? 1f : h.HalbQuer));
            Messung = new Schollenmessung(daten, pf, s => Platz(s, 1f));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lauf, Verteilung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Länge des Laufs (m).</summary>
    public float Laenge => _laenge;

    /// <summary>Der Punkt der Mittellinie an einer Stelle (gemittelt zwischen den Metern).</summary>
    public Laufpunkt Punkt(float s)
    {
        var i = Math.Clamp((int)s, 0, _lauf.Length - 2);
        var t = Mathf.Clamp(s - i, 0f, 1f);
        var (a, b) = (_lauf[i], _lauf[i + 1]);
        return new Laufpunkt(a.Ort.Lerp(b.Ort, t), Mathf.Lerp(a.Spiegel, b.Spiegel, t), Mathf.Lerp(a.Halb, b.Halb, t), a.Richtung.Lerp(b.Richtung, t).Normalized());
    }

    private float HalbAn(float s)
    {
        var i = Math.Clamp((int)s, 0, _lauf.Length - 2);
        var t = Mathf.Clamp(s - i, 0f, 1f);
        return _lauf[i].Halb + (_lauf[i + 1].Halb - _lauf[i].Halb) * t;
    }

    private float HalbNeigung(float s)
    {
        var i = Math.Clamp((int)s, 0, _lauf.Length - 2);
        return _lauf[i + 1].Halb - _lauf[i].Halb;
    }

    /// <summary>Platz quer von der Mitte (m) beim Eis <paramref name="eis"/>: halbe Breite minus Randeis (mit Zugabe) und Abstand.</summary>
    public float Platz(float s, float eis) => HalbAn(s) - _randeis * eis * 1.5f - UferAbstand;

    /// <summary>Strömungsprofil quer: in der Mitte etwas schneller als die mittlere Strömung, zum Ufer hin langsamer.</summary>
    private static float Profil(float quer, float halb)
    {
        var a = quer / Mathf.Max(halb, 1f);
        return 1.12f - 0.5f * a * a;
    }

    /// <summary>
    /// Der nächste Punkt der Mittellinie zu einem Weltort: (quer, Stelle) in Laufachsen und der Abstand. <paramref name="tipp"/> ist der Index vom
    /// letzten Mal (−1: ganz suchen); liegt der beste Punkt am Rand des Suchbereichs, wird ganz gesucht (die Kamera kann springen).
    /// </summary>
    public (float Quer, float Stelle, float Abstand) InLaufachsen(Vector2 ort, ref int tipp)
    {
        var (beste, besteD) = Suche(ort, tipp < 0 ? 0 : Math.Max(0, tipp - 40), tipp < 0 ? _lauf.Length - 1 : Math.Min(_lauf.Length - 1, tipp + 40));
        if (tipp >= 0 && (beste == Math.Max(0, tipp - 40) || beste == Math.Min(_lauf.Length - 1, tipp + 40)) && beste > 0 && beste < _lauf.Length - 1)
            (beste, besteD) = Suche(ort, 0, _lauf.Length - 1);
        tipp = beste;
        var p = _lauf[beste];
        var quer = new Vector2(-p.Richtung.Y, p.Richtung.X);
        var rel = ort - p.Ort;
        return (rel.Dot(quer), beste + rel.Dot(p.Richtung), MathF.Sqrt(besteD));
    }

    private (int, float) Suche(Vector2 ort, int von, int bis)
    {
        var (beste, besteD) = (von, float.MaxValue);
        for (var i = von; i <= bis; i++)
        {
            var d = _lauf[i].Ort.DistanceSquaredTo(ort);
            if (d < besteD) (besteD, beste) = (d, i);
        }
        return (beste, besteD);
    }

    /// <summary>
    /// Setzt die Schollen von groß nach klein an zufällige Stellen, ohne dass sie sich berühren; in den Feldern fast jede, auf den freien Strecken
    /// nur wenige und keine großen (wie bisher, gleiche Folge von Zufallszahlen). Die Saat der Form liegt in 0–2047 (im Compatibility-Renderer
    /// kommt sie als 16-Bit-Zahl an, bis 2048 genau).
    /// </summary>
    private Scholle[] Verteilen(int saat, float kleinste)
    {
        var rnd = new Random(saat);
        var liste = new List<Scholle>();
        var raster = new Dictionary<(int, int), List<int>>();
        const float rasterM = 4f;
        var versuche = (int)(_laenge * _halbMax * 2f * 0.4f);
        for (var k = 0; k < versuche; k++)
        {
            var halb = 0.25f + 3f * MathF.Pow(1f - (float)k / versuche, 2.4f);
            if (halb < kleinste) break;
            var s = (float)rnd.NextDouble() * _laenge;
            var feld = Feld(s, saat);
            var annahme = Mathf.Lerp(0.05f, 1f, feld) * (halb > 1.4f ? feld : 1f);
            var wurf = (float)rnd.NextDouble();
            var anteilWurf = (float)rnd.NextDouble() * 2f - 1f;
            if (wurf > annahme) continue;
            var platz = Platz(s, 1f);
            if (platz < halb + 0.3f) continue;
            var quer = anteilWurf * (platz - halb);
            var (rs, rq) = ((int)MathF.Floor(s / rasterM), (int)MathF.Floor(quer / rasterM));
            var frei = true;
            for (var i = -2; i <= 2 && frei; i++)
                for (var j = -2; j <= 2 && frei; j++)
                    if (raster.TryGetValue((rs + i, rq + j), out var nachbarn))
                        foreach (var n in nachbarn)
                        {
                            var o = liste[n];
                            var oq = o.Anteil * (Platz(o.S0, 1f) - o.Halbmesser);
                            var (ds, dq) = (o.S0 - s, oq - quer);
                            if (ds * ds + dq * dq < (o.Halbmesser + halb + 0.25f) * (o.Halbmesser + halb + 0.25f)) { frei = false; break; }
                        }
            if (!frei) continue;
            if (!raster.TryGetValue((rs, rq), out var zelle)) raster[(rs, rq)] = zelle = new List<int>();
            zelle.Add(liste.Count);
            var dicke = Mathf.Clamp(0.08f + 0.05f * halb + 0.1f * (float)rnd.NextDouble(), 0.08f, 0.32f);
            var winkel = (float)rnd.NextDouble() * Tau;
            var dreh = ((float)rnd.NextDouble() - 0.5f) * 0.06f / (0.5f + halb);
            var schwelle = (float)rnd.NextDouble();
            var formSaat = rnd.Next(0, 9973) % SaatZahl;
            _ = rnd.NextDouble(); // früher der Schnee (jetzt aus der Saat im Shader), damit die Folge gleich bleibt
            liste.Add(new Scholle
            {
                S0 = s, Anteil = anteilWurf, Halbmesser = halb, Dicke = dicke, Winkel = winkel, Dreh = dreh, Schwelle = schwelle, Saat = formSaat,
            });
        }
        return liste.ToArray();
    }

    private static float Feld(float s, int saat)
    {
        var x = s * 0.02f + saat % 1000 * 0.137f;
        var n = Rauschen(x) * 0.65f + Rauschen(x * 2.3f + 5.1f) * 0.35f;
        return Mathf.SmoothStep(0.3f, 0.68f, n);
    }

    private static float Rauschen(float x)
    {
        var i = MathF.Floor(x);
        var f = x - i;
        f = f * f * (3 - 2 * f);
        return Mathf.Lerp(Hash((int)i), Hash((int)i + 1), f);
    }

    private static float Hash(int i)
    {
        unchecked
        {
            var n = (uint)i * 1597334673u;
            n = (n ^ (n >> 15)) * 2246822519u;
            n ^= n >> 13;
            return (n & 0xFFFFFF) / 16777216f;
        }
    }

    private float Zufall()
    {
        _zufall = _zufall * 1664525u + 1013904223u;
        return (_zufall >> 8) * (1f / 16777216f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Umriss und Form (wie eis_scholle.gdshaderinc)
    // ═════════════════════════════════════════════════════════════════════════

    // Dasselbe Rauschen wie w_hash/w_rausch/w_fbm in wasser_wellen.gdshaderinc (Ganzzahl-Hash)
    public static float WHash(float x, float y)
    {
        unchecked
        {
            var qx = (uint)(int)MathF.Floor(x) * 1597334673u;
            var qy = (uint)(int)MathF.Floor(y) * 3812015801u;
            var n = (qx ^ qy) * 1597334673u;
            return (n >> 8) * (1f / 16777216f);
        }
    }

    public static float WRausch(float x, float y)
    {
        var (ix, iy) = (MathF.Floor(x), MathF.Floor(y));
        var (fx, fy) = (x - ix, y - iy);
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        var a = Mathf.Lerp(WHash(ix, iy), WHash(ix + 1, iy), fx);
        var b = Mathf.Lerp(WHash(ix, iy + 1), WHash(ix + 1, iy + 1), fx);
        return Mathf.Lerp(a, b, fy);
    }

    public static float WFbm(float x, float y) =>
        WRausch(x, y) * 0.55f + WRausch(x * 2.13f + 7.1f, y * 2.13f + 7.1f) * 0.3f + WRausch(x * 4.7f + 3.3f, y * 4.7f + 3.3f) * 0.15f;

    /// <summary>Halbmesser des Umrisses in Richtung <paramref name="th"/> (wie <c>umriss</c> und der zackige Rand im Shader), in Halbmessern der Scholle.</summary>
    public static float Umriss(float th, float saat)
    {
        var r = 1.4f;
        var kanten = 5f + MathF.Floor(WHash(saat, 91f) * 5f);
        for (var i = 0; i < 9; i++)
        {
            float fi = i;
            if (fi >= kanten) break;
            var phi = (fi + 0.95f * (WHash(saat, fi * 7f + 3f) - 0.5f)) * 6.2831853f / kanten;
            var d = 0.52f + 0.48f * WHash(saat, fi * 7f + 11f);
            var c = MathF.Cos(th - phi);
            if (c > 0.1f)
            {
                var ri = d / c;
                if (ri < r) r = ri;
            }
        }
        var streck = 0.4f * WHash(saat, 57f);
        var achse = WHash(saat, 63f) * 6.2831853f;
        r *= (1f + streck * MathF.Cos(2f * (th - achse))) / (1f + streck * 0.6f);
        var bucht = WHash(saat, 71f);
        if (bucht > 0.5f)
        {
            var wo = WHash(saat, 77f) * 6.2831853f;
            var dw = MathF.Atan2(MathF.Sin(th - wo), MathF.Cos(th - wo));
            dw *= dw < 0f ? 1.8f : 0.9f;
            r *= 1f - 0.28f * (bucht - 0.5f) / 0.5f * MathF.Exp(-dw * dw * 14f);
        }
        r *= 1f + 0.12f * (WRausch(th * 2.6f + saat * 0.13f, saat * 0.71f) - 0.5f) + 0.08f * (WRausch(th * 7f + saat * 0.29f, saat * 0.37f) - 0.5f);
        return r;
    }

    /// <summary>Ecke <paramref name="k"/> des Umrisses einer Saat (Netz, Einheit Halbmesser) – wie die Ecke k der feinen Scheibe im Shader.</summary>
    public Vector2 UmrissEcke(int saat, int k) => new(_umX[saat * Ecken + k], _umY[saat * Ecken + k]);

    private void FormBerechnen(int saat)
    {
        Span<float> x = stackalloc float[Ecken], y = stackalloc float[Ecken];
        for (var k = 0; k < Ecken; k++)
        {
            // genau wie ScheibenNetz die Ecke legt und der Shader ihren Winkel liest
            var w = Mathf.Tau * k / Ecken;
            var (sin, cos) = (MathF.Sin(w), MathF.Cos(w));
            var r = Umriss(MathF.Atan2(sin, cos), saat);
            (x[k], y[k]) = (cos * r, sin * r);
            (_umX[saat * Ecken + k], _umY[saat * Ecken + k]) = (x[k], y[k]);
        }
        _formen[saat] = FormAusVieleck(x, y);
    }

    /// <summary>
    /// Fläche, Schwerpunkt, Trägheit, Hauptachse und Stoßkreise eines Vielecks (Ecken gegen den Uhrzeigersinn, Einheit Halbmesser): länger als 1,3 ×
    /// breit zwei Kreise auf der Hauptachse (je 1,3 × Inkreis), sonst einer im Schwerpunkt (höchstens flächengleich).
    /// </summary>
    private static FormWerte FormAusVieleck(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        var e = x.Length;
        double a = 0, cx = 0, cy = 0, ixx = 0, iyy = 0, ixy = 0;
        for (var k = 0; k < e; k++)
        {
            var m = (k + 1) % e;
            double x0 = x[k], y0 = y[k], x1 = x[m], y1 = y[m];
            var c = x0 * y1 - x1 * y0;
            a += c;
            cx += (x0 + x1) * c;
            cy += (y0 + y1) * c;
            ixx += (y0 * y0 + y0 * y1 + y1 * y1) * c;
            iyy += (x0 * x0 + x0 * x1 + x1 * x1) * c;
            ixy += (x0 * y1 + 2 * x0 * y0 + 2 * x1 * y1 + x1 * y0) * c;
        }
        a *= 0.5;
        if (Math.Abs(a) < 1e-6) a = 1e-6;
        cx /= 6 * a;
        cy /= 6 * a;
        ixx = ixx / 12 - a * cy * cy;
        iyy = iyy / 12 - a * cx * cx;
        ixy = ixy / 24 - a * cx * cy;
        var f = new FormWerte { A = (float)a, Cx = (float)cx, Cy = (float)cy, J = (float)Math.Max((ixx + iyy) / a, 0.01) };
        var phi = 0.5f * MathF.Atan2((float)(2 * ixy), (float)(iyy - ixx));
        var (e1x, e1y) = (MathF.Cos(phi), MathF.Sin(phi));
        var (e2x, e2y) = (-e1y, e1x);
        float tmin = 9, tmax = -9, wmin = 9, wmax = -9;
        for (var k = 0; k < e; k++)
        {
            var (px, py) = (x[k] - f.Cx, y[k] - f.Cy);
            var t = px * e1x + py * e1y;
            var u = px * e2x + py * e2y;
            (tmin, tmax, wmin, wmax) = (MathF.Min(tmin, t), MathF.Max(tmax, t), MathF.Min(wmin, u), MathF.Max(wmax, u));
        }
        var (lg, wb) = (tmax - tmin, wmax - wmin);
        f.Laenge = lg;
        var flaechenR = MathF.Sqrt(f.A / MathF.PI);
        var zwei = false;
        if (lg >= 1.3f * wb)
        {
            var (tm, wm) = (0.5f * (tmin + tmax), 0.5f * (wmin + wmax));
            var d = MathF.Max(0.5f * lg - 0.45f * wb, 0.05f);
            var (k0x, k0y) = (e1x * (tm - d) + e2x * wm, e1y * (tm - d) + e2y * wm);
            var (k1x, k1y) = (e1x * (tm + d) + e2x * wm, e1y * (tm + d) + e2y * wm);
            var r0 = Inkreis(x, y, k0x + f.Cx, k0y + f.Cy);
            var r1 = Inkreis(x, y, k1x + f.Cx, k1y + f.Cy);
            if (r0 > 0.1f * flaechenR && r1 > 0.1f * flaechenR)
            {
                (f.Nk, f.K0x, f.K0y, f.K0r, f.K1x, f.K1y, f.K1r) = (2, k0x, k0y, 1.3f * r0, k1x, k1y, 1.3f * r1);
                zwei = true;
            }
        }
        if (!zwei)
        {
            var ri = Inkreis(x, y, f.Cx, f.Cy);
            (f.Nk, f.K0x, f.K0y, f.K0r) = (1, 0, 0, MathF.Max(MathF.Min(flaechenR, 1.3f * ri), 0.5f * flaechenR));
        }
        f.Huelle = MathF.Sqrt(f.K0x * f.K0x + f.K0y * f.K0y) + f.K0r;
        if (f.Nk == 2) f.Huelle = MathF.Max(f.Huelle, MathF.Sqrt(f.K1x * f.K1x + f.K1y * f.K1y) + f.K1r);
        return f;
    }

    /// <summary>Abstand eines Punktes zum Rand des Vielecks (innen positiv, außen negativ).</summary>
    private static float Inkreis(ReadOnlySpan<float> xs, ReadOnlySpan<float> ys, float px, float py)
    {
        var e = xs.Length;
        var best = float.MaxValue;
        var innen = false;
        for (int k = 0, m = e - 1; k < e; m = k++)
        {
            var (ex, ey) = (xs[k] - xs[m], ys[k] - ys[m]);
            var t = Math.Clamp(((px - xs[m]) * ex + (py - ys[m]) * ey) / MathF.Max(ex * ex + ey * ey, 1e-12f), 0f, 1f);
            var (dx, dy) = (px - (xs[m] + ex * t), py - (ys[m] + ey * t));
            best = MathF.Min(best, dx * dx + dy * dy);
            if ((ys[k] > py) != (ys[m] > py) && px < (xs[m] - xs[k]) * (py - ys[k]) / (ys[m] - ys[k]) + xs[k]) innen = !innen;
        }
        var d = MathF.Sqrt(best);
        return innen ? d : -d;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Plätze
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Setzt Platz <paramref name="i"/> als ganze Scholle der Stammscholle <paramref name="stamm"/> ein (Schwerpunkt aus dem Netz-Ursprung).</summary>
    private void Einsetzen(int i, int stamm, float sUrsprung, float qUrsprung, float winkel, bool frisch)
    {
        ref readonly var sch = ref _schollen[stamm];
        _stamm[i] = stamm;
        var f = _formen[sch.Saat];
        _form[i] = f;
        (_sekA[i], _sekB[i], _gen[i]) = (-1f, -1f, 0);
        (_r0[i], _rGleich[i]) = (sch.Halbmesser, sch.Halbmesser);
        (_rg[i], _rs[i], _mTau[i], _mAbrieb[i]) = (0f, 1f, 0f, 0f);
        _w[i] = winkel;
        var (sin, cos) = MathF.SinCos(winkel);
        _r[i] = sch.Halbmesser;
        _s[i] = sUrsprung + (f.Cx * cos - f.Cy * sin) * sch.Halbmesser;
        _q[i] = qUrsprung + (f.Cx * sin + f.Cy * cos) * sch.Halbmesser;
        _vs[i] = _tempo * Profil(_q[i], HalbAn(_s[i]));
        (_vq[i], _om[i]) = (0f, sch.Dreh);
        (_sAlt[i], _qAlt[i], _wAlt[i]) = (_s[i], _q[i], _w[i]);
        (_sperre[i], _duldung[i], _tauchen[i], _tauchZeit[i]) = (0f, 0f, 0, 0f);
        _dk[i] = sch.Dicke;
        _neu[i] = true;
        _frisch[i] = frisch;
        _ohneWarm[i] = true;
        Zustand[i] = Schollenmessung.Draussen;
    }

    private float SchmelzZiel(int i, float tauen)
    {
        // im Eisgang: Halbmesser × 1 → 0,5 von Tauwetter 0,3 bis 1, kleine schneller (unter etwa 0,3 m vergehen sie)
        var f = 1f - 0.5f * Mathf.SmoothStep(0.3f, 1f, tauen);
        var r0 = _rGleich[i]; // ein Bruchstück schmilzt wie eine gleich große Scholle
        return Mathf.Max(r0 * f - 0.4f * (1f - f), 0f) / Mathf.Max(r0, 0.01f);
    }

    /// <summary>Stammscholle kommt oben neu herein (Nachschub): frisch, mit ihren Ausgangswerten; im Eisgang schon so klein und morsch wie die übrigen.</summary>
    private void Nachschub(int i)
    {
        var stamm = _stamm[i];
        ref readonly var sch = ref _schollen[stamm];
        var s = Zufall() * 5f;
        Einsetzen(i, stamm, s, sch.Anteil * Mathf.Max(Platz(s, 1f) - sch.Halbmesser, 0f), Zufall() * Tau, frisch: true);
        _rs[i] = Mathf.Max(SchmelzZiel(i, _tauen), 0.05f);
        _mTau[i] = Mathf.SmoothStep(0.45f, 0.85f, _tauen);
        Ereignis[i] |= Schollenmessung.EreignisVersetzt;
        if (Zustand[i] == Schollenmessung.ImFenster) _suchen = true;
        if (Messung is not null) Messung.Nachschub++;
    }

    private void Freigeben(int i)
    {
        if (Zustand[i] == Schollenmessung.ImFenster) _suchen = true; // der Platz kann gleich neu belegt werden
        _stamm[i] = -1;
        Zustand[i] = Schollenmessung.Aus;
        _tauchen[i] = 0;
        _frei[_freiZahl++] = i;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Bild: feste Schritte zu <see cref="H"/> s (höchstens <see cref="MaxSchritte"/>), dann Bruch, Untertauchen, Schmelzen, Nachschub; zuletzt
    /// die gezeichnete Lage (zwischen den Schritten gemittelt). <paramref name="sBlick"/> = Stelle des Blickpunkts (Mitte des Stoßfensters),
    /// <paramref name="dichte"/> = Anteil aktiver Schollen (Schwelle), <paramref name="eisRand"/> = Eis für die Breite des Randeises,
    /// <paramref name="tauen"/> = Tauwetter, <paramref name="beweglich"/> = Prahm und Kähne in Laufachsen.
    /// </summary>
    public void Bild(double delta, float sBlick, float dichte, float eisRand, float tauen, List<Hindernis> beweglich)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        _zeit += delta;
        _bildNr++;
        (_dichte, _eisRand, _tauen, _beweglich) = (dichte, eisRand, tauen, beweglich);
        Array.Clear(Ereignis);
        _sammler += (float)Math.Min(delta, 0.25);
        var schritte = 0;
        while (_sammler >= H && schritte < MaxSchritte)
        {
            schritte++;
            _sammler -= H;
        }
        if (_sammler >= H) _sammler = H * 0.999f; // Rest bleibt stehen: unter etwa 7 Bildern/s läuft die Simulation langsamer als die Uhr
        Teilschritte = schritte;
        var dt = schritte * H;
        SBlick = sBlick;
        PlaetzeNachfuehren(dt, sBlick);
        SucheMs = LoesenMs = 0;
        if (schritte > 0)
        {
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_suchen || _seitSuche + dt > SuchAbstand + 1e-4f)
            {
                Suchen();
                FrischeTrennen();
                (_seitSuche, _suchen) = (0f, false);
            }
            _seitSuche += dt;
            SucheMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var x = 0; x < _fensterZahl; x++)
            {
                var i = _fenster[x];
                (_aufprallV[i], _stossV[i]) = (0f, 0f);
            }
            for (var k = 0; k < schritte; k++) Schritt(k == schritte - 1);
            LoesenMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            NachDenSchritten(dt);
        }
        Abschluss(schritte > 0);
        _erstesBild = false;
        SimulationMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    /// <summary>
    /// Je Platz einmal im Bild: Halbmesser, Dicke, Schmelzgrad, Masse und Zug nachführen – nur, wo sich etwas ändert: Wachsen (Dichte, Enden des
    /// Laufs) gedrosselt auf 0,12 m/s und nicht, solange die Scholle eine Nachbarin durchdringt; Schrumpfen 0,15 m/s; Schmelzen im Eisgang 4 mm/s;
    /// Schmelzgrad 1 %/s. Dann das Stoßfenster (±160 m um den Blickpunkt, eine Brücke näher als 220 m von 60 m stromauf bis 10 m stromab), wer neu
    /// hineinkommt, und die Listen der Schollen im Fenster und der tauchenden.
    /// </summary>
    private void PlaetzeNachfuehren(float dt, float sBlick)
    {
        var mZiel = Mathf.SmoothStep(0.45f, 0.85f, _tauen);
        var schmelzen = _tauen > 0.3f;
        var ende0 = 14f;
        var ende1 = _laenge - 14f;
        (_brueckeVon, _brueckeBis) = (1f, 0f);
        var beste = float.MaxValue;
        for (var b = 0; b < _brueckeS.Length; b++)
        {
            var a = MathF.Abs(_brueckeS[b] - sBlick);
            if (a < 220f && a < beste) (beste, _brueckeVon, _brueckeBis) = (a, _brueckeS[b] - 60f, _brueckeS[b] + 10f);
        }
        var aktiv = 0;
        (_fensterZahl, _taucherZahl) = (0, 0);
        _treibenRunde = (_treibenRunde + 1) & 3;
        var k4 = 1f - MathF.Exp(-4f * dt / 5f);
        for (var i = 0; i < _plaetze; i++)
        {
            var stamm = _stamm[i];
            if (stamm < 0) continue;
            var schwelle = _schollen[stamm].Schwelle;
            var wachsen = _dichte >= schwelle + 0.08f ? 1f : _dichte <= schwelle ? 0f : Mathf.SmoothStep(schwelle, schwelle + 0.08f, _dichte);
            var s = _s[i];
            var ziel = s > ende0 && s < ende1 ? wachsen : wachsen * Mathf.SmoothStep(0f, ende0, s) * Mathf.SmoothStep(_laenge, ende1, s);
            var neu = _neu[i] || _erstesBild;
            var r0 = _r0[i];
            if (_erstesBild) _rg[i] = ziel;
            else if (_rg[i] != ziel && dt > 0)
            {
                var d = (ziel - _rg[i]) * r0;
                d = d > 0 ? (_tiefe[i] > 0.02f ? 0f : MathF.Min(d, 0.12f * dt)) : MathF.Max(d, -0.15f * dt);
                var rg = Mathf.Clamp(_rg[i] + d / MathF.Max(r0, 0.01f), 0f, 1f);
                if (MathF.Abs(rg - ziel) < 1e-4f) rg = ziel;
                if (rg != _rg[i]) (_rg[i], neu) = (rg, true);
            }
            if ((schmelzen || _rs[i] < 1f) && dt > 0)
            {
                var rz = schmelzen ? SchmelzZiel(i, _tauen) : 1f;
                if (_rs[i] != rz)
                {
                    var dr = Mathf.Clamp((rz - _rs[i]) * r0, -0.004f * dt, 0.004f * dt);
                    _rs[i] = Mathf.Clamp(_rs[i] + dr / MathF.Max(r0, 0.01f), 0f, 1f);
                    neu = true;
                }
            }
            if ((mZiel > 0f || _mTau[i] > 0f) && dt > 0 && _mTau[i] != mZiel)
            {
                _mTau[i] += Mathf.Clamp(mZiel - _mTau[i], -0.01f * dt, 0.01f * dt);
                neu = true;
            }
            if (dt > 0)
            {
                if (_sperre[i] > 0) _sperre[i] -= dt;
                if (_duldung[i] > 0) _duldung[i] = MathF.Max(0f, _duldung[i] - 0.15f * dt);
            }
            if (neu)
            {
                _neu[i] = false;
                var m = MathF.Max(_mTau[i], _mAbrieb[i]);
                _dk[i] = _schollen[stamm].Dicke * (1f - 0.5f * m);
                var r = r0 * _rg[i] * _rs[i];
                _r[i] = r;
                ref readonly var f = ref _form[i];
                var masse = MathF.Max(917f * f.A * r * r * MathF.Max(_dk[i], 0.02f), 1e-3f);
                _invM[i] = 1f / masse;
                _invI[i] = 1f / (masse * f.J * r * r + 1e-4f);
                // Zug: Form (senkrechte Fläche 2 r × Tiefgang) und Reibung (Fläche), quadratisch; dazu ein kleiner linearer Anteil
                var rd = r * MathF.Sqrt(f.A / MathF.PI);
                _drag[i] = MathF.PI * 1000f * (ZugForm * 2f * rd * 0.9f * _dk[i] + ZugFlaeche * rd * rd) * _invM[i];
                _dragLin[i] = 1f / (ZugLinear + ZugLinearJeM * rd);
                _drehK[i] = 1f - MathF.Exp(-H / (DrehTau + DrehTauJeM * rd));
            }
            // Stoßfenster
            if (_rg[i] <= 0.001f && schwelle >= _dichte)
            {
                if (i >= _n) Freigeben(i);
                else if (Zustand[i] != Schollenmessung.Aus) Zustand[i] = Schollenmessung.Aus;
                continue;
            }
            aktiv++;
            var ab = MathF.Abs(s - sBlick);
            ab = MathF.Min(ab, _laenge - ab);
            var drin = ab < StossFenster || (s > _brueckeVon && s < _brueckeBis);
            if (drin && Zustand[i] != Schollenmessung.ImFenster)
            {
                // neu im Fenster (am Rand, wo niemand hinsieht): Kontakte mit der nächsten Suche, bis dahin trennt FrischeTrennen nichts
                _frisch[i] = true;
                _ohneWarm[i] = true;
                Ereignis[i] |= Schollenmessung.EreignisVersetzt;
                Kreise(i);
            }
            Zustand[i] = drin ? Schollenmessung.ImFenster : Schollenmessung.Draussen;
            if (_tauchen[i] != 0)
            {
                _taucher[_taucherZahl++] = i;
                Ereignis[i] |= Schollenmessung.EreignisTauchen;
            }
            else if (drin) _fenster[_fensterZahl++] = i;
            else if (dt > 0) Treiben(i, dt, k4);
        }
        Aktiv = aktiv;
    }

    private bool Stoesst(int i) => Zustand[i] == Schollenmessung.ImFenster && _tauchen[i] == 0 && _r[i] > 0.05f;

    /// <summary>
    /// Außerhalb des Fensters: nur treiben, einmal je Bild – zum Tempo der Strömung (5 s; nachgeführt jedes vierte Bild reihum), Drehung zur
    /// eigenen; an Pfeilern seitlich vorbei. Gezeichnet ohne Mitteln.
    /// </summary>
    private void Treiben(int i, float dt, float k4)
    {
        if ((i & 3) == _treibenRunde)
        {
            var us = _tempo * Profil(_q[i], HalbAn(_s[i]));
            _vs[i] += (us - _vs[i]) * k4;
            _vq[i] -= _vq[i] * k4;
            _om[i] += (_schollen[_stamm[i]].Dreh - _om[i]) * k4;
        }
        _s[i] += _vs[i] * dt;
        _q[i] += _vq[i] * dt;
        _w[i] += _om[i] * dt;
        if (_s[i] > _hindernisVon - 6f && _s[i] < _hindernisBis + 6f) DraussenPfeiler(i);
        (_sAlt[i], _qAlt[i], _wAlt[i]) = (_s[i], _q[i], _w[i]);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kontakte suchen
    // ═════════════════════════════════════════════════════════════════════════

    private int Zellindex(float s, float q)
    {
        var cs = Math.Clamp((int)(s / Zelle), 0, _zellenS - 1);
        var cq = Math.Clamp((int)((q + _halbMax) / Zelle), 0, _zellenQ - 1);
        return cs * _zellenQ + cq;
    }

    private void Kreise(int i)
    {
        var (sin, cos) = MathF.SinCos(_w[i]);
        var r = _r[i];
        ref readonly var f = ref _form[i];
        var k = 2 * i;
        _wx[k] = _s[i] + (f.K0x * cos - f.K0y * sin) * r;
        _wy[k] = _q[i] + (f.K0x * sin + f.K0y * cos) * r;
        _wr[k] = f.K0r * r;
        if (f.Nk == 2)
        {
            _wx[k + 1] = _s[i] + (f.K1x * cos - f.K1y * sin) * r;
            _wy[k + 1] = _q[i] + (f.K1x * sin + f.K1y * cos) * r;
            _wr[k + 1] = f.K1r * r;
        }
        else _wr[k + 1] = 0;
    }

    /// <summary>Hülle, ab der eine Scholle „groß“ ist: Paare mit ihr sucht sie selbst (weiter), kleine untereinander nur nah.</summary>
    private const float GrossAb = 2f;

    /// <summary>
    /// Kontakte suchen (spätestens alle <see cref="SuchAbstand"/> s): Die Schollen im Fenster werden nach Zellen sortiert in dichte Felder gelegt
    /// (Zählsortierung, gut für den Speicher), dann je Zelle von stromab nach stromauf: je Scholle zuerst Hindernisse und Eiskante, dann die Nachbarn
    /// (kleine untereinander nur im nahen Umkreis, große suchen weiter). Vorlauf je Paar nach dem Tempo, mit dem sie sich nähern. Die Stöße der
    /// letzten Liste gehen über einen Schlüssel an dieselben Kontakte (Warmstart).
    /// </summary>
    private void Suchen()
    {
        _suchNr++;
        (_kAlt, _kNeu) = (_kNeu, _kAlt);
        _huelleMax = 0;
        // 1. Zellen der Schollen, die stoßen; Bereich der belegten Zeilen
        var csMin = int.MaxValue;
        var csMax = -1;
        var zahl = 0;
        for (var x = 0; x < _fensterZahl; x++)
        {
            var i = _fenster[x];
            Kreise(i);
            if (_r[i] <= 0.05f)
            {
                // winzig (wächst gerade auf): treibt mit, stößt nicht
                (_huelle[i], _kNeu.Ab[i], _kNeu.Zahl[i]) = (0f, 0, 0);
                continue;
            }
            var h = _form[i].Huelle * _r[i];
            _huelle[i] = h;
            _huelleMax = MathF.Max(_huelleMax, h);
            var z = Zellindex(_s[i], _q[i]);
            _zelleVon[zahl] = z;
            _sortiert[zahl++] = i;
            var cs = z / _zellenQ;
            (csMin, csMax) = (Math.Min(csMin, cs), Math.Max(csMax, cs));
        }
        _kZahl = 0;
        if (zahl == 0) return;
        // 2. Zählsortierung nach Zelle: Anfang jeder Zelle in _kopf (Zellen der belegten Zeilen), Daten dicht in _p*
        var c0 = csMin * _zellenQ;
        var c1 = (csMax + 1) * _zellenQ;
        Array.Clear(_kopf, c0, c1 - c0 + 1);
        for (var k = 0; k < zahl; k++) _kopf[_zelleVon[k] + 1]++;
        for (var c = c0 + 1; c <= c1; c++) _kopf[c] += _kopf[c - 1];
        for (var k = 0; k < zahl; k++)
        {
            var i = _sortiert[k];
            var ziel = _naechster[_zelleVon[k]]++ + _kopf[_zelleVon[k]];
            (_pI[ziel], _pS[ziel], _pQ[ziel], _pH[ziel], _pVs[ziel], _pVq[ziel]) = (i, _s[i], _q[i], _huelle[i], _vs[i], _vq[i]);
        }
        for (var k = 0; k < zahl; k++) _naechster[_zelleVon[k]] = 0; // Zähler der Zellen wieder auf 0
        // 3. Kontakte; Vorlauf: fest (Rand, Drehung) plus der Weg, den sich zwei bis zur nächsten Suche nähern können (Tempounterschied × Zeit)
        const float vorlaufFest = Rand + 0.03f;
        var vorlaufMax = vorlaufFest + 2.5f * SuchVorlauf;
        var ueberlauf = 0L;
        var platzRand = _randeis * _eisRand * 1.5f + UferAbstand;
        var reichKlein = GrossAb + vorlaufMax;
        var reichGross = _huelleMax + vorlaufMax;
        for (var cs = csMax; cs >= csMin; cs--)
            for (var p = _kopf[cs * _zellenQ]; p < _kopf[(cs + 1) * _zellenQ]; p++)
            {
                var i = _pI[p];
                _kNeu.Ab[i] = _kZahl;
                var (si, qi, hi, vsi, vqi) = (_pS[p], _pQ[p], _pH[p], _pVs[p], _pVq[p]);
                var grossI = hi > GrossAb;
                var nki = _form[i].Nk;
                var vorlauf = vorlaufFest + MathF.Sqrt(vsi * vsi + vqi * vqi) * SuchVorlauf;
                // feste Hindernisse (nur in ihrer Nähe)
                var festeZahl = si > _hindernisVon - hi - vorlauf && si < _hindernisBis + hi + vorlauf ? _feste.Count : 0;
                for (var h = 0; h < festeZahl; h++)
                {
                    var o = _feste[h];
                    var weit = o.Form == Hindernisform.Eisbrecher ? 4.6f : MathF.Max(o.HalbLaengs, o.HalbQuer);
                    if (MathF.Abs(si - o.Stelle) > weit + hi + vorlauf || MathF.Abs(qi - o.Quer) > weit + hi + vorlauf) continue;
                    for (var a = 0; a < nki; a++)
                    {
                        var sep = o.Form == Hindernisform.Eisbrecher ? PfeilerAbstand(2 * i + a, o) : KapselAbstand(2 * i + a, o);
                        if (sep < vorlauf && !Neu(i, -1, o.Form == Hindernisform.Eisbrecher ? ArtPfeiler : ArtKapsel, (byte)a, 0, (short)h)) ueberlauf++;
                    }
                }
                for (var h = 0; h < _beweglich.Count; h++)
                {
                    var o = _beweglich[h];
                    var weit = MathF.Max(o.HalbLaengs, o.HalbQuer);
                    var vorB = vorlaufFest + (MathF.Sqrt((vsi - o.VStelle) * (vsi - o.VStelle) + (vqi - o.VQuer) * (vqi - o.VQuer)) + 0.5f) * SuchVorlauf;
                    if (MathF.Abs(si - o.Stelle) > weit + hi + vorB || MathF.Abs(qi - o.Quer) > weit + hi + vorB) continue;
                    for (var a = 0; a < nki; a++)
                        if (KapselAbstand(2 * i + a, o) < vorB && !Neu(i, -1, ArtBewegt, (byte)a, 0, (short)h)) ueberlauf++;
                }
                // Eiskante (nur nah am Rand)
                var platz = HalbAn(si) - platzRand;
                if (MathF.Abs(qi) + hi > platz - vorlauf)
                    for (var a = 0; a < nki; a++)
                    {
                        var k = 2 * i + a;
                        if (_wy[k] + 0.85f * _wr[k] > platz - vorlauf && !Neu(i, -1, ArtKante, (byte)a, 0, 1)) ueberlauf++;
                        if (-_wy[k] + 0.85f * _wr[k] > platz - vorlauf && !Neu(i, -1, ArtKante, (byte)a, 0, -1)) ueberlauf++;
                    }
                // Nachbarn: Hülle, dann Kreispaare. Klein–klein findet die kleinere Nummer im nahen Umkreis; Paare mit einer großen findet die große
                // (groß–groß die kleinere Nummer)
                var reich = hi + (grossI ? reichGross : reichKlein);
                var (s0, s1) = (Math.Max((int)((si - reich) / Zelle), csMin), Math.Min((int)((si + reich) / Zelle), csMax));
                var (q0, q1) = (Math.Max((int)((qi + _halbMax - reich) / Zelle), 0), Math.Min((int)((qi + _halbMax + reich) / Zelle), _zellenQ - 1));
                for (var zs = s0; zs <= s1; zs++)
                {
                    var (von, bis) = (_kopf[zs * _zellenQ + q0], _kopf[zs * _zellenQ + q1 + 1]);
                    for (var pj = von; pj < bis; pj++)
                    {
                        var j = _pI[pj];
                        if (j == i) continue;
                        var hj = _pH[pj];
                        var grossJ = hj > GrossAb;
                        if (grossI ? grossJ && j < i : grossJ || j < i) continue;
                        var (ds, dq) = (_pS[pj] - si, _pQ[pj] - qi);
                        var (dvs, dvq) = (_pVs[pj] - vsi, _pVq[pj] - vqi);
                        var vorP = vorlaufFest + MathF.Sqrt(dvs * dvs + dvq * dvq) * SuchVorlauf;
                        var hm = hi + hj + vorP;
                        if (ds * ds + dq * dq >= hm * hm) continue;
                        var nkj = _form[j].Nk;
                        for (var a = 0; a < nki; a++)
                            for (var b = 0; b < nkj; b++)
                            {
                                var (ia, jb) = (2 * i + a, 2 * j + b);
                                var (ex, ey) = (_wx[jb] - _wx[ia], _wy[jb] - _wy[ia]);
                                var m = _wr[ia] + _wr[jb] + vorP;
                                if (ex * ex + ey * ey < m * m && !Neu(i, j, ArtPaar, (byte)a, (byte)b, 0)) ueberlauf++;
                            }
                    }
                }
                _kNeu.Zahl[i] = _kZahl - _kNeu.Ab[i];
            }
        if (Messung is not null) Messung.KontaktUeberlauf += ueberlauf;
        // nach dieser Suche gelten die Kontakte frischer Schollen wieder für den Warmstart
        for (var x = 0; x < _fensterZahl; x++) _ohneWarm[_fenster[x]] = false;
    }

    /// <summary>Legt einen Kontakt an und übernimmt die Stöße desselben Kontakts aus dem letzten Bild. Liefert false, wenn der Vorrat voll ist.</summary>
    private bool Neu(int a, int b, byte art, byte ka, byte kb, short index)
    {
        if (_kZahl >= _kMax) return false;
        _inListe[a] = _suchNr;
        if (b >= 0) _inListe[b] = _suchNr;
        var c = _kZahl++;
        var k = _kNeu;
        (k.A[c], k.B[c], k.Art[c], k.KA[c], k.KB[c], k.Index[c]) = (a, b, art, ka, kb, index);
        (k.Ln[c], k.Lt[c], k.Lr[c], k.Beruehrt[c]) = (0f, 0f, 0f, 0);
        if (!Warmstart || art == ArtBewegt || _ohneWarm[a] || (b >= 0 && _ohneWarm[b])) return true;
        var alt = _kAlt;
        var ende = alt.Ab[a] + alt.Zahl[a];
        for (var o = alt.Ab[a]; o < ende && o < _kMax; o++)
            if (alt.A[o] == a && alt.B[o] == b && alt.Art[o] == art && alt.KA[o] == ka && alt.KB[o] == kb && alt.Index[o] == index)
            {
                (k.Ln[c], k.Lt[c], k.Lr[c], k.Beruehrt[c]) = (alt.Ln[o], alt.Lt[o], alt.Lr[o], alt.Beruehrt[o]);
                break;
            }
        return true;
    }

    /// <summary>Abstand eines Stoßkreises zu einem Steinpfeiler (Vieleck), negativ bei Durchdringung.</summary>
    private float PfeilerAbstand(int k, Hindernis o)
    {
        PfeilerNaechster(_wx[k] - o.Stelle, _wy[k] - o.Quer, out var dist, out _, out _, out _);
        return dist - _wr[k];
    }

    /// <summary>
    /// Nächster Punkt des Pfeiler-Vielecks zum Punkt (ps, pq) (relativ zur Pfeilermitte): Abstand (innen negativ), Normale nach außen und ob er
    /// am glatten Kopf liegt (Kanten des Eisbrechers und seine Spitze).
    /// </summary>
    private static void PfeilerNaechster(float ps, float pq, out float dist, out float ns, out float nq, out bool kopf)
    {
        var (beste, bk) = (float.MinValue, 0);
        for (var k = 0; k < 6; k++)
        {
            var t = PfNs[k] * (ps - PfS[k]) + PfNq[k] * (pq - PfQ[k]);
            if (t > beste) (beste, bk) = (t, k);
        }
        var m = (bk + 1) % 6;
        var (v1s, v1q, v2s, v2q) = (PfS[bk], PfQ[bk], PfS[m], PfQ[m]);
        var u1 = (ps - v1s) * (v2s - v1s) + (pq - v1q) * (v2q - v1q);
        var u2 = (ps - v2s) * (v1s - v2s) + (pq - v2q) * (v1q - v2q);
        kopf = bk == 0 || bk == 5;
        if (beste > 0f && u1 < 0f)
        {
            var (ds, dq) = (ps - v1s, pq - v1q);
            dist = MathF.Sqrt(ds * ds + dq * dq);
            (ns, nq) = dist > 1e-6f ? (ds / dist, dq / dist) : (PfNs[bk], PfNq[bk]);
            kopf = bk == 0; // an einer Ecke: nur die Spitze zählt zum Kopf, die Schultern zur Flanke
        }
        else if (beste > 0f && u2 < 0f)
        {
            var (ds, dq) = (ps - v2s, pq - v2q);
            dist = MathF.Sqrt(ds * ds + dq * dq);
            (ns, nq) = dist > 1e-6f ? (ds / dist, dq / dist) : (PfNs[bk], PfNq[bk]);
            kopf = m == 0;
        }
        else
        {
            dist = beste;
            (ns, nq) = (PfNs[bk], PfNq[bk]);
        }
    }

    /// <summary>Abstand eines Stoßkreises zu einer Kapsel (entlang der längeren Seite), negativ bei Durchdringung.</summary>
    private float KapselAbstand(int k, Hindernis o)
    {
        KapselNaechster(_wx[k], _wy[k], o, out var dist, out _, out _, out var rh);
        return dist - rh - _wr[k];
    }

    private static void KapselNaechster(float ps, float pq, Hindernis o, out float dist, out float ns, out float nq, out float rh)
    {
        var (ds, dq) = (ps - o.Stelle, pq - o.Quer);
        float cs = 0, cq = 0;
        if (o.HalbLaengs >= o.HalbQuer)
        {
            rh = o.HalbQuer;
            cs = Mathf.Clamp(ds, -(o.HalbLaengs - rh), o.HalbLaengs - rh);
        }
        else
        {
            rh = o.HalbLaengs;
            cq = Mathf.Clamp(dq, -(o.HalbQuer - rh), o.HalbQuer - rh);
        }
        var (as_, aq) = (ds - cs, dq - cq);
        dist = MathF.Sqrt(as_ * as_ + aq * aq);
        (ns, nq) = dist > 1e-5f ? (as_ / dist, aq / dist) : (-1f, 0f);
    }

    /// <summary>
    /// Neu ins Fenster gekommene oder neu eingesetzte Schollen: ihre Überlappung nur über die Lage auflösen (drei Durchgänge, ohne Tempo) – am Rand
    /// des Fensters und oben am Lauf sieht niemand hin.
    /// </summary>
    private void FrischeTrennen()
    {
        var gibt = false;
        for (var x = 0; x < _fensterZahl && !gibt; x++) gibt = _frisch[_fenster[x]];
        if (!gibt) return;
        for (var durchgang = 0; durchgang < 3; durchgang++)
            for (var c = 0; c < _kZahl; c++)
            {
                var a = _kNeu.A[c];
                var b = _kNeu.B[c];
                if (_kNeu.Art[c] != ArtPaar || (!_frisch[a] && !_frisch[b])) continue;
                var (ia, jb) = (2 * a + _kNeu.KA[c], 2 * b + _kNeu.KB[c]);
                var (ex, ey) = (_wx[jb] - _wx[ia], _wy[jb] - _wy[ia]);
                var d = MathF.Sqrt(ex * ex + ey * ey);
                var tief = _wr[ia] + _wr[jb] - d - MathF.Max(_duldung[a], _duldung[b]);
                if (tief <= 0 || d < 1e-5f) continue;
                var (ma, mb) = (_frisch[a] ? _invM[a] : 0f, _frisch[b] ? _invM[b] : 0f);
                if (ma + mb <= 0) continue;
                var (ka, kb) = (ma / (ma + mb), mb / (ma + mb));
                var (ns, nq) = (ex / d, ey / d);
                (_s[a], _q[a]) = (_s[a] - ns * tief * ka, _q[a] - nq * tief * ka);
                (_s[b], _q[b]) = (_s[b] + ns * tief * kb, _q[b] + nq * tief * kb);
                Kreise(a);
                Kreise(b);
            }
        for (var x = 0; x < _fensterZahl; x++)
        {
            var i = _fenster[x];
            if (!_frisch[i]) continue;
            _frisch[i] = false;
            (_sAlt[i], _qAlt[i], _wAlt[i]) = (_s[i], _q[i], _w[i]);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ein Schritt
    // ═════════════════════════════════════════════════════════════════════════

    private void Schritt(bool letzter)
    {
        var t = (float)_zeit;
        if (_pendelnBild != _bildNr)
        {
            // leichtes Pendeln quer (Wirbel), je Scholle anders; ändert sich langsam: einmal je Bild
            _pendelnBild = _bildNr;
            for (var x = 0; x < _fensterZahl; x++)
            {
                var i = _fenster[x];
                _pendeln[i] = 0.06f * (Rauschen(t * 0.05f + _schollen[_stamm[i]].Saat * 0.37f) - 0.5f) * 2f;
            }
        }
        // 1. Wasser zieht, Lage vor dem Schritt merken
        for (var x = 0; x < _fensterZahl; x++)
        {
            var i = _fenster[x];
            (_sAlt[i], _qAlt[i], _wAlt[i]) = (_s[i], _q[i], _w[i]);
            Zug(i, t);
        }
        // 2. Kontakte vorbereiten und warm starten; 3. lösen mit Ausschieben
        Vorbereiten();
        if (Warmstart) Warmstarten();
        Loesen(true);
        // 4. Lage fortschreiben; davor Drehgrenze (frei 0,08, mit Hindernis 0,25 rad/s, über 1 m geteilt durch den Halbmesser) und Fehler abfangen,
        // danach die Zähler für die Nachsorge leeren
        var oben = 0;
        for (var x = 0; x < _fensterZahl; x++)
        {
            var i = _fenster[x];
            var grenze = (_anHindernis[i] ? DrehGrenzeHindernis : DrehGrenzeFrei) * MathF.Min(1f, 1f / MathF.Max(_r[i], 0.01f));
            if (MathF.Abs(_om[i]) > grenze)
            {
                _om[i] = MathF.Sign(_om[i]) * grenze;
                oben++;
            }
            if (!float.IsFinite(_vs[i]) || !float.IsFinite(_vq[i]) || !float.IsFinite(_om[i]))
                (_vs[i], _vq[i], _om[i]) = (_tempo, 0f, 0f);
            var (ds, dq, dw) = (_vs[i] * H, _vq[i] * H, _om[i] * H);
            _s[i] += ds;
            _q[i] += dq;
            _w[i] += dw;
            (_schiebS[i], _schiebQ[i], _schiebW[i]) = (ds, dq, dw);
            if (_inListe[i] == _suchNr) Kreise(i); // Kreise nur für Schollen mit Kontakten (die übrigen bei der nächsten Suche)
            (_kontaktZahl[i], _belastet[i], _anHindernis[i], _tiefe[i]) = (0, 0, false, 0f);
            if (letzter) (_druck[i], _druckMax[i]) = (0f, 0f);
        }
        if (letzter) _oben = oben;
        for (var x = 0; x < _taucherZahl; x++)
        {
            var i = _taucher[x];
            (_sAlt[i], _qAlt[i], _wAlt[i]) = (_s[i], _q[i], _w[i]);
            TauchSchritt(i);
        }
        // 5. lösen ohne Ausschieben (Relax)
        Loesen(false);
        // Nachsorge: Drehgrenze, Belastung, Druck an Hindernissen
        Nachsorge(letzter);
    }

    /// <summary>
    /// Die Strömung zieht die Scholle: Form- und Reibungswiderstand quadratisch im Tempounterschied (Damsgaard u. a. 2018), dazu ein kleiner
    /// linearer Anteil; implizit gerechnet. Die Drehung strebt zur Wunschdrehung (eigene plus halbe Wirbelstärke des Profils), im dichten Eis
    /// klingt sie schnell ab.
    /// </summary>
    private void Zug(int i, float t)
    {
        ref readonly var sch = ref _schollen[_stamm[i]];
        var (s, q) = (_s[i], _q[i]);
        var halb = MathF.Max(HalbAn(s), 1f);
        var us = _tempo * Profil(q, halb);
        var uq = _pendeln[i];
        var omZiel = sch.Dreh + 0.5f * _tempo * q / (halb * halb);
        if (BrueckenFeld && s > _brueckeVon - 20f && s < _brueckeBis + 20f) BrueckenStroemung(i, t, s, q, ref us, ref uq, ref omZiel);
        var dicht = _kontaktZahl[i] >= 3;
        if (dicht) us *= 0.92f; // im dichten Eis langsamer, dazu ist der Formwiderstand abgeschirmt (unten)
        var (dvs, dvq) = (us - _vs[i], uq - _vq[i]);
        var dv = MathF.Sqrt(dvs * dvs + dvq * dvq);
        var k = (_drag[i] * dv * (dicht ? 0.5f : 1f) + _dragLin[i]) * H;
        k /= 1f + k;
        _vs[i] += dvs * k;
        _vq[i] += dvq * k;
        _om[i] += (omZiel - _om[i]) * _drehK[i];
        if (_belastet[i] >= 2) _om[i] *= _drehSperreK;
        // Grenze des Tempounterschieds (Fehler liefen sonst über die Kontakte durch den Stau)
        if (dv > 2.5f)
        {
            var f = 2.5f / dv;
            (_vs[i], _vq[i]) = (us - dvs * f, uq - dvq * f);
        }
    }

    /// <summary>Strömung an einer Brücke: in den Bögen schneller, vor dem Kopf teilt sie sich (mit Wirbeln), im Lee eines Pfeilers Kehrwasser.</summary>
    private void BrueckenStroemung(int i, float t, float s, float q, ref float us, ref float uq, ref float omZiel)
    {
        for (var b = 0; b < _brueckeS.Length; b++)
        {
            var dsB = s - _brueckeS[b];
            if (dsB < -16f || dsB > 26f) continue;
            var pf = _brueckePfeiler[b];
            var (naechster, abst) = (-1, float.MaxValue);
            for (var k = 0; k < pf.Length; k++)
            {
                var a = MathF.Abs(q - _feste[pf[k]].Quer);
                if (a < abst) (abst, naechster) = (a, pf[k]);
            }
            if (naechster < 0) continue;
            var o = _feste[naechster];
            var ds = s - o.Stelle;
            var seite = q >= o.Quer ? 1f : -1f;
            var breit = o.Form == Hindernisform.Eisbrecher ? 1f : o.HalbQuer;
            // Durchfluss in den Bögen: die Pfeiler versperren gut ein Fünftel der Breite
            if (abst > breit) us *= 1f + FeldBogen * (1f - Mathf.SmoothStep(4f, 8f, MathF.Abs(ds)));
            // vor dem Kopf: die Strömung teilt sich, mit unruhigen Wirbeln (keine Scholle balanciert auf der Spitze)
            if (ds > -14f && ds < -2f && abst < 4f)
            {
                var nah = (1f - abst / 4f) * Mathf.SmoothStep(-14f, -5f, ds);
                uq += seite * FeldTeilen * nah;
                us *= 1f - 0.25f * nah;
                uq += FeldWirbel * (Rauschen(t * 0.4f + _schollen[_stamm[i]].Saat * 0.71f) - 0.5f) * 2f * nah;
            }
            // Lee: Kehrwasser über gut sieben Pfeilerbreiten, langsam drehend
            if (ds > 3f && ds < 18f && abst < 2.6f)
            {
                var lee = (1f - Mathf.SmoothStep(1.2f, 2.6f, abst)) * (1f - Mathf.SmoothStep(10f, 18f, ds));
                us *= 1f - 0.6f * lee;
                omZiel += seite * 0.035f * lee;
            }
        }
    }

    /// <summary>Außerhalb des Fensters und unter Wasser: an Pfeilern seitlich vorbei (harte Grenze ohne Reibung).</summary>
    private void DraussenPfeiler(int i)
    {
        for (var h = 0; h < _feste.Count; h++)
        {
            var o = _feste[h];
            var ds = _s[i] - o.Stelle;
            if (ds < -6f || ds > 6f) continue;
            var breit = (o.Form == Hindernisform.Eisbrecher ? 1f : o.HalbQuer) + 0.7f * _r[i];
            var dq = _q[i] - o.Quer;
            if (MathF.Abs(dq) < breit) _q[i] = o.Quer + (dq >= 0 ? breit : -breit);
        }
    }

    /// <summary>Lage, Hebel, Massen, Reibwerte und Abstand jedes Kontakts zu Beginn des Schritts.</summary>
    private void Vorbereiten()
    {
        var k = _kNeu;
        var platzRand = _randeis * _eisRand * 1.5f + UferAbstand;
        for (var c = 0; c < _kZahl; c++)
        {
            var a = k.A[c];
            var ia = 2 * a + k.KA[c];
            _cVn0[c] = 0;
            if (!Stoesst(a) || (k.Art[c] == ArtPaar && !Stoesst(k.B[c])) || (k.Art[c] == ArtBewegt && k.Index[c] >= _beweglich.Count))
            {
                // gilt nicht mehr (zwischen zwei Suchen getaucht, gegangen, Hindernis fort): ohne Wirkung
                (_cMn[c], _cMt[c], _cMr[c], _cIma[c], _cIia[c], _cImb[c], _cIib[c], _cS0[c]) = (0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f);
                (k.Ln[c], k.Lt[c], k.Lr[c], _cRoll[c], _cTol[c]) = (0f, 0f, 0f, 0f, 0f);
                continue;
            }
            var (cs, cq, ra) = (_wx[ia], _wy[ia], _wr[ia]);
            float ns, nq, s0, ps, pq;
            float vbs = 0, vbq = 0, imb = 0, iib = 0, rbs = 0, rbq = 0;
            float muH, muG, roll, schub;
            var b = k.B[c];
            var tol = _duldung[a];
            switch (k.Art[c])
            {
                case ArtPaar:
                {
                    var jb = 2 * b + k.KB[c];
                    var rb = _wr[jb];
                    var (ex, ey) = (_wx[jb] - cs, _wy[jb] - cq);
                    var d = MathF.Sqrt(ex * ex + ey * ey);
                    (ns, nq) = d > 1e-6f ? (ex / d, ey / d) : (1f, 0f);
                    s0 = d - ra - rb;
                    (ps, pq) = (cs + ns * (ra + 0.5f * s0), cq + nq * (ra + 0.5f * s0));
                    (imb, iib) = (_invM[b], _invI[b]);
                    (rbs, rbq) = (ps - _s[b], pq - _q[b]);
                    (muH, muG, roll, schub) = (MuEisHaft, MuEisGleit, RollEis, SchubEis);
                    tol = MathF.Max(tol, _duldung[b]);
                    _cReff[c] = ra * rb / MathF.Max(ra + rb, 1e-4f);
                    break;
                }
                case ArtPfeiler:
                {
                    var o = _feste[k.Index[c]];
                    PfeilerNaechster(cs - o.Stelle, cq - o.Quer, out var dist, out var pns, out var pnq, out var kopf);
                    (ns, nq) = (-pns, -pnq);
                    s0 = dist - ra;
                    (ps, pq) = (cs + ns * (ra + 0.5f * s0), cq + nq * (ra + 0.5f * s0));
                    (muH, muG, roll, schub) = kopf ? (MuKopfHaft, MuKopfGleit, RollSonst, SchubHindernis) : (MuSteinHaft, MuSteinGleit, RollSonst, SchubHindernis);
                    _cReff[c] = ra;
                    break;
                }
                case ArtKapsel:
                case ArtBewegt:
                {
                    var o = k.Art[c] == ArtKapsel ? _feste[k.Index[c]] : _beweglich[k.Index[c]];
                    KapselNaechster(cs, cq, o, out var dist, out var kns, out var knq, out var rh);
                    (ns, nq) = (-kns, -knq);
                    s0 = dist - rh - ra;
                    (ps, pq) = (cs + ns * (ra + 0.5f * s0), cq + nq * (ra + 0.5f * s0));
                    (muH, muG, roll, schub) = (MuHolzHaft, MuHolzGleit, RollSonst, SchubHindernis);
                    if (k.Art[c] == ArtBewegt) (vbs, vbq) = (o.VStelle, o.VQuer);
                    _cReff[c] = ra;
                    break;
                }
                default:
                {
                    // Eiskante: Normale aus der Neigung der Uferlinie (an Engstellen bremst sie und lenkt ab)
                    float seite = k.Index[c];
                    var neig = HalbNeigung(cs);
                    var l = MathF.Sqrt(1f + neig * neig);
                    (ns, nq) = (-neig / l, seite / l);
                    var platz = HalbAn(cs) - platzRand;
                    s0 = (platz - seite * cq - 0.85f * ra) / l;
                    (ps, pq) = (cs + ns * (0.85f * ra + 0.5f * s0), cq + nq * (0.85f * ra + 0.5f * s0));
                    (muH, muG, roll, schub) = (MuKanteHaft, MuKanteGleit, RollSonst, SchubKante);
                    _cReff[c] = ra;
                    tol = 0;
                    break;
                }
            }
            var (ras, raq) = (ps - _s[a], pq - _q[a]);
            var (ima, iia) = (_invM[a], _invI[a]);
            if (k.Art[c] == ArtPaar && MassenVerhaeltnis > 0)
            {
                // wirksames Massenverhältnis begrenzen: die schwerere wird für diesen Kontakt leichter
                if (ima * MassenVerhaeltnis < imb) (ima, iia) = (imb / MassenVerhaeltnis, iia * (imb / MassenVerhaeltnis) / MathF.Max(ima, 1e-12f));
                else if (imb * MassenVerhaeltnis < ima) (imb, iib) = (ima / MassenVerhaeltnis, iib * (ima / MassenVerhaeltnis) / MathF.Max(imb, 1e-12f));
            }
            var rna = ras * nq - raq * ns;
            var rnb = rbs * nq - rbq * ns;
            var kn = ima + imb + iia * rna * rna + iib * rnb * rnb;
            var rta = ras * ns + raq * nq; // r × t mit t = (−nq, ns)
            var rtb = rbs * ns + rbq * nq;
            var kt = ima + imb + iia * rta * rta + iib * rtb * rtb;
            (_cNs[c], _cNq[c], _cS0[c]) = (ns, nq, s0);
            (_cRAs[c], _cRAq[c], _cRBs[c], _cRBq[c]) = (ras, raq, rbs, rbq);
            (_cMn[c], _cMt[c], _cMr[c]) = (kn > 0 ? 1f / kn : 0f, kt > 0 ? 1f / kt : 0f, iia + iib > 0 ? 1f / (iia + iib) : 0f);
            (_cVBs[c], _cVBq[c], _cIma[c], _cIia[c], _cImb[c], _cIib[c]) = (vbs, vbq, ima, iia, imb, iib);
            (_cMuH[c], _cMuG[c], _cRoll[c], _cSchub[c], _cTol[c]) = (muH, muG, roll, schub, tol);
        }
    }

    private void Warmstarten()
    {
        var k = _kNeu;
        for (var c = 0; c < _kZahl; c++)
        {
            var (ln, lt, lr) = (k.Ln[c], k.Lt[c], k.Lr[c]);
            if (ln == 0 && lt == 0 && lr == 0) continue;
            var (ns, nq) = (_cNs[c], _cNq[c]);
            var (ps, pq) = (ln * ns - lt * nq, ln * nq + lt * ns);
            var a = k.A[c];
            var (ima, iia) = (_cIma[c], _cIia[c]);
            _vs[a] -= ima * ps;
            _vq[a] -= ima * pq;
            _om[a] -= iia * (_cRAs[c] * pq - _cRAq[c] * ps + lr);
            if (k.Art[c] == ArtPaar)
            {
                var b = k.B[c];
                var (imb, iib) = (_cImb[c], _cIib[c]);
                _vs[b] += imb * ps;
                _vq[b] += imb * pq;
                _om[b] += iib * (_cRBs[c] * pq - _cRBq[c] * ps + lr);
            }
        }
    }

    /// <summary>
    /// Ein Durchgang über alle Kontakte (Gauss-Seidel): Normalstoß weich mit begrenztem Ausschieben (<paramref name="mitAusschieben"/>) bzw. ohne
    /// (Relax), dann Reibung (Haften unter 2 cm/s Schlupf) und Rollwiderstand. Vorausschauende Kontakte (Abstand > 0) lassen die Lücke nur schließen.
    /// </summary>
    private void Loesen(bool mitAusschieben)
    {
        var k = _kNeu;
        var omega = 2f * MathF.PI * KontaktHz;
        var a1 = 2f * Daempfung + H * omega;
        var a2 = H * omega * a1;
        var a3 = 1f / (1f + a2);
        var (biasRate, massScale, impulseScale) = (omega / a1, a2 * a3, a3);
        const float invH = 1f / H;
        for (var c = 0; c < _kZahl; c++)
        {
            var a = k.A[c];
            var b = k.B[c];
            var paar = k.Art[c] == ArtPaar;
            var (ns, nq) = (_cNs[c], _cNq[c]);
            var (ras, raq, rbs, rbq) = (_cRAs[c], _cRAq[c], _cRBs[c], _cRBq[c]);
            var (ima, iia, imb, iib) = (_cIma[c], _cIia[c], _cImb[c], _cIib[c]);
            var (vas, vaq, wa) = (_vs[a], _vq[a], _om[a]);
            float vbs, vbq, wb;
            if (paar) (vbs, vbq, wb) = (_vs[b], _vq[b], _om[b]);
            else (vbs, vbq, wb) = (_cVBs[c], _cVBq[c], 0f);
            // Abstand: im Relax mit dem Weg dieses Schritts nachgeführt
            var s = _cS0[c];
            if (!mitAusschieben)
            {
                var (das, daq) = (_schiebS[a] - _schiebW[a] * raq, _schiebQ[a] + _schiebW[a] * ras);
                var (dbs, dbq) = paar ? (_schiebS[b] - _schiebW[b] * rbq, _schiebQ[b] + _schiebW[b] * rbs) : (vbs * H, vbq * H);
                s += (dbs - das) * ns + (dbq - daq) * nq;
            }
            s += _cTol[c];
            // Normalstoß
            var dvs = vbs - wb * rbq - (vas - wa * raq);
            var dvq = vbq + wb * rbs - (vaq + wa * ras);
            var vn = dvs * ns + dvq * nq;
            float bias, ms, isc;
            if (s > 0f) (bias, ms, isc) = (s * invH, 1f, 0f);
            else if (mitAusschieben) (bias, ms, isc) = (MathF.Max(biasRate * s, -_cSchub[c]), massScale, impulseScale);
            else (bias, ms, isc) = (0f, 1f, 0f);
            var lnAlt = k.Ln[c];
            var lam = -_cMn[c] * ms * (vn + bias) - isc * lnAlt;
            var neu = MathF.Max(lnAlt + lam, 0f);
            lam = neu - lnAlt;
            k.Ln[c] = neu;
            if (mitAusschieben && neu > 0f && k.Beruehrt[c] == 0 && vn < _cVn0[c]) _cVn0[c] = vn;
            var (pS, pQ) = (lam * ns, lam * nq);
            vas -= ima * pS;
            vaq -= ima * pQ;
            wa -= iia * (ras * pQ - raq * pS);
            vbs += imb * pS;
            vbq += imb * pQ;
            wb += iib * (rbs * pQ - rbq * pS);
            // Reibung am Berührpunkt (mit der Drehung beider), Haften oder Gleiten
            dvs = vbs - wb * rbq - (vas - wa * raq);
            dvq = vbq + wb * rbs - (vaq + wa * ras);
            var vt = -dvs * nq + dvq * ns;
            var mu = MathF.Abs(vt) < 0.02f ? _cMuH[c] : _cMuG[c];
            var lt = -_cMt[c] * vt;
            var grenze = mu * neu;
            var ltNeu = Mathf.Clamp(k.Lt[c] + lt, -grenze, grenze);
            lt = ltNeu - k.Lt[c];
            k.Lt[c] = ltNeu;
            (pS, pQ) = (-lt * nq, lt * ns);
            vas -= ima * pS;
            vaq -= ima * pQ;
            wa -= iia * (ras * pQ - raq * pS);
            vbs += imb * pS;
            vbq += imb * pQ;
            wb += iib * (rbs * pQ - rbq * pS);
            // Rollwiderstand: hemmt das Drehen gegeneinander (eckige Schollen rollen nicht)
            if (_cRoll[c] > 0f)
            {
                var lr = -_cMr[c] * (wb - wa);
                var rg = _cRoll[c] * _cReff[c] * neu;
                var lrNeu = Mathf.Clamp(k.Lr[c] + lr, -rg, rg);
                lr = lrNeu - k.Lr[c];
                k.Lr[c] = lrNeu;
                wa -= iia * lr;
                wb += iib * lr;
            }
            (_vs[a], _vq[a], _om[a]) = (vas, vaq, wa);
            if (paar) (_vs[b], _vq[b], _om[b]) = (vbs, vbq, wb);
        }
    }

    /// <summary>Nach dem Schritt: Drehgrenze, Berührungen und Belastung, Kraft an Hindernissen, erste Berührungen (Aufprall), Abrieb.</summary>
    private void Nachsorge(bool letzter)
    {
        var k = _kNeu;
        if (letzter)
        {
            Array.Clear(_pfKraft);
            Array.Fill(_pfMaxScholle, -1);
            Array.Clear(_pfMaxKraft);
        }
        for (var c = 0; c < _kZahl; c++)
        {
            var a = k.A[c];
            var ln = k.Ln[c];
            var b = k.B[c];
            var art = k.Art[c];
            var tief = -_cS0[c];
            if (tief > _tiefe[a]) _tiefe[a] = tief;
            if (art == ArtPaar && tief > _tiefe[b]) _tiefe[b] = tief;
            if (ln <= 0f)
            {
                k.Beruehrt[c] = 0;
                continue;
            }
            var kraft = ln / H;
            _kontaktZahl[a]++;
            if (art == ArtPaar)
            {
                _kontaktZahl[b]++;
                if (kraft > 20f)
                {
                    _belastet[a]++;
                    _belastet[b]++;
                }
            }
            else if (art != ArtKante)
            {
                _anHindernis[a] = true;
                if (letzter)
                {
                    _druck[a] += kraft;
                    if (kraft > _druckMax[a])
                    {
                        _druckMax[a] = kraft;
                        (_druckPs[a], _druckPq[a], _druckNs[a], _druckNq[a]) = (_s[a] + _cRAs[c], _q[a] + _cRAq[c], _cNs[c], _cNq[c]);
                    }
                    if (art == ArtPfeiler || art == ArtKapsel)
                    {
                        var h = k.Index[c];
                        _pfKraft[h] += kraft;
                        if (kraft > _pfMaxKraft[h]) (_pfMaxKraft[h], _pfMaxScholle[h]) = (kraft, a);
                    }
                }
            }
            // erste Berührung: Aufprall (Tempo, mit dem sie sich näherten); Abrieb macht den Rand runder (höchstens 0,35)
            if (k.Beruehrt[c] == 0)
            {
                var v = -_cVn0[c];
                if (art == ArtPaar)
                {
                    Stoss(a, b, v, _cNs[c], _cNq[c]);
                    Stoss(b, a, v, -_cNs[c], -_cNq[c]);
                }
                else if (art != ArtKante && v > _aufprallV[a])
                {
                    _aufprallV[a] = v;
                    (_aufprallPs[a], _aufprallPq[a]) = (_s[a] + _cRAs[c], _q[a] + _cRAq[c]);
                    (_aufprallNs[a], _aufprallNq[a]) = (_cNs[c], _cNq[c]);
                    if (v > 0.2f) Ereignis[a] |= Schollenmessung.EreignisAufprall;
                }
                if (v > 0.05f)
                {
                    var abrieb = MathF.Min(v, 1.5f) * 0.004f;
                    (_mAbrieb[a], _neu[a]) = (MathF.Min(_mAbrieb[a] + abrieb, 0.35f), true);
                    if (art == ArtPaar) (_mAbrieb[b], _neu[b]) = (MathF.Min(_mAbrieb[b] + abrieb, 0.35f), true);
                }
            }
            k.Beruehrt[c] = 1;
        }
    }

    private int _oben;

    private void Stoss(int a, int b, float v, float ns, float nq)
    {
        if (v <= _stossV[a]) return;
        (_stossV[a], _stossMit[a], _stossNs[a], _stossNq[a]) = (v, b, ns, nq);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Untertauchen und Vergehen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Schritt einer tauchenden Scholle (keine Kontakte): abtauchen (1,5 s, Vorderkante nach unten), unter dem Stau mit 0,45 m/s weiter, hinter
    /// der Brücke auftauchen (1,2 s), sobald über ihr frei ist; vergehen (6 s: bündig absinken und kleiner werden).
    /// </summary>
    private void TauchSchritt(int i)
    {
        _tauchZeit[i] += H;
        var zustand = _tauchen[i];
        if (zustand == 4)
        {
            _vs[i] += (_tempo * 0.8f - _vs[i]) * 0.05f;
            _s[i] += _vs[i] * H;
            return;
        }
        _vs[i] += (0.45f - _vs[i]) * MathF.Min(H / 0.5f, 1f);
        _vq[i] *= 0.9f;
        _om[i] *= 0.9f;
        _s[i] += _vs[i] * H;
        _q[i] += _vq[i] * H;
        _w[i] += _om[i] * H;
        Kreise(i);
        if (zustand == 1 && _tauchZeit[i] >= 1.5f) (_tauchen[i], _tauchZeit[i]) = (2, 0f);
        else if (zustand == 2)
        {
            DraussenPfeiler(i);
            var achse = NaechsteBruecke(_s[i], out var hinter);
            if (achse < 0 || (hinter > 8f && (hinter > 40f || ObenFrei(i))))
                (_tauchen[i], _tauchZeit[i]) = (3, 0f);
        }
        else if (zustand == 3 && _tauchZeit[i] >= 1.2f)
        {
            (_tauchen[i], _tauchZeit[i]) = (0, 0f);
            _duldung[i] = 0.25f;
            _ohneWarm[i] = true;
            _vs[i] = _tempo * 0.7f;
        }
    }

    /// <summary>Index der Brücke, die der Stelle am nächsten liegt, und wie weit die Stelle hinter ihrer Achse liegt (negativ: davor).</summary>
    private int NaechsteBruecke(float s, out float hinter)
    {
        var (beste, ab) = (-1, float.MaxValue);
        hinter = 0;
        for (var b = 0; b < _brueckeS.Length; b++)
        {
            var d = s - _brueckeS[b];
            if (MathF.Abs(d) < ab) (ab, beste, hinter) = (MathF.Abs(d), b, d);
        }
        return beste;
    }

    /// <summary>Ob über einer tauchenden Scholle keine andere liegt (Gitter dieses Bildes).</summary>
    private bool ObenFrei(int i)
    {
        var hi = _form[i].Huelle * _r[i];
        var reich = hi + _huelleMax;
        for (var x = 0; x < _fensterZahl; x++)
        {
            // über die Fensterliste statt das Gitter: das Gitter gilt nur im Bereich dieses Bildes
            var j = _fenster[x];
            var ds = _s[j] - _s[i];
            if (ds > reich || ds < -reich) continue;
            var dq = _q[j] - _q[i];
            var hm = hi + _huelle[j];
            if (ds * ds + dq * dq < hm * hm * 0.6f) return false;
        }
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Nach den Schritten: Pfeiler unter Last, Bruch, Untertauchen, Vergehen, Ende des Laufs
    // ═════════════════════════════════════════════════════════════════════════

    private void NachDenSchritten(float dt)
    {
        _bruchKonto = MathF.Min(_bruchKonto + 3f * dt, 3f); // höchstens 3 Brüche je Sekunde
        // Steht ein Pfeiler lange unter Last (über 600 N, zufällig 20–40 s), gibt die am stärksten gedrückte Scholle dort nach: Sie bricht, oder sie
        // wird unter das Eis gedrückt – so hält kein Bogen ewig (in der Natur bricht oder taucht er auch)
        for (var h = 0; h < _feste.Count; h++)
        {
            _pfLast[h] = _pfKraft[h] > 600f ? _pfLast[h] + dt : MathF.Max(0f, _pfLast[h] - 2f * dt);
            if (_pfLast[h] <= _pfSchwelle[h] || _pfMaxScholle[h] < 0) continue;
            var i = _pfMaxScholle[h];
            _pfLast[h] = 0;
            _pfSchwelle[h] = LastVon + (LastBis - LastVon) * Zufall();
            if (Messung is not null) Messung.Notventil++;
            if (_gen[i] == 0 && _r[i] > 1f && _form[i].Laenge * _r[i] >= 2f && _freiZahl >= 2 && _tauchen[i] == 0)
            {
                _aufprallV[i] = 0;
                Brechen(i);
            }
            else if (_tauchen[i] == 0)
            {
                (_tauchen[i], _tauchZeit[i]) = (1, 0f);
                Ereignis[i] |= Schollenmessung.EreignisTauchen;
                if (Messung is not null) Messung.Taucher++;
            }
        }
        for (var x = 0; x < _fensterZahl; x++)
        {
            var i = _fenster[x];
            if (_tauchen[i] != 0 || _stamm[i] < 0) continue;
            if (_aufprallV[i] > 0 || _druck[i] > 0) BruchPruefen(i, dt);
            else if (_stossV[i] > 0.3f) TauchenPruefen(i);
        }
        if (Messung is not null)
        {
            Messung.FreieVorrat = _freiZahl;
            Messung.Vorrat = _plaetze - _n;
        }
    }

    /// <summary>
    /// Untertauchen an der Staukante: Eine Scholle stößt von stromauf an eine (fast) stehende Stau-Scholle (unter 0,3 m/s, mindestens zwei Kontakte),
    /// schneller als die Grenze 1,3 · √(0,08 · g · Dicke) – dann taucht sie mit einer Chance (30–50 %, dünne eher) unter dem Stau durch.
    /// </summary>
    private void TauchenPruefen(int i)
    {
        var j = _stossMit[i];
        if (j < 0 || _stamm[j] < 0 || _stossNs[i] < 0.6f) return; // die andere liegt stromab
        if (_vs[j] * _vs[j] + _vq[j] * _vq[j] > 0.3f * 0.3f || _kontaktZahl[j] < 2) return;
        var achse = NaechsteBruecke(_s[i], out var hinter);
        if (achse < 0 || hinter > -3f || hinter < -60f) return;
        var grenze = 1.3f * MathF.Sqrt(0.08f * 9.81f * _dk[i]);
        if (_stossV[i] < grenze) return;
        var chance = Mathf.Clamp(0.5f - 1f * (_dk[i] - 0.1f), 0.3f, 0.5f) * TauchFaktor;
        if (Zufall() > chance) return;
        (_tauchen[i], _tauchZeit[i]) = (1, 0f);
        Ereignis[i] |= Schollenmessung.EreignisTauchen;
        if (Messung is not null) Messung.Taucher++;
    }

    /// <summary>
    /// Bruch an einem Hindernis (Pfeiler, Joch, Prahm, Kahn), mit einer Chance: (a) Aufprall ab 0,6 m/s (im Eisgang 0,4), mittig 40 %, sonst 15 %,
    /// nur bei Schollen länger als 18 × Dicke (dicke, kurze spalten nicht); (b) Druck: Rate 0,5/s × (Kraft / Grenze − 1). Beides mal der Größe und
    /// mal (1 + 0,6 · Schmelzgrad), Aufprall gedeckelt bei 60 bzw. 20 %. Nur Stammschollen (keine zweite Generation), mindestens 2 m lang, 10 s Sperre
    /// je Stück, höchstens 3 Brüche je Sekunde. Wer nicht brechen kann (Bruchstück, kürzer als 2 m), wird unter hohem Druck (über dem 1,5-Fachen der
    /// Grenze) mit derselben Rate am Pfeiler unter das Eis gedrückt.
    /// </summary>
    private void BruchPruefen(int i, float dt)
    {
        var r = _r[i];
        var laenge = _form[i].Laenge * r;
        if (_gen[i] != 0 || laenge < 2f || _freiZahl < 2)
        {
            // kann nicht (mehr) brechen – ein Bruchstück, eine kleine Scholle: unter hohem Druck wird sie am Pfeiler unter das Eis gedrückt
            if (_druck[i] <= 0 || _sperre[i] > 0) return;
            var grenze = BruchSigma * _dk[i] * 2f * MathF.Max(_rGleich[i] * _rs[i], 0.3f);
            var rateT = BruchRate * MathF.Max(0f, _druck[i] / grenze - 1.5f);
            if (rateT <= 0 || Zufall() > 1f - MathF.Exp(-rateT * dt)) return;
            (_tauchen[i], _tauchZeit[i]) = (1, 0f);
            Ereignis[i] |= Schollenmessung.EreignisTauchen;
            if (Messung is not null) Messung.Taucher++;
            return;
        }
        if (_sperre[i] > 0 || _bruchKonto < 1f) return;
        var m = MathF.Max(_mTau[i], _mAbrieb[i]);
        var morsch = 1f + 0.6f * m;
        float chance = 0;
        if (_aufprallV[i] >= (_tauen > 0.3f ? 0.4f : 0.6f) && laenge >= 18f * _schollen[_stamm[i]].Dicke * (1f - 0.3f * m))
        {
            // mittig: Abstand des Berührpunkts von der Kraftlinie durch den Schwerpunkt
            var (ps, pq) = (_aufprallPs[i] - _s[i], _aufprallPq[i] - _q[i]);
            var versatz = MathF.Abs(ps * _aufprallNq[i] - pq * _aufprallNs[i]);
            var mittig = versatz < 0.3f * r;
            var groesse = Mathf.SmoothStep(0.9f, 2.5f, r) * morsch * BruchFaktor;
            chance = MathF.Min((mittig ? 0.4f : 0.15f) * groesse, MathF.Min((mittig ? 0.6f : 0.2f) * MathF.Max(1f, BruchFaktor), 1f));
        }
        if (_druck[i] > 0)
        {
            var grenzKraft = BruchSigma * _dk[i] * 2f * r;
            var rate = BruchRate * MathF.Max(0f, _druck[i] / grenzKraft - 1f) * Mathf.SmoothStep(0.7f, 2f, r) * morsch * BruchFaktor;
            chance = 1f - (1f - chance) * MathF.Exp(-rate * dt);
        }
        if (chance <= 0 || Zufall() > chance) return;
        Brechen(i);
    }

    /// <summary>
    /// Bricht eine Scholle: Der Riss läuft vom Berührpunkt zum Ursprung ihres Netzes und von dort weiter (± 20° geknickt); meist zwei Stücke, selten
    /// drei mit einem Keil zum Hindernis. Jedes Stück ist ein Ausschnitt derselben Scholle (Shader), Fläche, Schwerpunkt und Stoßkreise aus dem
    /// Umriss. Die Lage springt nicht; die Stücke behalten Tempo und Drehung und gehen mit 0,07 m/s auseinander.
    /// </summary>
    private void Brechen(int i)
    {
        _suchen = true;
        var saat = _schollen[_stamm[i]].Saat;
        var r = _r[i];
        var (sin, cos) = MathF.SinCos(_w[i]);
        var f0 = _form[i];
        // Ursprung des Netzes in Laufachsen
        var (os, oq) = (_s[i] - (f0.Cx * cos - f0.Cy * sin) * r, _q[i] - (f0.Cx * sin + f0.Cy * cos) * r);
        // Richtung vom Ursprung zum Berührpunkt (Aufprall, sonst der Kontakt mit dem größten Druck), im Netz
        var (ds, dq) = _aufprallV[i] > 0 ? (_aufprallPs[i] - os, _aufprallPq[i] - oq) : (_druckPs[i] - os, _druckPq[i] - oq);
        if (ds * ds + dq * dq < 1e-6f) (ds, dq) = _aufprallV[i] > 0 ? (_aufprallNs[i], _aufprallNq[i]) : (_druckNs[i], _druckNq[i]);
        var alpha = MathF.Atan2(ds * -sin + dq * cos, ds * cos + dq * sin);
        var knick = (Zufall() - 0.5f) * 2f * Mathf.DegToRad(20f);
        Span<float> grenzen = stackalloc float[4];
        int stuecke;
        if (Zufall() < 0.2f && _freiZahl >= 2)
        {
            var keil = Mathf.DegToRad(50f + 20f * Zufall());
            grenzen[0] = alpha + keil / 2f;
            grenzen[1] = alpha + MathF.PI + knick;
            grenzen[2] = alpha + Tau - keil / 2f;
            grenzen[3] = alpha + Tau + keil / 2f;
            stuecke = 3;
        }
        else
        {
            grenzen[0] = alpha;
            grenzen[1] = alpha + MathF.PI + knick;
            grenzen[2] = alpha + Tau;
            stuecke = 2;
        }
        var (vs, vq, om) = (_vs[i], _vq[i], _om[i]);
        var (sAlt, qAlt) = (_s[i], _q[i]);
        var (aAlt, r0Alt, gleichAlt) = (f0.A, _r0[i], _rGleich[i]);
        Span<int> plaetze = stackalloc int[3];
        for (var p = 0; p < stuecke; p++)
        {
            var platz = p == 0 ? i : _frei[--_freiZahl];
            plaetze[p] = platz;
            var (von, bis) = (grenzen[p], grenzen[p + 1]);
            var f = Ausschnitt(saat, von, bis);
            if (platz != i)
            {
                _stamm[platz] = _stamm[i];
                (_rg[platz], _rs[platz], _mTau[platz], _mAbrieb[platz], _dk[platz]) = (_rg[i], _rs[i], _mTau[i], _mAbrieb[i], _dk[i]);
                (_invM[platz], _invI[platz], _drag[platz], _dragLin[platz], _drehK[platz]) = (_invM[i], _invI[i], _drag[i], _dragLin[i], _drehK[i]);
                (_tauchen[platz], _tauchZeit[platz]) = (0, 0f);
                Zustand[platz] = Schollenmessung.ImFenster;
            }
            _form[platz] = f;
            _neu[platz] = true;
            (_sekA[platz], _sekB[platz], _gen[platz]) = (Winkel(von), Winkel(bis), 1);
            _w[platz] = _w[i];
            _r[platz] = r;
            // Schwerpunkt des Stücks; Tempo = v + ω × r, dazu auseinander (Richtung der Mitte des Ausschnitts)
            var (cs, cq) = (os + (f.Cx * cos - f.Cy * sin) * r, oq + (f.Cx * sin + f.Cy * cos) * r);
            var mitte = 0.5f * (von + bis);
            var (ms, mq) = (MathF.Cos(mitte) * cos - MathF.Sin(mitte) * sin, MathF.Cos(mitte) * sin + MathF.Sin(mitte) * cos);
            (_s[platz], _q[platz]) = (cs, cq);
            (_sAlt[platz], _qAlt[platz], _wAlt[platz]) = (cs, cq, _w[i]);
            (_vs[platz], _vq[platz], _om[platz]) = (vs - om * (cq - qAlt) + 0.07f * ms, vq + om * (cs - sAlt) + 0.07f * mq, om);
            _sperre[platz] = 10f;
            _duldung[platz] = 0.25f;
            _ohneWarm[platz] = true;
            // das Netz behält den Halbmesser der Scholle (das Stück ist ein Ausschnitt davon); gleich großer Kreis für Messung und Schmelzen
            _r0[platz] = r0Alt;
            _rGleich[platz] = gleichAlt * MathF.Sqrt(MathF.Max(f.A, 0.01f) / MathF.Max(aAlt, 0.01f));
            Ereignis[platz] |= Schollenmessung.EreignisBruch;
        }
        _bruchKonto -= 1f;
        if (Messung is not null)
        {
            Messung.Brueche++;
            Messung.BruecheHindernis++;
        }
        BruchProtokoll?.Invoke($"[wasser] Bruch: Scholle {i} (R {r:0.0} m, Saat {saat}) bei Stelle {sAlt:0.0}, quer {qAlt:0.0} in {stuecke} Stücke, Zeit {_zeit:0.0} s");
    }

    private static float Winkel(float a)
    {
        a %= Tau;
        return a < 0 ? a + Tau : a;
    }

    /// <summary>Form eines Ausschnitts [von, bis] (Netz, gegen den Uhrzeigersinn) um den Netz-Ursprung: Ursprung, Strahl, Umriss, Strahl.</summary>
    private FormWerte Ausschnitt(int saat, float von, float bis)
    {
        Span<float> x = stackalloc float[Ecken + 4], y = stackalloc float[Ecken + 4];
        var n = 0;
        (x[n], y[n]) = (0f, 0f);
        n++;
        var (ax, ay) = Strahl(saat, von);
        (x[n], y[n]) = (ax, ay);
        n++;
        var schritt = Tau / Ecken;
        var k0 = (int)MathF.Floor(von / schritt) + 1;
        for (var k = k0; k * schritt < bis; k++)
        {
            var e = ((k % Ecken) + Ecken) % Ecken;
            (x[n], y[n]) = (_umX[saat * Ecken + e], _umY[saat * Ecken + e]);
            n++;
            if (n >= x.Length - 1) break;
        }
        var (bx, by) = Strahl(saat, bis);
        (x[n], y[n]) = (bx, by);
        n++;
        return FormAusVieleck(x[..n], y[..n]);
    }

    /// <summary>Wo ein Strahl vom Netz-Ursprung im Winkel <paramref name="a"/> den Umriss schneidet (auf der Kante zwischen den Ecken davor und danach).</summary>
    private (float, float) Strahl(int saat, float a)
    {
        var schritt = Tau / Ecken;
        var k = (int)MathF.Floor(a / schritt);
        var e0 = ((k % Ecken) + Ecken) % Ecken;
        var e1 = (e0 + 1) % Ecken;
        var (p0x, p0y, p1x, p1y) = (_umX[saat * Ecken + e0], _umY[saat * Ecken + e0], _umX[saat * Ecken + e1], _umY[saat * Ecken + e1]);
        var (dx, dy) = (MathF.Cos(a), MathF.Sin(a));
        var (ex, ey) = (p1x - p0x, p1y - p0y);
        var nenner = dx * ey - dy * ex;
        var t = MathF.Abs(nenner) > 1e-9f ? -(dx * p0y - dy * p0x) / nenner : 0.5f;
        t = Mathf.Clamp(t, 0f, 1f);
        return (p0x + ex * t, p0y + ey * t);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Gezeichnete Lage
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Am Ende des Bildes je Platz: nach den Schritten das Ende des Laufs (Bruchstücke frei, Stammschollen oben neu) und zu klein Geschmolzenes
    /// (sinkt bündig ab, vergeht); dann die gezeichnete Lage, zwischen dem letzten und dem jetzigen Schritt gemittelt (Schwerpunkt, Winkel), mit
    /// Kippen und Absinken beim Tauchen und Vergehen.
    /// </summary>
    private void Abschluss(bool nachSchritten)
    {
        var alpha = _sammler / H;
        for (var i = 0; i < _plaetze; i++)
        {
            if (_stamm[i] < 0 || Zustand[i] == Schollenmessung.Aus) continue;
            if (nachSchritten && Ende(i)) continue;
            var (s, sAlt) = (_s[i], _sAlt[i]);
            (ZS[i], ZQ[i], ZW[i]) = MathF.Abs(s - sAlt) > 5f ? (s, _q[i], _w[i])
                : (sAlt + (s - sAlt) * alpha, _qAlt[i] + (_q[i] - _qAlt[i]) * alpha, _wAlt[i] + (_w[i] - _wAlt[i]) * alpha);
            ZR[i] = _r[i];
            ZDicke[i] = _dk[i];
            ZM[i] = MathF.Max(_mTau[i], _mAbrieb[i]);
            if (_tauchen[i] == 0)
            {
                (ZKipp[i], ZSenk[i]) = (0f, 0f);
                continue;
            }
            float kipp = 0, senk = 0;
            var tz = _tauchZeit[i] + alpha * H;
            switch (_tauchen[i])
            {
                case 1:
                    kipp = -Mathf.DegToRad(25f) * Mathf.SmoothStep(0f, 1.5f, tz);
                    senk = (_dk[i] + 0.15f) * Mathf.SmoothStep(0.2f, 1.5f, tz);
                    break;
                case 2:
                    senk = 9f; // unter Wasser, nicht gezeichnet
                    break;
                case 3:
                    kipp = Mathf.DegToRad(8f) * (1f - Mathf.SmoothStep(0f, 1.2f, tz));
                    senk = (_dk[i] + 0.15f) * (1f - Mathf.SmoothStep(0f, 1.2f, tz));
                    break;
                case 4:
                    // vergeht: sinkt bündig ab und wird dabei kleiner
                    senk = (0.2f * _dk[i] + 0.02f) * 4f * Mathf.SmoothStep(0f, 6f, tz);
                    ZR[i] = _r[i] * (1f - 0.35f * Mathf.SmoothStep(0f, 6f, tz));
                    break;
            }
            (ZKipp[i], ZSenk[i]) = (kipp, senk);
        }
    }

    /// <summary>Ende des Laufs und Vergehen; liefert true, wenn der Platz jetzt frei ist (nicht mehr zeichnen).</summary>
    private bool Ende(int i)
    {
        // Ende des Laufs: Bruchstücke frei, Stammschollen oben neu
        if (_s[i] >= _laenge || _s[i] < 0f)
        {
            if (i >= _n)
            {
                Freigeben(i);
                return true;
            }
            Nachschub(i);
            return false;
        }
        // vergangen (zu klein geschmolzen): bündig abgesunken
        if (_tauchen[i] == 4)
        {
            if (_tauchZeit[i] < 6f) return false;
            if (Messung is not null) Messung.Vergangen++;
            if (i >= _n)
            {
                Freigeben(i);
                return true;
            }
            Nachschub(i);
            return false;
        }
        if (_tauchen[i] == 0 && _rGleich[i] * _rs[i] < 0.15f && _rg[i] > 0.95f && _rs[i] < 0.999f && _tauen > 0.3f) (_tauchen[i], _tauchZeit[i]) = (4, 0f);
        return false;
    }

    /// <summary>Ursprung des Netzes (Laufachsen) eines Platzes in der gezeichneten Lage: Schwerpunkt minus dem Versatz des Schwerpunkts im Netz.</summary>
    public (float S, float Q) Ursprung(int i)
    {
        var (sin, cos) = MathF.SinCos(ZW[i]);
        ref readonly var f = ref _form[i];
        var r = ZR[i];
        return (ZS[i] - (f.Cx * cos - f.Cy * sin) * r, ZQ[i] - (f.Cx * sin + f.Cy * cos) * r);
    }

    /// <summary>Schollen, deren Drehung im letzten Bild an der Obergrenze lag (Messung).</summary>
    public int Obergrenze => _oben;

    /// <summary>Stoßkreise aller Schollen im Fenster auf die jetzige Lage (nur für die Messung: im Schritt rechnet die Simulation sie nur für Schollen mit Kontakten).</summary>
    public void KreiseFuerMessung()
    {
        for (var x = 0; x < _fensterZahl; x++) Kreise(_fenster[x]);
    }

    /// <summary>Tempo eines Platzes (Laufachsen, m/s) und Drehtempo (rad/s).</summary>
    public (float S, float Q, float Om) Tempo(int i) => (_vs[i], _vq[i], _om[i]);

    /// <summary>Saat der Form, Ausschnitt (−1 = ganz) und Stamm eines Platzes – für die Zeichnung.</summary>
    public int Saat(int i) => _stamm[i] < 0 ? 0 : _schollen[_stamm[i]].Saat;
    public float SektorA(int i) => _sekA[i];
    public float SektorB(int i) => _sekB[i];
    public float Ausgangshalbmesser(int i) => _stamm[i] < 0 ? 0 : _rGleich[i];

    /// <summary>
    /// Umriss eines Platzes in Laufachsen, wie er gezeichnet wird (Bruchstück: Ursprung, Strahl, Umriss, Strahl) – für die Prüfhilfe
    /// <c>--schollenumriss</c> und den Nachbau. Liefert die Zahl der Ecken (höchstens 28).
    /// </summary>
    public int UmrissInLauf(int i, Span<float> ps, Span<float> pq)
    {
        if (_stamm[i] < 0) return 0;
        var saat = Saat(i);
        var r = ZR[i];
        var (sin, cos) = MathF.SinCos(ZW[i]);
        Span<float> x = stackalloc float[Ecken + 4], y = stackalloc float[Ecken + 4];
        var n = 0;
        if (_sekA[i] < 0)
            for (var k = 0; k < Ecken; k++) (x[n], y[n++]) = (_umX[saat * Ecken + k], _umY[saat * Ecken + k]);
        else
        {
            var (von, bis) = (_sekA[i], _sekB[i]);
            if (bis <= von) bis += Tau;
            (x[n], y[n++]) = (0f, 0f);
            (x[n], y[n]) = Strahl(saat, von);
            n++;
            var schritt = Tau / Ecken;
            for (var k = (int)MathF.Floor(von / schritt) + 1; k * schritt < bis && n < Ecken + 2; k++)
            {
                var e = ((k % Ecken) + Ecken) % Ecken;
                (x[n], y[n++]) = (_umX[saat * Ecken + e], _umY[saat * Ecken + e]);
            }
            (x[n], y[n]) = Strahl(saat, bis);
            n++;
        }
        var (os, oq) = Ursprung(i);
        for (var k = 0; k < n; k++)
            (ps[k], pq[k]) = (os + (x[k] * cos - y[k] * sin) * r, oq + (x[k] * sin + y[k] * cos) * r);
        return n;
    }

    /// <summary>Stoßkreis k (0, 1) eines Platzes in Laufachsen (Mitte, Halbmesser; 0 = keiner) – für die Prüfhilfe <c>--schollenumriss</c>.</summary>
    public (float S, float Q, float R) Kreis(int i, int k) => (_wx[2 * i + k], _wy[2 * i + k], _wr[2 * i + k]);
}
