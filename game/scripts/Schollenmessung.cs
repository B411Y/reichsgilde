using System;
using System.Collections.Generic;

namespace Reichsgilde;

/// <summary>
/// Misst das Verhalten der treibenden Eisschollen (Startoption <c>--schollenmessung=1</c>, <c>docs/wasser/phase3.md</c>, Abschnitt „Schollen 2“):
/// Drehen großer Schollen, Sprünge der Lage von Bild zu Bild, Durchdringung der Stoßkreise, Stau an den Brückenpfeilern, Durchsatz, Rechenzeit und
/// Speicheranforderung. Liefert alle 10 s eine Zeile „[wasser] Schollen-Verhalten …“ (<see cref="Zeile"/>). Gerechnet wird nur mit der Option; alle
/// Felder werden beim Bau angelegt (keine Speicheranforderung je Bild). Kennt Godot nicht: Der Nachbau im Notizordner bindet die Datei unverändert ein.
/// <para>Zonen wie im Nachbau: <b>Brücke</b> = 30 m stromauf bis 6 m stromab der Pfeilerachse der Brücke, die dem Blickpunkt am nächsten liegt;
/// <b>frei</b> = 80–150 m stromauf des Blickpunkts. „Groß“ heißt ein Ausgangshalbmesser ab 1,5 m. Ein <b>Sprung</b> ist eine Änderung des
/// Scheintempos (aus der gezeichneten Lage) von einem Bild zum nächsten um mehr als 0,5 bzw. 1,5 m/s; nicht gezählt werden Bilder mit Umlauf,
/// Neueinsetzen, Bruch, Tauchen oder einer Bildzeit über 0,1 s, „ohne Aufprall“ zusätzlich nicht die Bilder, in denen eine Scholle neu an ein
/// Hindernis stößt (dort bremst sie wirklich hart).</para>
/// </summary>
public sealed class Schollenmessung
{
    /// <summary>Ereignisse je Scholle im Bild (Bits): Die Lage springt gewollt (Umlauf, neu eingesetzt, Fenstereintritt), Bruch, Tauchen, neuer Stoß an ein Hindernis.</summary>
    public const byte EreignisVersetzt = 1, EreignisBruch = 2, EreignisTauchen = 4, EreignisAufprall = 8;

    /// <summary>Zustand je Platz: 0 = nicht da, 1 = aktiv im Stoßfenster, 2 = aktiv außerhalb (treibt nur).</summary>
    public const byte Aus = 0, ImFenster = 1, Draussen = 2;

    /// <summary>Die Felder der Simulation (einmal gesetzt; die Simulation füllt sie, die Messung liest nur).</summary>
    public sealed class Daten
    {
        public int Plaetze;
        /// <summary>Gezeichnete Lage (Laufachsen, m), Tempo (m/s), Drehtempo (rad/s), Ausgangshalbmesser (m).</summary>
        public float[] S = Array.Empty<float>(), Q = Array.Empty<float>(), Vs = Array.Empty<float>(), Vq = Array.Empty<float>(), Om = Array.Empty<float>(), R0 = Array.Empty<float>();
        public byte[] Zustand = Array.Empty<byte>(), Ereignis = Array.Empty<byte>();
        /// <summary>Stoßkreise: je Platz <see cref="KreisSchritt"/> Einträge (Mitte s, q und Halbmesser; Halbmesser 0 = keiner).</summary>
        public float[] KreisS = Array.Empty<float>(), KreisQ = Array.Empty<float>(), KreisR = Array.Empty<float>();
        public int KreisSchritt = 1;
        public float Laenge;
    }

    /// <summary>Ein Histogramm mit zwei linearen Bereichen (fein bis <c>feinBis</c>, grob bis <c>grobBis</c>, darüber der letzte Eimer) und dem Höchstwert.</summary>
    private sealed class Histogramm
    {
        private readonly float _feinBis, _fein, _grob;
        private readonly int _feinZahl;
        private readonly long[] _eimer;
        public long Zahl { get; private set; }
        public float Hoechst { get; private set; }
        public double Summe { get; private set; }

        public Histogramm(float feinBis, float fein, float grobBis, float grob)
        {
            (_feinBis, _fein, _grob) = (feinBis, fein, grob);
            _feinZahl = (int)MathF.Ceiling(feinBis / fein);
            _eimer = new long[_feinZahl + (int)MathF.Ceiling((grobBis - feinBis) / grob) + 1];
        }

        public void Dazu(float x)
        {
            if (!(x >= 0)) x = 0;
            var k = x < _feinBis ? (int)(x / _fein) : _feinZahl + (int)((x - _feinBis) / _grob);
            _eimer[Math.Min(k, _eimer.Length - 1)]++;
            Zahl++;
            Summe += x;
            if (x > Hoechst) Hoechst = x;
        }

        /// <summary>Der Wert, unter dem der Anteil <paramref name="p"/> der Proben liegt (Mitte des Eimers, höchstens der Höchstwert).</summary>
        public float P(double p)
        {
            if (Zahl == 0) return float.NaN;
            var ziel = Math.Max((long)Math.Ceiling(p * Zahl), 1);
            long summe = 0;
            for (var k = 0; k < _eimer.Length; k++)
            {
                summe += _eimer[k];
                if (summe >= ziel) return Math.Min(k < _feinZahl ? (k + 0.5f) * _fein : _feinBis + (k - _feinZahl + 0.5f) * _grob, Hoechst);
            }
            return Hoechst;
        }

        public void Leeren()
        {
            Array.Clear(_eimer);
            (Zahl, Hoechst, Summe) = (0, 0, 0);
        }
    }

    private readonly Daten _d;
    private readonly float[] _altS, _altQ, _altVs, _altVq;
    private readonly byte[] _alt; // 0 = kein Vorbild, 1 = Lage des Vorbilds, 2 = auch sein Scheintempo
    private readonly int[] _zone;  // Plätze in der Brückenzone (für die Durchdringung)
    private readonly Histogramm _drehBruecke = new(20f, 0.05f, 2000f, 2f), _drehFrei = new(20f, 0.05f, 2000f, 2f);
    private readonly Histogramm _tiefe = new(2f, 0.005f, 4f, 0.05f), _simMs = new(20f, 0.01f, 200f, 0.5f);
    private long _dreherProben, _dreher, _spruenge05, _spruenge15, _ohneStoss05, _ohneStoss15, _sprungProben, _ohneStossProben;
    private float _sprungHoechst, _ohneStossHoechst;
    private long _stehendSumme, _bilder, _durch, _kontaktSumme, _bytesSumme, _bytesHoechst, _oben;
    private double _messZeit, _uhr;
    private int _bild;

    // Brücken (aus den festen Hindernissen): Achse (Stelle) und Lücken zwischen den Pfeilern (quer, mit den äußeren bis zur Eiskante)
    private readonly float[] _brueckeS;
    private readonly float[][] _lueckeVon, _lueckeBis;
    private readonly double[][] _letzterDurchgang;
    private readonly bool[][] _stauAktiv;
    private readonly int[][] _stehendVor;
    private long _stauEreignisse;
    private double _stauDauerSumme, _stauDauerHoechst, _stauLaufendHoechst;
    private int _bruecke = -1;
    private float _sBlick;

    /// <summary>Zähler, die die Simulation selbst führt (seit der letzten Zeile).</summary>
    public long Brueche, BruecheHindernis, Vergangen, Nachschub, Taucher, Notventil, KontaktUeberlauf, Obergrenze;
    public int FreieVorrat = -1, Vorrat = -1;

    /// <param name="daten">Die Felder der Simulation.</param>
    /// <param name="pfeiler">Feste Hindernisse (quer, Stelle, halbe Breite quer); Pfeiler innerhalb von 12 m Stelle bilden eine Brücke.</param>
    /// <param name="platzQuer">Halbe nutzbare Breite an einer Stelle (für die äußeren Lücken).</param>
    public Schollenmessung(Daten daten, IReadOnlyList<(float Quer, float Stelle, float HalbQuer)> pfeiler, Func<float, float> platzQuer)
    {
        _d = daten;
        var n = daten.Plaetze;
        (_altS, _altQ, _altVs, _altVq, _alt, _zone) = (new float[n], new float[n], new float[n], new float[n], new byte[n], new int[n]);
        var liste = new List<(float Quer, float Stelle, float HalbQuer)>(pfeiler);
        liste.Sort((a, b) => a.Stelle.CompareTo(b.Stelle));
        var gruppen = new List<List<(float Quer, float Stelle, float HalbQuer)>>();
        foreach (var p in liste)
        {
            if (gruppen.Count == 0 || p.Stelle - gruppen[^1][^1].Stelle > 12f) gruppen.Add(new());
            gruppen[^1].Add(p);
        }
        var g = gruppen.Count;
        (_brueckeS, _lueckeVon, _lueckeBis) = (new float[g], new float[g][], new float[g][]);
        (_letzterDurchgang, _stauAktiv, _stehendVor) = (new double[g][], new bool[g][], new int[g][]);
        for (var k = 0; k < g; k++)
        {
            var b = gruppen[k];
            var summe = 0f;
            foreach (var p in b) summe += p.Stelle;
            _brueckeS[k] = summe / b.Count;
            b.Sort((x, y) => x.Quer.CompareTo(y.Quer));
            var platz = platzQuer(_brueckeS[k]);
            var zahl = b.Count + 1;
            (_lueckeVon[k], _lueckeBis[k]) = (new float[zahl], new float[zahl]);
            for (var j = 0; j < zahl; j++)
            {
                _lueckeVon[k][j] = j == 0 ? -platz : b[j - 1].Quer + b[j - 1].HalbQuer;
                _lueckeBis[k][j] = j == b.Count ? platz : b[j].Quer - b[j].HalbQuer;
            }
            (_letzterDurchgang[k], _stauAktiv[k], _stehendVor[k]) = (new double[zahl], new bool[zahl], new int[zahl]);
        }
    }

    private bool InBruecke(float s)
    {
        if (_bruecke < 0) return false;
        var ds = s - _brueckeS[_bruecke];
        return ds > -30f && ds < 6f;
    }

    private bool ImFreien(float s)
    {
        var ds = _sBlick - s;
        return ds > 80f && ds < 150f;
    }

    private int Luecke(float q)
    {
        var (von, bis) = (_lueckeVon[_bruecke], _lueckeBis[_bruecke]);
        for (var l = 0; l < von.Length; l++)
            if (q >= von[l] && q <= bis[l]) return l;
        return -1;
    }

    /// <summary>
    /// Nach jedem Bild der Simulation. <paramref name="dt"/> = Bildzeit (Echtzeit, s), <paramref name="sBlick"/> = Stelle des Blickpunkts,
    /// <paramref name="kontakte"/> = Zahl der Kontakte, <paramref name="simMs"/> = Rechenzeit der Simulation, <paramref name="bytes"/> = Speicher,
    /// den das Bild angefordert hat; <paramref name="oben"/> = Schollen an der Obergrenze des Drehtempos.
    /// </summary>
    public void Bild(double dt, float sBlick, int kontakte, double simMs, long bytes, int oben = 0)
    {
        _bild++;
        _uhr += dt;
        _messZeit += dt;
        _bilder++;
        _sBlick = sBlick;
        _kontaktSumme += kontakte;
        _simMs.Dazu((float)simMs);
        _bytesSumme += bytes;
        _bytesHoechst = Math.Max(_bytesHoechst, bytes);
        _oben += oben;
        _bruecke = -1;
        var beste = float.MaxValue;
        for (var k = 0; k < _brueckeS.Length; k++)
        {
            var a = MathF.Abs(_brueckeS[k] - sBlick);
            if (a < beste) (beste, _bruecke) = (a, k);
        }
        var b = _bruecke;
        if (b >= 0) Array.Clear(_stehendVor[b]);
        var fdt = (float)dt;
        var gueltig = dt > 1e-4 && dt <= 0.1;
        var (stehend, zahlZone) = (0, 0);
        var d = _d;
        for (var i = 0; i < d.Plaetze; i++)
        {
            if (d.Zustand[i] == Aus)
            {
                _alt[i] = 0;
                continue;
            }
            var (s, q) = (d.S[i], d.Q[i]);
            var ereignis = d.Ereignis[i];
            var imFenster = d.Zustand[i] == ImFenster;
            var zone = !imFenster ? 0 : InBruecke(s) ? 1 : ImFreien(s) ? 2 : 0;
            var v2 = d.Vs[i] * d.Vs[i] + d.Vq[i] * d.Vq[i];
            if (zone != 0)
            {
                var omGrad = MathF.Abs(d.Om[i]) * (180f / MathF.PI);
                if (d.R0[i] >= 1.5f)
                {
                    (zone == 1 ? _drehBruecke : _drehFrei).Dazu(omGrad);
                    if (zone == 1)
                    {
                        _dreherProben++;
                        if (v2 < 0.25f * 0.25f && omGrad > 15f) _dreher++;
                    }
                }
                if (zone == 1)
                {
                    if (v2 < 0.1f * 0.1f) stehend++;
                    _zone[zahlZone++] = i;
                }
            }
            // Scheintempo aus der gezeichneten Lage; Sprung = seine Änderung von Bild zu Bild
            var versetzt = (ereignis & (EreignisVersetzt | EreignisBruch | EreignisTauchen)) != 0;
            var vorher = _alt[i];
            if (vorher > 0 && !versetzt && gueltig)
            {
                var ds = s - _altS[i];
                if (MathF.Abs(ds) > d.Laenge / 2) ds = 0; // Umlauf am Ende des Laufs
                var (vs, vq) = (ds / fdt, (q - _altQ[i]) / fdt);
                if (vorher > 1 && zone != 0)
                {
                    var (es, eq) = (vs - _altVs[i], vq - _altVq[i]);
                    var e = MathF.Sqrt(es * es + eq * eq);
                    _sprungProben++;
                    if (e > 0.5f) _spruenge05++;
                    if (e > 1.5f) _spruenge15++;
                    _sprungHoechst = MathF.Max(_sprungHoechst, e);
                    if ((ereignis & EreignisAufprall) == 0)
                    {
                        _ohneStossProben++;
                        if (e > 0.5f) _ohneStoss05++;
                        if (e > 1.5f) _ohneStoss15++;
                        _ohneStossHoechst = MathF.Max(_ohneStossHoechst, e);
                    }
                }
                (_altVs[i], _altVq[i]) = (vs, vq);
                _alt[i] = 2;
            }
            else _alt[i] = 1;
            // Durchgang durch die Pfeilerachse; Stand vor den Lücken
            if (b >= 0 && imFenster)
            {
                var achse = _brueckeS[b];
                if (vorher > 0 && _altS[i] < achse && s >= achse && s - _altS[i] < 5f)
                {
                    _durch++;
                    var l = Luecke(q);
                    if (l >= 0)
                    {
                        if (_stauAktiv[b][l])
                        {
                            var dauer = _uhr - _letzterDurchgang[b][l];
                            _stauDauerSumme += dauer;
                            _stauDauerHoechst = Math.Max(_stauDauerHoechst, dauer);
                            _stauEreignisse++;
                            _stauAktiv[b][l] = false;
                        }
                        _letzterDurchgang[b][l] = _uhr;
                    }
                }
                var vor = achse - s;
                if (vor > 3f && vor < 15f && v2 < 0.1f * 0.1f && Luecke(q) is var lv and >= 0) _stehendVor[b][lv]++;
            }
            (_altS[i], _altQ[i]) = (s, q);
        }
        _stehendSumme += stehend;
        // Stau je Lücke: beginnt, wenn mindestens 3 Schollen davor stehen und seit 3 s keine durchkam; endet mit dem nächsten Durchgang
        if (b >= 0)
            for (var l = 0; l < _stehendVor[b].Length; l++)
            {
                var seit = _uhr - _letzterDurchgang[b][l];
                if (!_stauAktiv[b][l] && _stehendVor[b][l] >= 3 && seit > 3) _stauAktiv[b][l] = true;
                if (_stauAktiv[b][l]) _stauLaufendHoechst = Math.Max(_stauLaufendHoechst, seit);
            }
        if (_bild % 10 == 0 && b >= 0) Durchdringung(zahlZone);
    }

    /// <summary>Durchdringung der Stoßkreise in der Brückenzone (Anteil des kleineren Halbmessers), rohe Paarsuche über die Schollen dort.</summary>
    private void Durchdringung(int zahl)
    {
        var d = _d;
        var k = d.KreisSchritt;
        for (var x = 0; x < zahl; x++)
        {
            var i = _zone[x];
            for (var y = x + 1; y < zahl; y++)
            {
                var j = _zone[y];
                var (ds, dq) = (d.S[j] - d.S[i], d.Q[j] - d.Q[i]);
                if (ds * ds + dq * dq > 100f) continue;
                var tief = 0f;
                for (var a = 0; a < k; a++)
                {
                    var ra = d.KreisR[i * k + a];
                    if (ra <= 0) continue;
                    for (var c = 0; c < k; c++)
                    {
                        var rb = d.KreisR[j * k + c];
                        if (rb <= 0) continue;
                        var (es, eq) = (d.KreisS[j * k + c] - d.KreisS[i * k + a], d.KreisQ[j * k + c] - d.KreisQ[i * k + a]);
                        var t = (ra + rb - MathF.Sqrt(es * es + eq * eq)) / MathF.Min(ra, rb);
                        if (t > tief) tief = t;
                    }
                }
                if (tief > 0) _tiefe.Dazu(tief);
            }
        }
    }

    /// <summary>Alle 10 s eine Zeile (sonst null); danach beginnt die Messung von vorn.</summary>
    public string? Zeile()
    {
        if (_messZeit < 10) return null;
        var bilder = Math.Max(_bilder, 1);
        var sek = Math.Max(_messZeit, 1e-3);
        var jeTausend = 1000.0 / Math.Max(_sprungProben, 1);
        var jeTausendOhne = 1000.0 / Math.Max(_ohneStossProben, 1);
        static string P(Histogramm h) => h.Zahl == 0 ? "–" : $"{h.P(0.5):0.00}/{h.P(0.95):0.00}/{h.Hoechst:0.0}";
        var zeile = $"[wasser] Schollen-Verhalten ({_bilder} Bilder, {sek:0.0} s): Drehen groß P50/P95/max Brücke {P(_drehBruecke)} °/s (n={_drehBruecke.Zahl}), " +
                    $"frei {P(_drehFrei)} °/s (n={_drehFrei.Zahl}); Dreher {(double)_dreher / Math.Max(_dreherProben, 1) * 100:0.0} %; " +
                    $"Sprünge >0,5/>1,5 m/s je 1000: {_spruenge05 * jeTausend:0.00}/{_spruenge15 * jeTausend:0.00}, max {_sprungHoechst:0.00} m/s " +
                    $"(ohne Aufprall {_ohneStoss05 * jeTausendOhne:0.00}/{_ohneStoss15 * jeTausendOhne:0.00}, max {_ohneStossHoechst:0.00}; n={_sprungProben}); " +
                    $"Durchdringung P50/P99 {(_tiefe.Zahl == 0 ? "–" : $"{_tiefe.P(0.5):0.00}/{_tiefe.P(0.99):0.00}")} (n={_tiefe.Zahl}); " +
                    $"stehend {(double)_stehendSumme / bilder:0.0}; Durchsatz {_durch / sek * 60:0}/min; " +
                    $"Stau {_stauEreignisse} gelöst, Dauer Ø {(_stauEreignisse > 0 ? _stauDauerSumme / _stauEreignisse : 0):0} s, max {_stauDauerHoechst:0} s (laufend max {_stauLaufendHoechst:0} s); " +
                    $"Brüche {Brueche} (an Hindernissen {BruecheHindernis}), vergangen {Vergangen}, Nachschub {Nachschub}, Taucher {Taucher}, Notventil {Notventil}, " +
                    $"Vorrat frei {FreieVorrat}/{Vorrat}, Kontaktüberlauf {KontaktUeberlauf}, an der Drehgrenze {(double)_oben / bilder:0.0}; " +
                    $"Kontakte Ø {(double)_kontaktSumme / bilder:0}; Sim Ø {_simMs.Summe / Math.Max(_simMs.Zahl, 1):0.000} ms, P95 {_simMs.P(0.95):0.00} ms; " +
                    $"Speicher Ø {(double)_bytesSumme / bilder:0} B/Bild, max {_bytesHoechst} B";
        _drehBruecke.Leeren();
        _drehFrei.Leeren();
        _tiefe.Leeren();
        _simMs.Leeren();
        (_dreherProben, _dreher, _spruenge05, _spruenge15, _ohneStoss05, _ohneStoss15, _sprungProben, _ohneStossProben) = (0, 0, 0, 0, 0, 0, 0, 0);
        (_sprungHoechst, _ohneStossHoechst) = (0, 0);
        (_stehendSumme, _bilder, _durch, _kontaktSumme, _bytesSumme, _bytesHoechst, _oben) = (0, 0, 0, 0, 0, 0, 0);
        (_stauEreignisse, _stauDauerSumme, _stauDauerHoechst, _stauLaufendHoechst) = (0, 0, 0, 0);
        (Brueche, BruecheHindernis, Vergangen, Nachschub, Taucher, Notventil, KontaktUeberlauf, Obergrenze) = (0, 0, 0, 0, 0, 0, 0, 0);
        _messZeit = 0;
        return zeile;
    }
}
