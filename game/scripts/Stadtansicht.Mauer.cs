using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Stadtmauer – elfter Teil der <c>partial class Stadtansicht</c>.
///
/// Der Mauer-Baukasten des Katalogs (<c>stadtmauer.glb</c>, siehe <c>docs/bereiche/darstellung.md</c>): „Mauerstueck“, „Mauerturm“, „Eckturm“ und „Tor“,
/// jedes mit dem Ursprung im Nullpunkt, lokal X entlang der Mauer, +Z zur Feldseite. <see cref="MauerAufbauen"/> setzt sie für eine beliebige
/// Mauerlinie zusammen: Mauerstücke entlang jeder Strecke, ein Eckturm an jedem Knick, dazwischen etwa alle 35 m ein Mauerturm,
/// an jedem Tor die Torburg. Jede Mauer der Karte (<c>mauern</c>: Punktfolge, offen oder geschlossen, mit Toren) ruft es auf
/// (<see cref="MauerAusKarte"/>) – das Quadrat der Greybox-Stadt ebenso wie der Halbkreis von Köln.
///
/// Die vielen gleichen Teile (Mauerstücke, Mauertürme, Ecktürme) sind je Teil ein <see cref="MultiMeshInstance3D"/>: Die Grafikkarte zeichnet
/// alle Kopien in einem Aufruf je Material, statt hundert einzelner Knoten.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Länge eines Mauerstücks im Modell; es lässt sich auf 3 bis 5 m strecken, damit eine Strecke aufgeht.</summary>
    private const float MauerstueckLaenge = 4;

    /// <summary>Die Mauerstücke enden so weit links und rechts von der Mitte eines Tors (die Torburg ist 16 m breit).</summary>
    private const float TorAbstand = 7.6f;

    /// <summary>Etwa so weit stehen Mauertürme auseinander.</summary>
    private const float MauerturmAbstand = 35;

    /// <summary>Ab diesem Knick (Grad) bekommt ein Punkt der Mauerlinie einen Eckturm; flachere Knicke laufen nur durch.</summary>
    private const float EckturmAbWinkel = 12;

    /// <summary>
    /// Baut eine Mauer aus dem Baukasten <paramref name="datei"/> (<c>stadtmauer.glb</c>).
    /// </summary>
    /// <param name="linie">Die Punkte der Mauerlinie in Metern; jeder Punkt ist ein Knick oder ein Ende.</param>
    /// <param name="geschlossen">Ob die letzte Strecke zum ersten Punkt zurückläuft (Ring) oder die Linie offen endet (Halbkreis am Fluss).</param>
    /// <param name="stadtmitte">Ein Punkt in der Stadt – bestimmt, welche Seite der Mauer die Feldseite ist (die von ihm abgewandte).</param>
    /// <param name="tore">Die Tore mit Namen (Kennung wie beim Anklicken) und Ort; ein Tor sitzt auf der Strecke, der es am nächsten liegt.</param>
    private void MauerAufbauen(string datei, IReadOnlyList<Vector3> linie, bool geschlossen, Vector3 stadtmitte, IReadOnlyList<(string Name, Vector3 Ort)> tore)
    {
        if (Modell(datei)?.Instantiate<Node3D>() is not { } kasten) return;
        var stueckNetz = (kasten.FindChild("Mauerstueck", true, false) as MeshInstance3D)?.Mesh;
        var turmNetz = (kasten.FindChild("Mauerturm", true, false) as MeshInstance3D)?.Mesh;
        var eckNetz = (kasten.FindChild("Eckturm", true, false) as MeshInstance3D)?.Mesh;
        var torNetz = (kasten.FindChild("Tor", true, false) as MeshInstance3D)?.Mesh;
        var torAnker = kasten.FindChild("Anker_Tuer", true, false) as Node3D;
        var torAnkerLage = torAnker?.Position;
        kasten.Free();
        if (stueckNetz is null || turmNetz is null || eckNetz is null || torNetz is null)
        {
            GD.PushError($"Modell unvollständig: res://assets/modelle/{datei}.glb braucht Mauerstueck, Mauerturm, Eckturm und Tor.");
            return;
        }

        var stuecke = new List<Transform3D>();
        var tuerme = new List<Transform3D>();
        var ecken = new List<Transform3D>();
        var strecken = geschlossen ? linie.Count : linie.Count - 1;
        var torZuStrecke = tore.Select(t => (t, Strecke: NaechsteStrecke(linie, geschlossen, t.Ort))).ToList();

        for (var i = 0; i < strecken; i++)
        {
            var von = linie[i];
            var bis = linie[(i + 1) % linie.Count];
            var laenge = von.DistanceTo(bis);
            var entlang = (bis - von) / laenge;
            var lage = Ausrichtung(entlang, (von + bis) / 2 - stadtmitte);

            // Die Strecke zwischen den Toren in Abschnitte teilen: Mauerstücke enden ±TorAbstand vom Tor.
            var schnitte = new List<(float Anfang, float Ende)>();
            var anfang = 0f;
            foreach (var (tor, _) in torZuStrecke.Where(x => x.Strecke == i).OrderBy(x => (x.t.Ort - von).Dot(entlang)))
            {
                var t = Mathf.Clamp((tor.Ort - von).Dot(entlang), 0, laenge);
                schnitte.Add((anfang, t - TorAbstand));
                anfang = t + TorAbstand;
                var torLage = new Transform3D(lage, von + entlang * t);
                TorSetzen(tor.Name, torNetz, torLage, torAnkerLage);
            }
            schnitte.Add((anfang, laenge));

            foreach (var (s0, s1) in schnitte)
            {
                var abschnitt = s1 - s0;
                if (abschnitt < 0.5f) continue;
                var anzahl = Math.Max(1, (int)Math.Round(abschnitt / MauerstueckLaenge));
                var stueck = abschnitt / anzahl;
                for (var k = 0; k < anzahl; k++)
                    stuecke.Add(new Transform3D(new Basis(lage.X * (stueck / MauerstueckLaenge), Vector3.Up, lage.Z), von + entlang * (s0 + (k + 0.5f) * stueck)));
                // dazwischen Mauertürme, etwa alle 35 m
                var innere = (int)Math.Round(abschnitt / MauerturmAbstand) - 1;
                for (var k = 1; k <= innere; k++)
                    tuerme.Add(new Transform3D(lage, von + entlang * (s0 + abschnitt * k / (innere + 1))));
            }
        }

        // Ecktürme an Ecken und Enden
        for (var i = 0; i < linie.Count; i++)
        {
            var offenesEnde = !geschlossen && (i == 0 || i == linie.Count - 1);
            if (!offenesEnde)
            {
                var vorher = linie[(i + linie.Count - 1) % linie.Count];
                var nachher = linie[(i + 1) % linie.Count];
                var knick = Mathf.RadToDeg((linie[i] - vorher).Normalized().AngleTo((nachher - linie[i]).Normalized()));
                if (knick < EckturmAbWinkel) continue;
            }
            var aussen = linie[i] - stadtmitte;
            aussen.Y = 0;
            ecken.Add(new Transform3D(Ausrichtung(Vector3.Up.Cross(aussen.Normalized()), aussen), linie[i]));
        }

        Vielfach("Mauerstuecke", stueckNetz, stuecke);
        Vielfach("Mauertuerme", turmNetz, tuerme);
        Vielfach("Eckturme", eckNetz, ecken);
    }

    /// <summary>
    /// Die Ausrichtung eines Teils auf einer Strecke: lokal X entlang der Strecke, Y oben, Z (Feldseite) zur Außenseite. Die Strecke selbst hat
    /// keine feste Richtung, darum dreht sich <paramref name="entlang"/> bei Bedarf um – so gilt für jede Kartenlage dieselbe Regel.
    /// </summary>
    private static Basis Ausrichtung(Vector3 entlang, Vector3 aussen)
    {
        entlang.Y = 0;
        entlang = entlang.Normalized();
        var feld = entlang.Cross(Vector3.Up); // X × Y = Z: bei +X als Richtung zeigt die Feldseite nach +Z
        if (feld.Dot(aussen) < 0)
        {
            entlang = -entlang;
            feld = -feld;
        }
        return new Basis(entlang, Vector3.Up, feld);
    }

    /// <summary>Die Strecke der Linie, der <paramref name="ort"/> am nächsten liegt (für die Tore).</summary>
    private static int NaechsteStrecke(IReadOnlyList<Vector3> linie, bool geschlossen, Vector3 ort)
    {
        var beste = 0;
        var kleinster = float.MaxValue;
        for (var i = 0; i < (geschlossen ? linie.Count : linie.Count - 1); i++)
        {
            var a = linie[i];
            var b = linie[(i + 1) % linie.Count];
            var abstand = ort.DistanceTo(Geometry3D.GetClosestPointToSegment(ort, a, b));
            if (abstand < kleinster) { kleinster = abstand; beste = i; }
        }
        return beste;
    }

    /// <summary>Die Torburg: Modell, Anker „Anker_Tuer“ in der Durchfahrt (Kennung „Tor:&lt;Name&gt;“), Klickfläche und Schild wie beim alten Torhaus.</summary>
    private void TorSetzen(string name, Mesh netz, Transform3D lage, Vector3? ankerLage)
    {
        var tor = new MeshInstance3D { Name = name, Mesh = netz, Transform = lage };
        AddChild(tor);
        if (ankerLage is { } anker)
        {
            var knoten = new Node3D { Name = "Anker_Tuer", Position = anker };
            tor.AddChild(knoten);
            _modellAnker["Tor:" + name] = (knoten, null);
        }
        // Die Klickfläche liegt längs der Mauer: Torburg 16,5 m breit und 7 m tief
        var breit = new Vector3(Mathf.Abs(lage.Basis.X.X), 0, Mathf.Abs(lage.Basis.X.Z));
        var tief = new Vector3(Mathf.Abs(lage.Basis.Z.X), 0, Mathf.Abs(lage.Basis.Z.Z));
        Klickflaeche(name, lage.Origin + new Vector3(0, 7, 0), breit * 16.5f + tief * 7 + new Vector3(0, 14, 0));
        Beschriftung(name, lage.Origin + new Vector3(0, 20, 0), 48, kennung: name);
    }

    /// <summary>Viele Kopien eines Teils in einem Zeichenaufruf je Material (<see cref="MultiMeshInstance3D"/>).</summary>
    private void Vielfach(string name, Mesh netz, List<Transform3D> lagen)
    {
        if (lagen.Count == 0) return;
        var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = lagen.Count };
        for (var i = 0; i < lagen.Count; i++) vielfach.SetInstanceTransform(i, lagen[i]);
        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = vielfach });
    }
}
