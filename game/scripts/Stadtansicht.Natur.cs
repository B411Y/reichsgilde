using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Natur vor der Stadt – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Wald und Streu kommen aus den Natur-Flächen der Karte (<see cref="NaturAusKarte"/>); jede Art ist ein Baustein des Katalogs, ein Knoten in einem
/// der Baukästen <c>natur_baeume</c>, <c>natur_buesche</c>, <c>natur_boden</c>, <c>natur_steine</c> und <c>natur_holz</c> (je Art und Variante ein
/// Knoten, der Fuß im Nullpunkt; Konventionen in <c>docs/bereiche/darstellung.md</c>). Alles steht fest aus dem Startwert der Fläche (gleich bei
/// jedem Start), jede Art ist ein <see cref="MultiMeshInstance3D"/>: Hunderte Teile kosten nur ein paar Zeichenaufrufe.
/// Dazu stellt <see cref="JahreszeitSetzen"/> die Jahreszeit ein: Das Laub aller Modelle färbt sich um (Material „Laub“, „Laub hell“, „Laub dunkel“),
/// im Winter zeigen Bäume und Büsche ihre kahlen „…_Winter“-Fassungen bzw. „Winteraeste_…“ statt der Laubkrone.
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Sammeln und aufbauen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Netze der Baukästen, je Datei und Knoten einmal geholt.</summary>
    private readonly Dictionary<(string Datei, string Knoten), Mesh?> _naturNetze = new();

    /// <summary>Wo welche Art steht: Lage (Ort, Drehung, Größe) aller Kopien, bis <see cref="NaturAufbauen"/> sie in je ein MultiMesh packt.</summary>
    private readonly Dictionary<(string Datei, string Knoten), List<Transform3D>> _naturLagen = new();

    /// <summary>Die MultiMeshes der Arten mit kahler Fassung: das Netz im Sommer und das im Winter (<see cref="JahreszeitSetzen"/> tauscht).</summary>
    private readonly List<(MultiMesh Vielfach, Mesh Sommer, Mesh Winter)> _naturWinter = new();

    private Mesh? NaturNetz(string datei, string knoten, bool pflicht = true)
    {
        if (_naturNetze.TryGetValue((datei, knoten), out var netz)) return netz;
        if (Modell(datei)?.Instantiate<Node3D>() is { } kasten)
        {
            netz = (kasten.FindChild(knoten, true, false) as MeshInstance3D)?.Mesh;
            kasten.Free();
            if (netz is null) { if (pflicht) GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb hat keinen Knoten „{knoten}“."); }
            else LaubMelden(netz);
        }
        return _naturNetze[(datei, knoten)] = netz;
    }

    /// <summary>Merkt eine Kopie der Art vor: Drehung um die Hochachse und Größe (ein Faktor für alle Richtungen).</summary>
    private void NaturSetzen(string datei, string knoten, Vector3 ort, float drehung, float groesse)
    {
        if (!_naturLagen.TryGetValue((datei, knoten), out var lagen)) _naturLagen[(datei, knoten)] = lagen = new();
        lagen.Add(new Transform3D(new Basis(Vector3.Up, drehung).Scaled(Vector3.One * groesse), ort));
    }

    /// <summary>
    /// Packt alles Vorgemerkte in je ein MultiMesh je Art. Bodenpflanzen werfen keinen Schatten (zu klein, die Karte der Sonne wäre vergeudet).
    /// </summary>
    private void NaturAufbauen()
    {
        var natur = new Node3D { Name = "Natur" };
        AddChild(natur);
        foreach (var ((datei, knoten), lagen) in _naturLagen)
        {
            if (NaturNetz(datei, knoten) is not { } netz) continue;
            // Gemischt (immer gleich): Die Einstellung „Natur-Dichte“ zeigt nur die ersten Kopien (Stadtansicht.Grafik.cs), und die sollen
            // über alle Flächen verteilt sein, nicht die erste Fläche ganz und die letzte gar nicht.
            var mischer = new Random(lagen.Count * 7919 + knoten.Length);
            for (var i = lagen.Count - 1; i > 0; i--)
            {
                var j = mischer.Next(i + 1);
                (lagen[i], lagen[j]) = (lagen[j], lagen[i]);
            }
            var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = lagen.Count };
            for (var i = 0; i < lagen.Count; i++) vielfach.SetInstanceTransform(i, lagen[i]);
            // Bäume und Büsche haben eine kahle Fassung „<Name>_Winter“ (Tanne, Ginster und Wacholder nicht): im Winter tauscht JahreszeitSetzen das Netz
            if ((datei is "natur/natur_baeume" or "natur/natur_buesche") && NaturNetz(datei, knoten + "_Winter", pflicht: false) is { } winter)
            {
                _naturWinter.Add((vielfach, netz, winter));
                if (_jahreszeit == Jahreszeit.Winter) vielfach.Mesh = winter;
            }
            var teil = new MultiMeshInstance3D
            {
                Name = knoten,
                Multimesh = vielfach,
                CastShadow = datei == "natur/natur_boden" ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            };
            natur.AddChild(teil);
            NaturDetailMelden(teil, datei); // Natur-Dichte und Sichtweite der Feinheiten (Stadtansicht.Grafik.cs)
        }
        _naturLagen.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Natur aus der Karte
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Natur der Karte (<c>natur</c>): Jede Fläche bekommt so viele Streupunkte, wie ihre Dichte (je 1.000 m² des Teils, der in keiner Aussparung liegt) sagt,
    /// gleich verteilt aus ihrem Startwert – gleiche Karte, gleiches Bild. An jedem Punkt wählt der Zufall eine Gruppe nach Gewicht; jeder ihrer
    /// Teile setzt seine Anzahl Kopien einer Art (nach Gewicht) in eine Scheibe mit dem Radius der Gruppe, mit eigener Variante, Drehung und Größe.
    /// Auf Wegen, Gebäuden, Bauzonen, in Aussparungen und in der Stadt wächst nichts (<see cref="NaturFrei"/>). Dazu die einzeln gesetzten Pflanzen.
    /// </summary>
    private void NaturAusKarte(Stadtkarte karte)
    {
        foreach (var flaeche in karte.Natur.Flaechen)
            NaturFlaeche(flaeche);
        foreach (var pflanze in karte.Natur.Pflanzen)
        {
            if (Katalog?.Finde(pflanze.Katalog) is not { Modell: { } datei, Knoten: { } knoten } art) continue;
            var ort = Punkt3(pflanze.Punkt);
            var variante = pflanze.Variante > 0 ? Math.Min(pflanze.Variante, art.Varianten)
                : (int)(Streuung((int)(ort.X * 100), (long)(ort.Z * 100)) % (uint)art.Varianten) + 1;
            NaturSetzen(datei, knoten + variante, ort, Mathf.DegToRad((float)pflanze.Drehung), (float)pflanze.Massstab);
        }
        NaturAufbauen();
    }

    private void NaturFlaeche(Naturflaeche flaeche)
    {
        var aussen = flaeche.Flaeche.Where(p => p is { Length: 2 }).Select(Punkt2).ToArray();
        if (aussen.Length < 3 || flaeche.Gruppen.Count == 0 || Katalog is not { } katalog) return;
        var loecher = flaeche.Aussparungen.Select(l => l.Where(p => p is { Length: 2 }).Select(Punkt2).ToArray()).Where(l => l.Length >= 3).ToList();
        var rahmen = new Rect2(aussen[0], Vector2.Zero);
        foreach (var p in aussen) rahmen = rahmen.Expand(p);
        bool InFlaeche(Vector2 p) => Geometry2D.IsPointInPolygon(p, aussen) && !loecher.Any(l => Geometry2D.IsPointInPolygon(p, l));
        var anzahl = (int)Math.Round(flaeche.Dichte * Inhalt(rahmen, InFlaeche) / 1000);

        var zufall = new Random(flaeche.Startwert);
        for (var n = 0; n < anzahl; n++)
        {
            // Streupunkt: gleich verteilt in der Fläche (Würfeln im umschließenden Rechteck, bis er drin liegt)
            var punkt = rahmen.GetCenter();
            for (var versuch = 0; versuch < 200; versuch++)
            {
                punkt = rahmen.Position + new Vector2((float)zufall.NextDouble(), (float)zufall.NextDouble()) * rahmen.Size;
                if (InFlaeche(punkt)) break;
            }
            var gruppe = NachGewicht(flaeche.Gruppen, g => g.Gewicht, zufall);
            var mitte = new Vector3(punkt.X, 0, punkt.Y);
            var frei = NaturFrei(mitte); // der Zufall läuft auch für verworfene Punkte gleich weiter: Eine neue Straße verschiebt keine anderen Bäume
            foreach (var teil in gruppe.Teile)
            {
                var (von, bis) = teil.Anzahl is { Length: 2 } a ? (a[0], Math.Max(a[0], a[1])) : (1, 1);
                var kopien = von + zufall.Next(bis - von + 1);
                for (var i = 0; i < kopien; i++)
                {
                    var winkel = zufall.NextDouble() * Math.Tau;
                    var abstand = Math.Sqrt(zufall.NextDouble()) * gruppe.Radius;
                    var p = mitte + new Vector3((float)(Math.Cos(winkel) * abstand), 0, (float)(Math.Sin(winkel) * abstand));
                    var art = katalog.Finde(NachGewicht(teil.Arten.ToList(), x => x.Value, zufall).Key);
                    var variante = zufall.Next(Math.Max(1, art?.Varianten ?? 1)) + 1;
                    var drehung = (float)(zufall.NextDouble() * Math.Tau);
                    var groesse = 0.85f + (float)zufall.NextDouble() * 0.3f;
                    if (!frei || art is not { Modell: { } datei, Knoten: { } knoten } || !InFlaeche(new Vector2(p.X, p.Z)) || !NaturFrei(p)) continue;
                    NaturSetzen(datei, knoten + variante, p, drehung, groesse);
                }
            }
        }
    }

    /// <summary>
    /// Wie groß der Teil des Rechtecks ist, der „drin“ liegt (m²) – gezählt auf einem Raster von etwa 20.000 Punkten. So zählen Aussparungen, die
    /// sich überdecken oder über den Rand der Fläche hinausragen, richtig: drin ist, was in der Fläche und in keiner Aussparung liegt.
    /// </summary>
    private static float Inhalt(Rect2 rahmen, Func<Vector2, bool> drin)
    {
        var schritt = Mathf.Max(0.5f, Mathf.Sqrt(rahmen.Size.X * rahmen.Size.Y / 20000f));
        var zaehler = 0;
        for (var x = rahmen.Position.X + schritt / 2; x < rahmen.End.X; x += schritt)
            for (var z = rahmen.Position.Y + schritt / 2; z < rahmen.End.Y; z += schritt)
                if (drin(new Vector2(x, z))) zaehler++;
        return zaehler * schritt * schritt;
    }

    /// <summary>Wählt einen Eintrag nach seinem Gewicht (Gewichte ≤ 0 zählen nicht; sind alle 0, der erste).</summary>
    private static T NachGewicht<T>(IReadOnlyList<T> liste, Func<T, double> gewicht, Random zufall)
    {
        var summe = liste.Sum(x => Math.Max(0, gewicht(x)));
        var wurf = zufall.NextDouble() * summe;
        foreach (var x in liste)
        {
            wurf -= Math.Max(0, gewicht(x));
            if (wurf < 0) return x;
        }
        return liste[0];
    }

    /// <summary>So viel Abstand (Meter) hält die Natur zu Gebäuden, Bauzonen und zum Rand der Wege.</summary>
    private const float NaturAbstand = 3;

    /// <summary>So viel Abstand (Meter) hält die Natur zur Mauer.</summary>
    private const float NaturAbstandMauer = 6;

    /// <summary>
    /// Darf hier etwas wachsen? Nicht in der Stadt und dicht an der Mauer, nicht auf einem Weg (mit Rand), nicht auf einem Gebäude der Karte – bei
    /// Umlandbetrieben und Arbeitsstellen so groß, wie das Modell wirklich ist, beim Felsberg auf der Ellipse in seiner Fläche –, nicht auf einer
    /// Bauzone (dort wird gebaut, nicht gepflanzt).
    /// </summary>
    private bool NaturFrei(Vector3 p)
    {
        if (_karte is null) return false;
        if (InDerStadt(p) || AbstandZurMauer(p) < NaturAbstandMauer) return false;
        foreach (var (von, bis, breite) in _wegStrecken)
            if (AbstandZurStrecke(p, von, bis) < breite / 2 + NaturAbstand) return false;
        foreach (var g in _karte.Gebaeude)
            if (AufGebaeude(g, p)) return false;
        return !_karte.Bauzonen.Any(z => z.Abstand(p.X, p.Z) < NaturAbstand);
    }

    /// <summary>Liegt der Punkt auf der Fläche eines Gebäudes der Karte (mit <see cref="NaturAbstand"/> Rand)?</summary>
    private bool AufGebaeude(Kartengebaeude g, Vector3 p)
    {
        var groesse = GroesseVon(g);
        if (groesse == Vector2.Zero) return false;
        var lokal = new Basis(Vector3.Up, DrehungVon(g)).Inverse() * (p - Punkt3(g.Punkt));
        if (g.Art == Bausteinart.Berg) // der Felsberg: die Ellipse in seiner Fläche, ohne Rand
            return Mathf.Pow(lokal.X / (groesse.X / 2), 2) + Mathf.Pow(lokal.Z / (groesse.Y / 2), 2) < 1;
        var halb = groesse / 2;
        if (g.Art is Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle && g.Baustein?.Modell is { } datei)
        {
            // Die Modelle im Umland sind größer als ihre belegte Fläche (Gelände, Zaun, Beete): ihr ganzer Quader zählt
            var masse = UmlandAnkerImModell(datei).Masse;
            var m = (float)g.Massstab;
            halb = new Vector2(Mathf.Max(halb.X, Mathf.Max(-masse.Position.X, masse.End.X) * m), Mathf.Max(halb.Y, Mathf.Max(-masse.Position.Z, masse.End.Z) * m));
        }
        return Mathf.Abs(lokal.X) < halb.X + NaturAbstand && Mathf.Abs(lokal.Z) < halb.Y + NaturAbstand;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jahreszeit
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Das Laub aller geladenen Modelle: das Material und seine Sommerfarbe (so wie das Modell es mitbringt).</summary>
    private readonly Dictionary<BaseMaterial3D, Color> _laubStoffe = new();
    private Jahreszeit _jahreszeit = Jahreszeit.Sommer;

    /// <summary>Herbstfarben des Laubs (sRGB): „Laub hell“ gelb, „Laub“ orange, „Laub dunkel“ braun.</summary>
    private static readonly Dictionary<string, Color> Herbstlaub = new()
    {
        ["Laub hell"] = new Color(0.86f, 0.70f, 0.22f),
        ["Laub"] = new Color(0.78f, 0.40f, 0.12f),
        ["Laub dunkel"] = new Color(0.50f, 0.28f, 0.13f),
    };

    /// <summary>
    /// Winterfarben (sRGB) für das Laub, das im Winter noch dran ist, wenn die Kronen kahl sind – Buchshecken, Beete, Weide, Grasbüschel und Marktware:
    /// verdorrt und matt (Stroh, Lehm, Moor) statt herbstlich bunt.
    /// </summary>
    private static readonly Dictionary<string, Color> Winterlaub = new()
    {
        ["Laub hell"] = new Color(0.60f, 0.54f, 0.36f),
        ["Laub"] = new Color(0.40f, 0.34f, 0.23f),
        ["Laub dunkel"] = new Color(0.29f, 0.25f, 0.19f),
    };

    /// <summary>Merkt ein Laubmaterial vor und färbt es sofort nach der gerade geltenden Jahreszeit. Andere Materialien bleiben unberührt.</summary>
    private void LaubMelden(BaseMaterial3D stoff)
    {
        if (!Herbstlaub.ContainsKey(stoff.ResourceName) || _laubStoffe.ContainsKey(stoff)) return;
        _laubStoffe[stoff] = stoff.AlbedoColor;
        LaubFaerben(stoff, _laubStoffe[stoff]);
    }

    /// <summary>Wie <see cref="LaubMelden(BaseMaterial3D)"/> für alle Flächen eines Netzes.</summary>
    private void LaubMelden(Mesh netz)
    {
        for (var i = 0; i < netz.GetSurfaceCount(); i++)
            if (netz.SurfaceGetMaterial(i) is BaseMaterial3D stoff) LaubMelden(stoff);
    }

    private void LaubFaerben(BaseMaterial3D stoff, Color sommer) =>
        stoff.AlbedoColor = _jahreszeit switch
        {
            Jahreszeit.Herbst => Herbstlaub[stoff.ResourceName],
            Jahreszeit.Winter => Winterlaub[stoff.ResourceName],
            _ => sommer,
        };

    // ═════════════════════════════════════════════════════════════════════════
    //  Kronen im Winter
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Laubkronen („Laubkrone_*“) und kahlen Äste („Winteraeste_*“) der eingesetzten Modelle: Beide sind Kinder des Knotens, in dem der Baum steht
    /// (Stufe, Haus, Wald), und folgen so dessen Sichtbarkeit. Im Winter zeigt das Spiel die Äste statt der Krone.
    /// </summary>
    private readonly List<(MeshInstance3D Knoten, bool Aeste)> _kronen = new();
    private int _kronenSchwelle = 64;

    /// <summary>
    /// Meldet alle Kronen und Winteräste unterhalb von <paramref name="wurzel"/> an und stellt sie auf die gerade geltende Jahreszeit ein – auch Modelle,
    /// die erst später gebaut werden, haben so sofort den richtigen Stand. Die Laubmaterialien der Kronen melden sich dabei mit.
    /// </summary>
    private void KronenMelden(Node wurzel)
    {
        var winter = _jahreszeit == Jahreszeit.Winter;
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            var aeste = name.StartsWith("Winteraeste_");
            if (!aeste && !name.StartsWith("Laubkrone_")) continue;
            if (_kronen.Count >= _kronenSchwelle) // abgerissene und ersetzte Häuser nehmen ihre Kronen mit
            {
                _kronen.RemoveAll(k => !IsInstanceValid(k.Knoten));
                _kronenSchwelle = Math.Max(64, _kronen.Count * 2);
            }
            _kronen.Add((teil, aeste));
            teil.Visible = aeste == winter;
            if (teil.Mesh is { } netz) LaubMelden(netz);
        }
    }

    /// <summary>
    /// Stellt die Jahreszeit ein (jedes Bild gefragt, angefasst wird nur beim Wechsel). Frühling und Sommer zeigen das Laub, wie die Modelle es
    /// mitbringen, Herbst gelb, orange und braun. Im Winter zeigen Bäume und Büsche der Natur ihre kahlen Fassungen („…_Winter“), in den Modellen
    /// wechseln „Laubkrone_*“ zu „Winteraeste_*“, und das übrige Laub (Hecken, Beete, Weide) verdorrt zu Stroh und Braun.
    /// </summary>
    private void JahreszeitSetzen(Jahreszeit jahreszeit)
    {
        if (jahreszeit == _jahreszeit) return;
        _jahreszeit = jahreszeit;
        foreach (var (stoff, sommer) in _laubStoffe) LaubFaerben(stoff, sommer);

        var winter = jahreszeit == Jahreszeit.Winter;
        foreach (var (vielfach, sommerNetz, winterNetz) in _naturWinter) vielfach.Mesh = OberflaecheNetz(winter ? winterNetz : sommerNetz); // mit Struktur, wenn die Oberflächendetails an sind
        _kronen.RemoveAll(k => !IsInstanceValid(k.Knoten));
        foreach (var (knoten, aeste) in _kronen) knoten.Visible = aeste == winter;
    }
}
