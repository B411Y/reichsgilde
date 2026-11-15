using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// <b>Klappbrücke von Köln</b> (Modell <c>bruecke/klappbruecke.glb</c>, Entwurf 15.11.2026): Die Brücke über den Rhein ist keine Steinbrücke aus Wasserplan-Bausteinen mehr,
/// sondern die Doppelklappbrücke der Zwerge und Elben – in der Mitte der feste Mittelteil mit den zwei Torhäusern und den zwei Klappen, von jedem Ufer her
/// ein Ende (Rampe), Bogenfelder und Zwischenpfeiler (Aufteilung: <see cref="Klappbrueckenlage"/> im Kern, aus den Maßen des Modells).
/// <list type="bullet">
/// <item><b>Das Modell richtet sich nach dem Wasserplan</b>: Lage und Länge kommen aus den Uferkanten der Querung (<c>UferA</c>, <c>UferB</c>, <c>Deckhoehe</c>); Durchfahrt, Mittelteil,
/// Feld, Zwischenpfeiler, Rampe und Deckhöhe stehen als glTF-extras am Knoten <c>Anker_Durchfahrt</c> (Skript <c>tools/modelle/klappbruecke.py</c>) und werden hier gelesen.</item>
/// <item><b>Material</b>: <c>building_trim.gdshader</c> mit dem Trim-Sheet <c>texturen/bruecke/bruecke_trim_*</c> (Albedo, Normal, ORM) und der gebackenen Verdeckung auf UV2 (nur Pfeiler,
/// Torhäuser, Klappen, Bogenfelder, Enden); Pixel-Look, Nässe und Schnee wie bei den Häusern (<c>haus_baender = 0</c>: die Bänder des Blatts sind keine Haus-Bänder).</item>
/// <item><b>Animation</b>: Die Klappen öffnen und schließen mit den Animationen <c>Oeffnen</c> und <c>Schliessen</c> des Modells (6 s). Bis der Ablauf sie nach dem Kern ansteuert,
/// zeigt die Prüfhilfe <c>--klappbruecke=auf|zu|auto</c> den Zustand (docs/STARTOPTIONEN.md).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    private const string KlappbrueckeModell = "bruecke/klappbruecke";
    private const string KlappbrueckeTexturen = "res://assets/texturen/bruecke/";

    /// <summary>Startoption <c>--klappbruecke=auf|zu|auto</c> (Prüfhilfe): Klappen öffnen, geschlossen lassen oder abwechselnd öffnen und schließen.</summary>
    private static readonly string? KlappbrueckeWunsch = NeubauOption("--klappbruecke");

    /// <summary>Die Maße des Modells aus den glTF-extras von <c>Anker_Durchfahrt</c> (Meter).</summary>
    internal sealed record KlappbrueckeMasse(float Durchfahrt, float Mittelteil, float Deck, float Rampe, float UferTief, float Feld, float Zwischenpfeiler, float Breite, float LichteBreite, float SchrankeDauer);

    private KlappbrueckeMasse? _klappbrueckeMasse;
    private bool _klappbrueckeGeprueft;

    /// <summary>Die Knoten des Modells, aus denen die Seitenspannen und Enden kopiert werden: sie liegen im Modell abseits und bleiben dort unsichtbar.</summary>
    private static readonly string[] KlappbrueckeVorlagen = { "Bogenfeld", "Zwischenpfeiler", "Ende" };

    /// <summary>Teile mit UV2 und gebackener Verdeckung (die anderen bekommen eine weiße Verdeckung).</summary>
    private static bool KlappbrueckeMitVerdeckung(string knoten) =>
        knoten.StartsWith("Pfeiler_") || knoten.StartsWith("Torhaus_") || knoten.StartsWith("Klappe_") || knoten.StartsWith("Bogenfeld") || knoten.StartsWith("Zwischenpfeiler") || knoten.StartsWith("Ende");

    /// <summary>Ist die Querung die Klappbrücke? Der Wasserplan kennzeichnet sie mit einer Durchfahrt (<see cref="Wasserquerung.Durchfahrt"/> &gt; 0, nur Köln-Rhein); alle anderen Brücken bleiben wie sie sind.</summary>
    private bool IstKlappbruecke(Wasserquerung q) => q.IstBruecke && q.Durchfahrt > 0 && KlappbrueckeMasseLesen() is not null;

    private KlappbrueckeMasse? KlappbrueckeMasseLesen()
    {
        if (_klappbrueckeGeprueft) return _klappbrueckeMasse;
        _klappbrueckeGeprueft = true;
        if (Modell(KlappbrueckeModell) is not { } szene) return null;
        var probe = szene.Instantiate<Node3D>();
        try
        {
            if (probe.FindChild("Anker_Durchfahrt", true, false) is not { } anker || !anker.HasMeta("extras") || anker.GetMeta("extras").AsGodotDictionary() is not { } extras)
            {
                GD.PushError("Klappbrücke: Anker_Durchfahrt ohne extras (Maße des Modells fehlen)");
                return null;
            }
            float Lesen(string name)
            {
                if (extras.ContainsKey(name)) return (float)extras[name].AsDouble();
                GD.PushError($"Klappbrücke: Maß „{name}“ fehlt in den extras des Modells");
                return float.NaN;
            }
            var m = new KlappbrueckeMasse(Lesen("durchfahrt"), Lesen("mittelteil"), Lesen("deck"), Lesen("rampe"), Lesen("ufer_tief"), Lesen("feld"), Lesen("zwischenpfeiler"), Lesen("breite"), Lesen("lichte_breite"), Lesen("schranke_dauer"));
            return _klappbrueckeMasse = float.IsNaN(m.Mittelteil + m.Deck + m.Rampe + m.UferTief + m.Feld + m.Zwischenpfeiler + m.Breite + m.SchrankeDauer) ? null : m;
        }
        finally { probe.QueueFree(); }
    }

    private ShaderMaterial KlappbrueckeStoff(bool mitVerdeckung)
    {
        var schluessel = mitVerdeckung ? "bruecke/ao" : "bruecke/frei";
        if (_neubauStoffe.TryGetValue(schluessel, out var vorhanden)) return vorhanden;
        _neubauShader ??= GD.Load<Shader>("res://assets/shader/building_trim.gdshader");
        var stoff = new ShaderMaterial { Shader = _neubauShader, ResourceName = "Trim Klappbruecke" + (mitVerdeckung ? "" : " ohne Verdeckung") };
        stoff.SetShaderParameter("albedo_tex", GD.Load<TextureLayered>(KlappbrueckeTexturen + "bruecke_trim_albedo.png"));
        stoff.SetShaderParameter("normal_tex", GD.Load<TextureLayered>(KlappbrueckeTexturen + "bruecke_trim_normal.png"));
        stoff.SetShaderParameter("orm_tex", GD.Load<TextureLayered>(KlappbrueckeTexturen + "bruecke_trim_orm.png"));
        if (mitVerdeckung) stoff.SetShaderParameter("ao_uv2_tex", GD.Load<TextureLayered>(KlappbrueckeTexturen + "klappbruecke_ao_atlas.png"));
        else
        {
            var weiss = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            weiss.Fill(Colors.White);
            var leer = new Texture2DArray();
            leer.CreateFromImages(new Godot.Collections.Array<Image> { weiss });
            stoff.SetShaderParameter("ao_uv2_tex", leer);
        }
        stoff.SetShaderParameter("wetter_an", 1f);
        stoff.SetShaderParameter("haus_baender", 0f);
        // Eigenes Array mit einer Ebene (Blatt der Brücke): keine Steinebenen der Häuser, kein Stein-Band (Pixel-Mip überall wie bisher)
        stoff.SetShaderParameter("stein_je_ebene", false);
        stoff.SetShaderParameter("stein_baender", new Vector4(-1f, -1f, -1f, -1f));
        stoff.SetShaderParameter("pixel_look", Fenster.PixelLookAn ? 1f : 0f);
        return _neubauStoffe[schluessel] = stoff;
    }

    /// <summary>Tauscht den Stoff „Bruecke“ (das ganze Trim-Sheet in einem Stoff) gegen das Trim-Material (mit oder ohne Verdeckung nach dem Knoten); „Fenster“ bleibt (Nachtlicht).</summary>
    private void KlappbrueckeEinkleiden(Node3D wurzel)
    {
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } mesh) continue;
            var stoff = KlappbrueckeStoff(KlappbrueckeMitVerdeckung(teil.Name.ToString()));
            for (var i = 0; i < mesh.GetSurfaceCount(); i++)
                if (mesh.SurfaceGetMaterial(i)?.ResourceName is "Bruecke") teil.SetSurfaceOverrideMaterial(i, stoff);
        }
    }

    /// <summary>
    /// Baut die Klappbrücke an Stelle der Steinbrücke: Mittelteil in der Mitte zwischen den Uferkanten, von jedem Ufer Ende, Bogenfelder und Zwischenpfeiler nach
    /// <see cref="Klappbrueckenlage"/>. False, wenn die Querung keine Klappbrücke ist (dann baut <see cref="BrueckeBauen"/> wie bisher).
    /// </summary>
    private bool KlappbrueckeBauen(Node3D eltern, Wasserquerung q)
    {
        if (!IstKlappbruecke(q) || _klappbrueckeMasse is not { } m) return false;
        var (a, b) = (V2(q.UferA), V2(q.UferB));
        var weite = a.DistanceTo(b);
        var d0 = (b - a) / weite;
        // die Pfeiler kommen aus dem Wasserplan (Kern), nicht aus eigener Rechnung: Stellen ab Uferkante A; die beiden mittleren sind die Torhauspfeiler
        var stellen = q.Pfeiler.Select(p => (double)(V2(p.Ort) - a).Dot(d0)).OrderBy(u => u).ToList();
        var torhausBreite = m.Mittelteil / 2 - (float)q.Durchfahrt / 2;           // Torhaus je Seite: (Mittelteil − Durchfahrt) / 2 = 4 m
        if (Klappbrueckenlage.Aus(weite, stellen, q.Durchfahrt, torhausBreite, m.Zwischenpfeiler) is not { } lage)
        {
            GD.PushError($"Klappbrücke: Pfeiler des Wasserplans passen nicht zum Modell (Durchfahrt {q.Durchfahrt:0.##} m, {stellen.Count} Pfeiler): alte Brücke");
            return false;
        }
        var gruppe = new Node3D { Name = "Klappbruecke_" + q.GewaesserName };
        eltern.AddChild(gruppe);
        if (ModellEinsetzen(KlappbrueckeModell, gruppe, Vector3.Zero, new ModellZustand()) is not { } instanz) { gruppe.QueueFree(); return false; }
        var wurzel = instanz.Wurzel;
        var d = d0;
        var phi = Mathf.Atan2(-d.Y, d.X);                         // Modell-X liegt längs der Brücke, Torhaus_L am Anfang (UferA)
        var deck = (float)q.Deckhoehe;
        var wasserlinie = deck - m.Deck;                           // das Modell sitzt auf seiner Wasserlinie: die Fahrbahn trifft Deckhöhe des Wasserplans
        wurzel.Transform = BausteinLage((a + b) / 2, wasserlinie, phi);
        KlappbrueckeEinkleiden(wurzel);

        // Seitenspannen und Enden aus den Vorlagen des Modells
        var vorlagen = KlappbrueckeVorlagen.ToDictionary(n => n, n => wurzel.FindChild(n, true, false) as Node3D);
        void Kopie(string vorlage, Vector2 ort, float drehung, Vector3? streckung = null)
        {
            if (vorlagen[vorlage] is not { } v) return;
            var kopie = (Node3D)v.Duplicate();
            gruppe.AddChild(kopie);
            kopie.Visible = true;
            kopie.Transform = BausteinLage(ort, wasserlinie, drehung, streckung);
        }
        // Rampe je Ufer aus dem Wasserplan (Anfang/Ende der Brücke auf dem Land), nicht aus dem Modell: das Ende streckt sich auf diese Länge (Modell-Rampe m.Rampe)
        var rampeA = (float)(q.UferAStelle - q.VonStelle) is > 0.5f and var ra ? ra : m.Rampe;
        var rampeB = (float)(q.BisStelle - q.UferBStelle) is > 0.5f and var rb ? rb : m.Rampe;
        Kopie("Ende", a, phi, new Vector3(rampeA / m.Rampe, 1, 1));  // Ursprung an der Uferkante, Rampe nach außen (Modell-x < 0)
        Kopie("Ende", b, phi + Mathf.Pi, new Vector3(rampeB / m.Rampe, 1, 1));
        foreach (var (u, laenge) in lage.Felder) Kopie("Bogenfeld", a + d * (float)u, phi, new Vector3((float)laenge / m.Feld, 1, 1));
        foreach (var u in lage.Zwischenpfeiler) Kopie("Zwischenpfeiler", a + d * (float)u, phi);
        foreach (var v in vorlagen.Values) if (v is not null) v.Visible = false;

        // Eisschollen und Strömungsfeld: genau die Pfeiler des Wasserplans (Torhauspfeiler 4 m breit samt Eisbrecher, Zwischenpfeiler 2 m)
        foreach (var (p, nr) in q.Pfeiler.Select((p, nr) => (p, nr)))
        {
            var tor = Math.Abs((V2(p.Ort) - (a + b) / 2).Dot(d)) < q.Durchfahrt / 2 + torhausBreite;
            _wasserPfeiler.Add((V2(p.Ort), (tor ? torhausBreite : m.Zwischenpfeiler) / 2 + 0.4f, tor ? 9.2f : 5.6f, true));
        }

        // Fahrfläche: von der Rampe am ersten bis zur Rampe am zweiten Ufer; die Rampe steigt von der Uferhöhe (Fahrbahn − UferTief) auf die Fahrbahn
        var ufer = deck - m.UferTief;
        var halb = m.LichteBreite / 2 + 0.2f;
        var gesamt = weite + rampeA + rampeB;
        float? Hoehe(float u) => u < rampeA ? Mathf.Lerp(ufer, deck, Mathf.Clamp(u / rampeA, 0, 1)) : u > weite + rampeA ? Mathf.Lerp(ufer, deck, Mathf.Clamp((gesamt - u) / rampeB, 0, 1)) : deck;
        FahrflaecheMerken(a - d * rampeA, d, gesamt, halb, Hoehe);

        // Schranken (Anker im Modell): dort halten Karren, wenn die Brücke offen ist
        _klappbrueckeSchranken = new[] { "Anker_Schranke_L", "Anker_Schranke_R" }
            .Select(n => wurzel.FindChild(n, true, false) as Node3D).Where(n => n is not null)
            .Select(n => new Vector2(n!.GlobalPosition.X, n.GlobalPosition.Z)).ToArray();
        _klappbrueckeMitte = (a + b) / 2;
        _klappbrueckeAchse = d;
        _klappbrueckeBaeume = new[] { "Schranke_L", "Schranke_R" }.Select(n => wurzel.FindChild(n, true, false) as Node3D).Where(n => n is not null).Select(n => (n!, n!.Rotation.X)).ToList();   // in der Datei offen (oben), zu = 0
        KlappbrueckeAnimieren(wurzel);
        return true;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Klappen: Zustand und Animation
    // ════════════════════════════════════════════════════════════════════════

    private AnimationPlayer? _klappbrueckeAnimation;

    /// <summary>Ist die Brücke offen (Klappen oben)? Bis der Ablauf sie nach dem Kern führt, setzt nur die Prüfhilfe den Zustand.</summary>
    internal bool KlappbrueckeOffen { get; private set; }

    private void KlappbrueckeAnimieren(Node3D wurzel)
    {
        _klappbrueckeAnimation = wurzel.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        var wunsch = KlappbrueckeWunsch;
        if (_klappbrueckeAnimation is null)
        {
            GD.PushError("Klappbrücke: Das Modell hat keinen AnimationPlayer (Oeffnen/Schliessen fehlen)");
            return;
        }
        foreach (var name in new[] { "Oeffnen", "Schliessen" })
            if (!_klappbrueckeAnimation.HasAnimation(name)) GD.PushError($"Klappbrücke: Animation „{name}“ fehlt im Modell");
        GD.Print($"Klappbrücke: gebaut, Animationen {string.Join(", ", _klappbrueckeAnimation.GetAnimationList())}");
        switch (wunsch)
        {
            case "auf":
                KlappbrueckeSetzen(true);
                break;
            case "auto":
                KlappbrueckeSetzen(true);
                _klappbrueckeAnimation.AnimationFinished += name =>
                {
                    // nach jeder Fahrt drei Sekunden stehen, dann die Gegenrichtung (Prüfhilfe; der Ablauf ersetzt das)
                    GetTree().CreateTimer(3).Timeout += () => { if (IsInstanceValid(_klappbrueckeAnimation)) KlappbrueckeSetzen(!KlappbrueckeOffen); };
                };
                break;
            default:
                GD.Print("Klappbrücke: zu");
                break;
        }
    }

    /// <summary>Fährt die Klappen auf oder zu (Animation des Modells) und meldet den Zustand im Log.</summary>
    internal void KlappbrueckeSetzen(bool offen)
    {
        if (_klappbrueckeAnimation is null || !IsInstanceValid(_klappbrueckeAnimation)) return;
        KlappbrueckeOffen = offen;
        _klappbrueckeAnimation.Play(offen ? "Oeffnen" : "Schliessen");
        KlappbrueckeSchrankeSetzen(offen ? 1f : 0f);
        GD.Print($"Klappbrücke: {(offen ? "auf" : "zu")}");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Ablauf: die Klappen folgen dem Kern, Karren halten an der Schranke
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Dauer der Animationen <c>Oeffnen</c> und <c>Schliessen</c> in echten Sekunden (6 s im Modell).</summary>
    private const float KlappbrueckeFahrtSekunden = 6f;

    private List<(Node3D Baum, float Auf)> _klappbrueckeBaeume = new();
    private Vector2 _klappbrueckeMitte;
    private Vector2 _klappbrueckeAchse;

    /// <summary>Wie weit die Schrankenbäume zu sind (0 oben, 1 quer über der Fahrbahn); folgt dem Kern (<see cref="Klappbrueckenlage.Schrankenstand"/>), nur Anzeige.</summary>
    internal float KlappbrueckeSchranke { get; private set; }

    internal bool KlappbrueckeSchrankeZu => KlappbrueckeSchranke > 0.5f;

    private void KlappbrueckeSchrankeSetzen(float stand)
    {
        if ((stand > 0.5f) != KlappbrueckeSchrankeZu) GD.Print($"Klappbrücke: Schranke {(stand > 0.5f ? "zu" : "auf")}");
        KlappbrueckeSchranke = stand;
        foreach (var (baum, auf) in _klappbrueckeBaeume)
            if (IsInstanceValid(baum)) baum.Rotation = new Vector3(Mathf.Lerp(auf, 0f, stand), baum.Rotation.Y, baum.Rotation.Z);
    }

    /// <summary>
    /// Passanten warten nur zum Anschauen: Wer von außen auf die Brücke zugeht, während die Schranke zu ist, bleibt eine Wegelänge vor dem Baum stehen, solange sie zu ist; danach steht er
    /// wieder an seinem Platz auf dem Plan (die Anzeige verändert weder Plan noch Ankunftszeit des Kerns, kein Befehl). Wer schon zwischen den Bäumen war, geht weiter.
    /// Gibt zurück, ob die Figur steht.
    /// </summary>
    private bool KlappbrueckePassantHalt(Passant p, ref Vector3 hier)
    {
        if (_klappbrueckeSchranken.Length < 2) return false;
        var rel = new Vector2(hier.X, hier.Z) - _klappbrueckeMitte;
        var (u, v) = (rel.Dot(_klappbrueckeAchse), rel.Dot(new Vector2(-_klappbrueckeAchse.Y, _klappbrueckeAchse.X)));
        var halbU = (_klappbrueckeSchranken[0] - _klappbrueckeMitte).Dot(_klappbrueckeAchse) is var u0 && (_klappbrueckeSchranken[1] - _klappbrueckeMitte).Dot(_klappbrueckeAchse) is var u1 ? Math.Min(Math.Abs(u0), Math.Abs(u1)) : 0f;
        if (Math.Abs(v) > 4f || Math.Abs(u) >= halbU) { p.KlappbrueckeSeite = Math.Abs(v) <= 4f && Math.Abs(u) < halbU + 12f ? Math.Sign(u) : 0; return false; }   // draußen: Seite merken (nur in der Nähe)
        if (!KlappbrueckeSchrankeZu || p.KlappbrueckeSeite == 0) return false;
        var halt = _klappbrueckeMitte + _klappbrueckeAchse * (p.KlappbrueckeSeite * (halbU + 1.0f)) + new Vector2(-_klappbrueckeAchse.Y, _klappbrueckeAchse.X) * v;
        hier = new Vector3(halt.X, hier.Y, halt.Y);
        return true;
    }

    private readonly HashSet<Karren> _klappbrueckeWartende = new();

    /// <summary>Meldet im Log, wenn ein Karren vor der Schranke zu stehen beginnt oder weiterfährt (nur beim Wechsel).</summary>
    private void KlappbrueckeWartenMelden(Karren karren, bool steht, float? halt)
    {
        if (steht && _klappbrueckeWartende.Add(karren))
            GD.Print($"Klappbrücke: Karren {karren.Heimat.Name} {karren.Name} wartet vor der Schranke (Meter {halt:0.0} auf dem Weg), bis Minute {karren.BrueckeWartenBis}");
        else if (!steht && _klappbrueckeWartende.Remove(karren))
            GD.Print($"Klappbrücke: Karren {karren.Heimat.Name} {karren.Name} fährt weiter");
    }

    private Vector2[] _klappbrueckeSchranken = Array.Empty<Vector2>();
    private bool _klappbrueckeNaechsteGemeldet;

    /// <summary>Wie weit die Klappen offen sind (0 zu, 1 ganz auf); aus dem Kern (<see cref="Reichsgilde.Core.Wirtschaft.Klappbruecke.Oeffnung"/>) und der Echtzeit der Fahrt.</summary>
    internal float KlappbrueckeOffenheit { get; private set; }

    /// <summary>Jedes Bild: Klappen nach der Öffnung des Kerns stellen (Animation auf die Stelle springen lassen, so gilt sie auch bei Pause, Spielstand und Tempo).</summary>
    private void KlappbrueckeAblauf(Stadtwirtschaft w, double minute)
    {
        if (_klappbrueckeAnimation is null || !IsInstanceValid(_klappbrueckeAnimation) || KlappbrueckeWunsch is "auf" or "zu" or "auto") return;   // die Prüfhilfe führt die Klappen selbst
        var kern = w.Klappbruecke;
        if (!_klappbrueckeNaechsteGemeldet)
        {
            _klappbrueckeNaechsteGemeldet = true;
            GD.Print(kern.NaechsteOeffnung((long)Math.Floor(minute)) is { } n ? $"Klappbrücke: nächste Öffnung Minute {n.Von} bis {n.Bis} (jetzt {minute:0})" : $"Klappbrücke: keine Öffnung in Sicht (jetzt {minute:0}, Gibt: {kern.Gibt})");
        }
        var tps = Math.Max(0.001, _simulation?.Takt.TicksProSekunde ?? 1);
        var fahrtMinuten = KlappbrueckeFahrtSekunden * tps;
        var offenheit = kern.Oeffnung((long)Math.Floor(minute)) is { } o ? Klappbrueckenlage.Offenheit(minute, o.Von, o.Bis, fahrtMinuten) : 0f;
        // Aufwärts (Öffnen) oder abwärts (Schließen)? Beim Anfahren gilt die Hälfte der Öffnung, die der Minute näher am Anfang liegt.
        var oeffnet = kern.Oeffnung((long)Math.Floor(minute)) is { } p && (minute - p.Von) < (p.Bis - minute);
        var name = oeffnet ? "Oeffnen" : "Schliessen";
        var position = (oeffnet ? offenheit : 1f - offenheit) * KlappbrueckeFahrtSekunden;
        if (!oeffnet && offenheit == 0f) { name = "Oeffnen"; position = 0f; }   // zu: Ausgangslage der Animation
        _klappbrueckeAnimation.SpeedScale = 0f;
        if (_klappbrueckeAnimation.CurrentAnimation != name) _klappbrueckeAnimation.Play(name);
        _klappbrueckeAnimation.Seek(position, true);
        // Schrankenbäume (rein Anzeige): zu, bevor die Klappen aufgehen, bis sie wieder zu sind
        var schrankeFahrt = (_klappbrueckeMasse?.SchrankeDauer ?? 2f) * tps;
        var stand = 0f;
        foreach (var f in new[] { kern.Oeffnung((long)Math.Floor(minute - schrankeFahrt) - 1), kern.Oeffnung((long)Math.Floor(minute)), kern.NaechsteOeffnung((long)Math.Floor(minute)) })
            if (f is { } fo) stand = Math.Max(stand, Klappbrueckenlage.Schrankenstand(minute, fo.Von, fo.Bis, schrankeFahrt));
        KlappbrueckeSchrankeSetzen(stand);
        if ((offenheit > 0.5f) != KlappbrueckeOffen) GD.Print($"Klappbrücke: {(offenheit > 0.5f ? "auf" : "zu")} (Minute {minute:0})");
        KlappbrueckeOffen = offenheit > 0.5f;
        KlappbrueckeOffenheit = offenheit;
    }

    /// <summary>
    /// Wo (Meter auf dem Karrenweg <paramref name="weg"/>) der Karren vor der Schranke hält: In Fahrtrichtung zum Markt (Meter wachsen) an der ersten, auf der Rückfahrt an der zweiten Schranke,
    /// mit der Spitze des Gespanns an der Schranke. Null, wenn der Weg nicht über die Brücke führt.
    /// </summary>
    private float? KlappbrueckeHaltMeter(Vector3[] weg, bool zumMarkt, string typ)
    {
        if (_klappbrueckeSchranken.Length < 2 || weg.Length < 2) return null;
        var meter = new List<float>();
        foreach (var schranke in _klappbrueckeSchranken)
        {
            var (bestes, abstand, laufend) = (float.MaxValue, float.MaxValue, 0f);
            for (var i = 0; i + 1 < weg.Length; i++)
            {
                var (a, b) = (new Vector2(weg[i].X, weg[i].Z), new Vector2(weg[i + 1].X, weg[i + 1].Z));
                var laenge = a.DistanceTo(b);
                var t = laenge > 0 ? Mathf.Clamp((schranke - a).Dot((b - a) / laenge), 0, laenge) : 0;
                var d = (a + (laenge > 0 ? (b - a) / laenge * t : Vector2.Zero)).DistanceTo(schranke);
                if (d < abstand) (abstand, bestes) = (d, laufend + t);
                laufend += laenge;
            }
            if (abstand > 3f) return null;                         // der Weg führt nicht an dieser Schranke vorbei
            meter.Add(bestes);
        }
        var vorn = KarrenGespann(typ).Vorn;
        return zumMarkt ? meter.Min() - vorn - 0.5f : meter.Max() + vorn + 0.5f;
    }
}
