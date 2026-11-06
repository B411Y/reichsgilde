using Godot;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Hafenarbeit (Teil der <c>partial class Stadtansicht</c>; Entladen Stufe 3, Spiel-KI 6.11.2026): Die Träger am Hafen sind echte Einwohner, die der
/// Kern an den Hafen schickt (<see cref="Hafenarbeit"/>, <see cref="Stadtort.Hafen"/>). Sie kommen wie jeder Passant durch die Gassen bis an den Kai
/// (<see cref="HafenLandpunkt"/>); von dort übernimmt diese Datei: Jeder Träger hat einen Platz in der Schicht und folgt den Gängen des Kerns
/// (<see cref="Hafengang"/>) – beim Niederländer nimmt er ein Stück vom Stapel am Stegkopf, trägt es über den Zugang des Stegs zum Lagerhaus des
/// Hafens, legt es ab und kommt zurück; beim Oberländer umgekehrt. Der Kran setzt auf den Stapel ab bzw. hebt von ihm (Stadtansicht.Kranladen.cs),
/// der Stapel ist nur Puffer (<see cref="Hafenschicht.StapelUm"/>). Der Vorarbeiter steht am Stegkopf, weist an (Bewegung „Reden“) und trägt im
/// Dunkeln eine Laterne.
///
/// Alles ist eine Funktion der Spielminute und der Ordnung des Kerns; zwischen den Gängen gehen die Figuren zum nächsten Ausgangspunkt. Getragen
/// wird mit der Bewegung „Tragen“ und dem Stück der Ladung auf der Schulter. Der Weg ist im Zeitraffer wie der Kran: ein Gang dauert eine
/// <see cref="Hafenschicht.Runde"/> Spielminuten, bei Tempo 1 also gut neun Sekunden für etwa 30 m hin und zurück.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wo der Vorarbeiter steht (Steg-Raum, Land bei +Z): westlich des Stapels am Stegkopf, mit Blick auf Stapel und Schiff.</summary>
    private static readonly Vector3 VorarbeiterPlatz = new(-4.8f, StegDeck, -8.7f);

    /// <summary>Wie schnell eine Figur zwischen den Gängen geht (Meter je Spielminute): 1,3 m/s bei Tempo 1.</summary>
    private const float HafenGehen = 1.3f / (float)Reichsgilde.Core.Zeit.Taktgeber.SekundenJeSpielminute;

    /// <summary>Der Plan eines Besuchs und je Gang, welcher Platz des Stapels gemeint ist (oben aufgesetzt bzw. oben weggenommen).</summary>
    private sealed class HafenPlan
    {
        public required Hafenschicht Schicht { get; init; }
        /// <summary>Je Gang (Index = Stück): Platz des Stapels, an dem der Träger nimmt (Entladen) bzw. ablegt (Beladen).</summary>
        public required int[] TraegerPlatz { get; init; }
        /// <summary>Je Hub des Krans: Platz des Stapels, auf den er absetzt (Entladen) bzw. von dem er hebt (Beladen).</summary>
        public required int[] KranPlatz { get; init; }
    }

    private long _hafenPlanFenster = long.MinValue;
    private HafenPlan? _hafenPlan;

    /// <summary>Der Plan der Hafenarbeit für den Besuch im Fenster der Minute (null ohne Hafenarbeit, im Winter oder ohne Daten).</summary>
    private HafenPlan? HafenPlanUm(double minute)
    {
        var fenster = (long)Math.Floor(minute / Zierfahrplan.Fenster);
        if (fenster == _hafenPlanFenster) return _hafenPlan;
        _hafenPlanFenster = fenster;
        _hafenPlan = null;
        if (_simulation?.Wirtschaft?.Einwohnerschaft is not { IstAktiv: true } ew || ew.Hafenarbeit.Schicht(fenster) is not { } s) return null;
        var traeger = new int[s.Stuecke];
        var kran = new int[s.Stuecke];
        foreach (var g in s.Gaenge)
        {
            // Wer oben nimmt bzw. ablegt: der oberste Platz kurz davor
            traeger[g.Stueck] = s.Entladen ? Math.Max(0, s.StapelUm(g.VomStapel - 1e-6) - 1) : s.StapelUm(g.AufStapel - 1e-6);
            kran[g.Stueck] = s.Entladen ? s.StapelUm(g.AufStapel - 1e-6) : Math.Max(0, s.StapelUm(g.VomStapel - 1e-6) - 1);
        }
        return _hafenPlan = new HafenPlan { Schicht = s, TraegerPlatz = traeger, KranPlatz = kran };
    }

    // ── Orte ──────────────────────────────────────────────────────────────────────────────────────────────

    private Node3D? HafenSteg()
    {
        if (_simulation?.Wirtschaft is not { } w || w.Schiffahrt.Haefen.Count == 0) return null;
        return _stege.TryGetValue(w.Schiffahrt.Haefen[0].Kennung, out var steg) && IsInstanceValid(steg) && steg.IsInsideTree() ? steg : null;
    }

    /// <summary>Ein Punkt im Raum des Stegs (Land bei +Z) in Koordinaten der Stadt.</summary>
    private Vector3 StegPunkt(Node3D steg, Vector3 lokal) => ToLocal(steg.ToGlobal(lokal));

    /// <summary>Wo die Gassen am Hafen enden: auf dem Kai hinter dem Fuß des Stegs. Dorthin gehen die Einwohner mit Ort Hafen, von dort übernimmt die Hafenarbeit.</summary>
    private Vector3? HafenLandpunkt() => HafenSteg() is { } steg ? AufFuss(StegPunkt(steg, new Vector3(0, 0.25f, 3.5f))) : null;

    /// <summary>
    /// Wo die Träger die Stücke ablegen bzw. holen: vor der Wasserseite des Lagerhauses im Hafenmodell (Modell-Raum −2,5 / −1,8; das Haus reicht bis −1,3).
    /// Ohne Hafenmodell ein Punkt auf dem Land links hinter dem Steg.
    /// </summary>
    private Vector3? HafenLagerpunkt()
    {
        if (HafenSteg() is not { } steg || _simulation?.Wirtschaft is not { } w) return null;
        var name = "Hafen_" + w.Schiffahrt.Haefen[0].Kennung;
        foreach (var h in _haefen)
            if (IsInstanceValid(h.Wurzel) && h.Wurzel.IsInsideTree() && h.Wurzel.Name == name)
                return AufFuss(ToLocal(h.Wurzel.ToGlobal(new Vector3(-2.5f, 0, -1.8f))));
        return AufFuss(StegPunkt(steg, new Vector3(-3.5f, 0.25f, 5.5f)));
    }

    /// <summary>
    /// Der Weg eines Gangs vom Stapel (Platz <paramref name="platz"/>) zum Lager: an die Landseite des Stapels, über den Zugang des Stegs, den Kai hinauf
    /// und zum Lagerhaus. <paramref name="spur"/> versetzt die Träger auf dem 3 m breiten Zugang nebeneinander.
    /// </summary>
    private Vector3[] HafenWeg(Node3D steg, int platz, float spur)
    {
        var (x, _, _, _) = ZierStapelPlatz(platz);
        var lager = HafenLagerpunkt() ?? StegPunkt(steg, new Vector3(-3.5f, 0.25f, 5.5f));
        return new[]
        {
            StegPunkt(steg, new Vector3(x, StegDeck, -7.95f)),
            StegPunkt(steg, new Vector3(spur, StegDeck, -7.4f)),
            StegPunkt(steg, new Vector3(spur, 0.25f, 0.4f)),
            AufFuss(StegPunkt(steg, new Vector3(spur - 0.6f, 0.25f, 2.6f))),
            lager,
        };
    }

    private static float Weglaenge(Vector3[] w)
    {
        var l = 0f;
        for (var i = 1; i < w.Length; i++) l += w[i - 1].DistanceTo(w[i]);
        return l;
    }

    /// <summary>Punkt und Richtung bei <paramref name="anteil"/> (0…1) entlang des Weges.</summary>
    private static (Vector3 Ort, Vector3 Richtung) AufWeg(Vector3[] w, float anteil)
    {
        var rest = Mathf.Clamp(anteil, 0, 1) * Weglaenge(w);
        for (var i = 1; i < w.Length; i++)
        {
            var l = w[i - 1].DistanceTo(w[i]);
            if (rest <= l || i == w.Length - 1)
                return (w[i - 1].Lerp(w[i], l <= 1e-4f ? 1 : Mathf.Clamp(rest / l, 0, 1)), (w[i] - w[i - 1]).Normalized());
            rest -= l;
        }
        return (w[^1], Vector3.Forward);
    }

    // ── Wer trägt auf welchem Platz ───────────────────────────────────────────────────────────────────────

    private readonly Dictionary<int, int> _hafenPlaetze = new();
    private int _hafenVorarbeiter = -1;
    private long _hafenOrdnungBild = -1;

    /// <summary>
    /// Einmal je Bild: welcher Einwohner gerade auf welchem Platz trägt. Grundlage ist die Reihenfolge des Kerns (<see cref="Hafenarbeit.Traeger"/>).
    /// Wer zur vollen Stunde abgelöst wurde, trägt weiter, bis der Neue da ist und der nächste Gang seines Platzes beginnt (der Kern lässt ihn so
    /// lange am Hafen, <c>Einwohnerschaft.HafenAbloesen</c>).
    /// </summary>
    private void HafenOrdnen(Einwohnerschaft ew, double minute)
    {
        if (_hafenOrdnungBild == _figurenBild) return;
        _hafenOrdnungBild = _figurenBild;
        _hafenPlaetze.Clear();
        _hafenVorarbeiter = -1;
        if (HafenPlanUm(minute) is not { } plan) return;
        var jetzt = (long)Math.Floor(minute);
        bool Da(Einwohner e) => e.Wo(jetzt) is { Ort: Stadtort.Hafen };
        var liste = ew.Hafenarbeit.Traeger;
        var stundenbeginn = jetzt - jetzt % Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag % 60; // die Stunden beginnen um 04:00, 05:00 …
        // Abgelöste: Ort nicht mehr Hafen, aber noch dort (ihr Weg beginnt am Hafen, sie sind noch nicht los)
        var abgeloest = ew.Einwohner.Where(e => e.Ort != Stadtort.Hafen && e.Weg is { Von.Ort: Stadtort.Hafen } w && w.Von.Stelle == Hafenarbeit.StelleTraeger && Da(e))
                                    .OrderBy(e => e.Nummer).ToList();
        var naechster = 0;
        for (var i = 0; i < liste.Count && i < plan.Schicht.Traeger; i++)
        {
            var neu = liste[i];
            var traeger = neu;
            if (neu.Weg is { } w && w.Von.Ort != Stadtort.Hafen && w.Abmarsch >= stundenbeginn && naechster < abgeloest.Count)
            {
                var alt = abgeloest[naechster++];
                // Wechsel beim ersten Gang des Platzes, der nach der Ankunft des Neuen beginnt
                var wechsel = plan.Schicht.Gaenge.Where(g => g.Platz == i && GangBeginn(plan.Schicht, g) >= w.Ankunft + 2)
                                  .Select(g => GangBeginn(plan.Schicht, g)).DefaultIfEmpty(w.Ankunft).Min();
                traeger = Da(neu) && minute >= wechsel || !Da(alt) ? neu : alt;
            }
            if (Da(traeger)) _hafenPlaetze[traeger.Nummer] = i;
        }
        var va = ew.Hafenarbeit.Vorarbeiter;
        if (va is not null && Da(va)) _hafenVorarbeiter = va.Nummer;
        else if (ew.Einwohner.FirstOrDefault(e => e.Ort != Stadtort.Hafen && e.Weg is { Von.Ort: Stadtort.Hafen } w && w.Von.Stelle == Hafenarbeit.StelleVorarbeiter && Da(e)) is { } alterVa)
            _hafenVorarbeiter = alterVa.Nummer;
    }

    private static double GangBeginn(Hafenschicht s, Hafengang g) => s.Entladen ? g.VomStapel : g.ImSchuppen;

    // ── Figuren ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Das Stück, das ein Träger gerade auf der Schulter hat (Kopie eines Stücks des Stapels).</summary>
    private readonly Dictionary<int, (int Stueck, Node3D Knoten)> _hafenLasten = new();

    /// <summary>
    /// Jedes Bild für eine Figur, die laut Kern am Hafen ist (aus <see cref="PassantenAktualisieren"/>): Gang, Warten oder Aufsicht. Gibt false zurück,
    /// wenn es keinen Steg gibt (dann steht sie wie sonst am Verweilpunkt).
    /// </summary>
    private bool HafenarbeitStellen(Passant p, Einwohner e, Aufenthalt ort, double minute, double sekunden, double ticksJeSekunde)
    {
        if (HafenSteg() is not { } steg || p.Figur is not { } figur || _simulation?.Wirtschaft?.Einwohnerschaft is not { } ew) return false;
        HafenOrdnen(ew, minute);
        p.Stand = (ort, e.Weg);
        p.Knoten.Visible = true;
        (p.VersatzJetzt, p.Geschwindigkeit) = (Vector3.Zero, Vector3.Zero);
        var plan = HafenPlanUm(minute);
        var dminute = (float)(sekunden * ticksJeSekunde);
        var land = HafenLandpunkt() ?? p.Knoten.Position;
        var bein = figur.Beinfaktor;

        // Der Vorarbeiter: am Stegkopf, solange getragen wird, weist er an; danach zurück an den Kai
        if (e.Nummer == _hafenVorarbeiter)
        {
            LastAblegen(p, e.Nummer);
            var arbeit = plan is { } pv && minute >= pv.Schicht.Beginn - 10 && minute <= pv.Schicht.Ende;
            var ziel = arbeit ? StegPunkt(steg, VorarbeiterPlatz) : land;
            if (!HinGehen(p, figur, ziel, dminute, HafenGehen, ticksJeSekunde))
            {
                var blick = StegPunkt(steg, new Vector3(0, StegDeck, -9.6f)) - p.Knoten.Position;
                if (arbeit) p.Knoten.Rotation = new Vector3(0, Mathf.Atan2(blick.X, blick.Z), 0);
                BewegungSetzen(figur, arbeit ? "Reden" : "Stehen");
            }
            return true;
        }

        if (plan is null || !_hafenPlaetze.TryGetValue(e.Nummer, out var platz))
        {
            // Ohne Platz (zu früh, zu spät, abgelöst): zum Kai und dort warten
            LastAblegen(p, e.Nummer);
            if (!HinGehen(p, figur, land, dminute, HafenGehen, ticksJeSekunde)) BewegungSetzen(figur, "Stehen");
            return true;
        }

        var s = plan.Schicht;
        var spur = (platz - (s.Traeger - 1) / 2f) * 0.55f;
        Hafengang? laufend = null, kommend = null;
        foreach (var g in s.Gaenge)
        {
            if (g.Platz != platz) continue;
            var beginn = GangBeginn(s, g);
            if (minute >= beginn && minute < g.Zurueck) laufend = g;
            else if (beginn > minute && (kommend is null || beginn < GangBeginn(s, kommend.Value))) kommend = g;
        }
        if (laufend is { } gang)
        {
            var weg = HafenWeg(steg, plan.TraegerPlatz[gang.Stueck], spur);
            var u = (minute - GangBeginn(s, gang)) / s.Runde;
            var hin = u < 0.5;
            var anteil = (float)(hin ? u * 2 : (u - 0.5) * 2);
            // Entladen: hin = Stapel → Lager (mit Stück), zurück leer; Beladen: hin = Lager → Stapel (mit Stück), zurück leer
            var vomStapel = s.Entladen == hin;
            var (ortAufWeg, richtung) = AufWeg(weg, vomStapel ? anteil : 1 - anteil);
            if (!vomStapel) richtung = -richtung;
            p.Knoten.Position = ortAufWeg;
            if (richtung.LengthSquared() > 1e-6f) p.Knoten.Rotation = new Vector3(0, Mathf.Atan2(richtung.X, richtung.Z), 0);
            var tempo = Weglaenge(weg) / (s.Runde / 2f) * (float)ticksJeSekunde; // Meter je echter Sekunde
            p.Geschwindigkeit = richtung * tempo;
            if (hin) LastTragen(p, e.Nummer, figur, plan.TraegerPlatz[gang.Stueck]);
            else LastAblegen(p, e.Nummer);
            BewegungSetzen(figur, hin ? "Tragen" : tempo > EilenAb * bein ? "Eilen" : "Gehen", tempo / ((hin ? TragenTempo : tempo > EilenAb * bein ? EilenTempo : GehenTempo) * bein));
            return true;
        }
        LastAblegen(p, e.Nummer);
        // Zwischen den Gängen: zum Anfang des nächsten Gangs (Entladen an den Stapel, Beladen ans Lager) – so schnell, dass er rechtzeitig dort ist
        Vector3 warten;
        var tempoMin = HafenGehen;
        if (kommend is { } next)
        {
            var weg = HafenWeg(steg, plan.TraegerPlatz[next.Stueck], spur);
            warten = s.Entladen ? weg[0] : weg[^1];
            var bleibt = (float)(GangBeginn(s, next) - minute);
            if (bleibt > 0.05f) tempoMin = Mathf.Max(HafenGehen, p.Knoten.Position.DistanceTo(warten) / bleibt);
        }
        else warten = land;
        if (!HinGehen(p, figur, warten, dminute, tempoMin, ticksJeSekunde)) BewegungSetzen(figur, "Stehen");
        return true;
    }

    /// <summary>Ein Schritt zum Ziel (Meter je Spielminute <paramref name="tempo"/>); false, wenn sie schon dort steht.</summary>
    private bool HinGehen(Passant p, Figur figur, Vector3 ziel, float dminute, float tempo, double ticksJeSekunde)
    {
        var d = ziel - p.Knoten.Position;
        d.Y = 0;
        var abstand = d.Length();
        if (abstand < 0.05f)
        {
            p.Knoten.Position = ziel;
            return false;
        }
        var schritt = Mathf.Min(abstand, tempo * Mathf.Max(dminute, 0));
        var neu = p.Knoten.Position + d / abstand * schritt;
        p.Knoten.Position = new Vector3(neu.X, ziel.Y + (p.Knoten.Position.Y - ziel.Y) * (abstand - schritt) / abstand, neu.Z);
        p.Knoten.Rotation = new Vector3(0, Mathf.Atan2(d.X, d.Z), 0);
        var mps = tempo * (float)ticksJeSekunde;
        BewegungSetzen(figur, mps > EilenAb * figur.Beinfaktor ? "Eilen" : "Gehen", mps / ((mps > EilenAb * figur.Beinfaktor ? EilenTempo : GehenTempo) * figur.Beinfaktor));
        return true;
    }

    /// <summary>Setzt das Stück auf die Schulter (eine verkleinerte Kopie des Stücks vom Stapelplatz, höchstens 0,55 m groß).</summary>
    private void LastTragen(Passant p, int nummer, Figur figur, int platz)
    {
        if (_zierStapel.Count == 0) return;
        var k = Math.Clamp(platz, 0, _zierStapel.Count - 1);
        if (_hafenLasten.TryGetValue(nummer, out var da) && IsInstanceValid(da.Knoten))
        {
            if (da.Stueck == k) { da.Knoten.Visible = true; return; }
            da.Knoten.QueueFree();
        }
        var quelle = _zierStapel[k];
        if (!IsInstanceValid(quelle.Knoten) || quelle.Knoten.Duplicate() is not MeshInstance3D kopie) return;
        var groesse = (quelle.Knoten as MeshInstance3D)?.Mesh?.GetAabb().Size ?? Vector3.One;
        var mass = Mathf.Min(1f, 0.55f / Mathf.Max(0.01f, Mathf.Max(groesse.X, Mathf.Max(groesse.Y, groesse.Z))));
        kopie.Name = "Hafenlast";
        kopie.Visible = true;
        p.Knoten.AddChild(kopie);
        // auf der rechten Schulter, Mitte des Stücks über der Schulter
        kopie.Scale = Vector3.One * mass;
        kopie.Rotation = Vector3.Zero;
        kopie.Position = new Vector3(-0.16f, 1.42f * figur.Beinfaktor, 0) - quelle.Mitte * mass - new Vector3(0, quelle.NetzUnten * mass - 0.02f, 0);
        _hafenLasten[nummer] = (k, kopie);
    }

    private void LastAblegen(Passant p, int nummer)
    {
        if (_hafenLasten.TryGetValue(nummer, out var da) && IsInstanceValid(da.Knoten) && da.Knoten.Visible) da.Knoten.Visible = false;
    }

    /// <summary>Für Licht und Auskunft: Träger (1), Vorarbeiter (2) oder keins (0) – nach der Ordnung dieses Bildes.</summary>
    private int HafenRolle(Einwohner e) =>
        e.Nummer == _hafenVorarbeiter && _hafenOrdnungBild == _figurenBild ? 2
        : _hafenPlaetze.ContainsKey(e.Nummer) && _hafenOrdnungBild == _figurenBild ? 1
        : e.Wo(_simulation?.Zeit.VergangeneMinuten ?? 0) is { Ort: Stadtort.Hafen } a ? (a.Stelle == Hafenarbeit.StelleVorarbeiter ? 2 : 1) : 0;

    /// <summary>Die Zeile für die Auswahltafel: was er am Hafen tut.</summary>
    private string HafenTun(Einwohner e)
    {
        var minute = _simulation?.Zeit.VergangeneMinuten ?? 0;
        var entladen = HafenPlanUm(minute)?.Schicht.Entladen ?? Zierfahrplan.IstNieder((long)Math.Floor(minute / (double)Zierfahrplan.Fenster));
        return e.Stelle == Hafenarbeit.StelleVorarbeiter
            ? (entladen ? "beaufsichtigt das Entladen" : "beaufsichtigt das Beladen")
            : "arbeitet am Hafen";
    }

    // ── Kran und Stapel ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>Wie viele Stücke stehen auf dem Stapel, wenn es Hafenarbeit gibt (sonst null: dann gilt der Stapel ohne Träger, PR #332)?</summary>
    private int? HafenStapel(double minute) => HafenPlanUm(minute) is { } plan && _zierStapel.Count >= plan.Schicht.Stuecke
        ? plan.Schicht.StapelUm(minute) : null;

    private readonly List<ZierStueck> _kranLasten = new();

    /// <summary>Das Stück am Haken: eine eigene Kopie je Platz des Stapels (der Platz selbst kann schon wieder belegt sein).</summary>
    private ZierStueck? KranLast(int platz)
    {
        if (platz < 0 || platz >= _zierStapel.Count) return null;
        if (_kranLasten.Count != _zierStapel.Count || _kranLasten.Count > 0 && !IsInstanceValid(_kranLasten[0].Knoten))
        {
            foreach (var alt in _kranLasten) if (IsInstanceValid(alt.Knoten)) alt.Knoten.QueueFree();
            _kranLasten.Clear();
            foreach (var s in _zierStapel)
            {
                if (!IsInstanceValid(s.Knoten) || s.Knoten.Duplicate() is not Node3D kopie) { _kranLasten.Clear(); return null; }
                kopie.Name = "Kranlast";
                kopie.Visible = false;
                s.Knoten.GetParent().AddChild(kopie);
                _kranLasten.Add(new ZierStueck { Knoten = kopie, Unten = s.Unten, Dreh = s.Dreh, Hoehe = s.Hoehe, NetzUnten = s.NetzUnten, Mitte = s.Mitte });
            }
        }
        return _kranLasten[platz];
    }

    /// <summary>
    /// Liegephase mit Hafenarbeit: wie <see cref="KranLadung"/>, aber der Stapel ist Puffer. Der Kran setzt jedes Stück oben auf den Stapel (Platz aus dem
    /// Plan) bzw. hebt das oberste; welche Plätze belegt sind, sagt <see cref="Hafenschicht.StapelUm"/>. Das Stück am Haken ist eine eigene Kopie.
    /// Gibt zurück, wie viele Stücke an Bord sichtbar sind.
    /// </summary>
    private int KranLadungMitTraegern(SchiffAnsicht ansicht, double a, bool nieder, int voll, HafenPlan plan, double minute)
    {
        var steg = _zierStapel.Count > 0 && IsInstanceValid(_zierStapel[0].Knoten) ? _zierStapel[0].Knoten.GetParent() as Node3D : null;
        if (steg is null || _zierStapel.Count < voll || _kranHaken is null || !IsInstanceValid(_kranHaken)) return voll;
        var w = (ZierNiederEntladeBis - ZierEntladeAb) / voll;
        var wr = (ZierOberLadeBis - ZierOberLadeAb) / voll;
        var ende1 = ZierEntladeAb + voll * w;
        var ende2 = ZierOberLadeAb + voll * wr;
        (Vector3 Oben, float Dreh) Schiff(int i) => KranSchiffStueck(ansicht, steg, i);
        (Vector3 Oben, float Dreh) Platz(int k) => (_zierStapel[k].Oben, _zierStapel[k].Dreh);
        Vector3 Hoch(Vector3 o) => new(o.X, KranHochY, o.Z);
        Vector3 Tief(Vector3 o) => o + new Vector3(0, KranGehaenge, 0);
        int KranPlatz(int hub) => Math.Clamp(plan.KranPlatz[Math.Min(hub, plan.KranPlatz.Length - 1)], 0, voll - 1);

        var haken = KranRuheOrt();
        var gehalten = -1;
        float dreh0 = 0, dreh1 = 0, dreht = 0;
        var haengt = false;
        int anBord;
        if (nieder)
        {
            if (a < ZierEntladeAb) anBord = voll;
            else if (a < ende1)
            {
                var x = (a - ZierEntladeAb) / w;
                var h = Math.Min((int)x, voll - 1);
                var q = Schiff(voll - 1 - h);
                var z = Platz(KranPlatz(h));
                var start = h == 0 ? KranRuheOrt() : Tief(Platz(KranPlatz(h - 1)).Oben);
                haken = KranHub(start, q.Oben, z.Oben, x - h, out haengt, out dreht);
                (dreh0, dreh1) = (q.Dreh, z.Dreh);
                if (haengt) gehalten = KranPlatz(h);
                anBord = voll - (haengt ? h + 1 : h);
            }
            else
            {
                var letzt = Platz(KranPlatz(voll - 1)).Oben;
                haken = KranWeg(Tief(letzt), Hoch(letzt), (a - ende1) / 0.02).Lerp(KranRuheOrt(), Glatt((a - 0.93) / 0.06));
                anBord = 0;
            }
        }
        else if (a < ZierOberLadeAb) anBord = 0;
        else if (a < ende2)
        {
            var x = (a - ZierOberLadeAb) / wr;
            var j = Math.Min((int)x, voll - 1);
            var q = Platz(KranPlatz(j));
            var z = Schiff(j);
            var start = j == 0 ? KranRuheOrt() : Tief(Schiff(j - 1).Oben);
            haken = KranHub(start, q.Oben, z.Oben, x - j, out haengt, out dreht);
            (dreh0, dreh1) = (q.Dreh, z.Dreh);
            if (haengt) gehalten = KranPlatz(j);
            anBord = j;
        }
        else
        {
            var letzt = Schiff(voll - 1).Oben;
            haken = KranWeg(Tief(letzt), Hoch(letzt), (a - ende2) / 0.02).Lerp(KranRuheOrt(), Glatt((a - 0.93) / 0.06));
            anBord = voll;
        }

        // Stapel nach dem Plan; Stücke, die die alte Rechnung verschoben hatte, wieder an ihren Platz
        if (_kranGehalten >= 0 && _kranGehalten < _zierStapel.Count) StueckSetzen(_zierStapel[_kranGehalten], _zierStapel[_kranGehalten].Unten, _zierStapel[_kranGehalten].Dreh);
        _kranGehalten = -1;
        var stapel = plan.Schicht.StapelUm(minute);
        for (var k = 0; k < _zierStapel.Count; k++)
        {
            var sichtbar = k < stapel;
            if (_zierStapel[k].Knoten.Visible != sichtbar) _zierStapel[k].Knoten.Visible = sichtbar;
        }
        _zierStapelGezeigt = -1;
        var yaw = Mathf.LerpAngle(dreh0, dreh1, dreht);
        for (var k = 0; k < _kranLasten.Count; k++)
            if (k != gehalten && _kranLasten[k].Knoten.Visible) _kranLasten[k].Knoten.Visible = false;
        if (gehalten >= 0 && KranLast(gehalten) is { } last)
        {
            last.Knoten.Visible = true;
            StueckSetzen(last, haken - new Vector3(0, KranGehaenge + last.Hoehe, 0), yaw);
            _kranGehaenge!.Rotation = new Vector3(0, yaw, 0);
        }
        _kranGehaenge!.Visible = gehalten >= 0;
        KranHakenSetzen(haken, gehalten >= 0 ? yaw : 0);
        return anBord;
    }

    /// <summary>Kein Stück am Haken (außerhalb der Liegephase): alle Kopien am Haken aus.</summary>
    private void KranLastenAus()
    {
        foreach (var l in _kranLasten)
            if (IsInstanceValid(l.Knoten) && l.Knoten.Visible) l.Knoten.Visible = false;
    }
}
