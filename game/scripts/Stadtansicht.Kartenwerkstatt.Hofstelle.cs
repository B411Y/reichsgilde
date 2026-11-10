using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Das Werkzeug „Hofstelle“ der Kartenwerkstatt – ein Teil der <c>partial class Stadtansicht</c> (Überblick in Stadtansicht.Kartenwerkstatt.cs).
/// Eine Hofstelle setzt Werkstattplatz (16 × 14 m) und Wohnhausplatz (8 × 10 m) nebeneinander, vorn bündig, die Werkstatt links von vorn gesehen
/// (<see cref="Kartenentwurf.HofstelleSetzen"/>). Am Mauszeiger steht dazu der Umriss beider Plätze: Der Werkstattplatz ist der gewöhnliche
/// Umriss der Bausteine (mit Prüfung, grün/gelb/rot und Text), der Wohnhausplatz daneben ein zweiter, der nach seiner eigenen Prüfung
/// (<see cref="Kartenpruefung.PruefeStelle"/>) gefärbt ist. Q/E dreht beide gemeinsam (<see cref="WerkstattDrehung"/>).
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _wsHofWohn;
    private StandardMaterial3D? _wsHofWohnFarbe, _wsHofWohnKoerper;
    private (double X, double Z, double Drehung)? _wsHofGeprueft;
    private bool _wsHofWohnFehler, _wsHofWohnHinweis;

    /// <summary>Die Mitten beider Plätze für eine Hofstelle bei (x, z) – die Rechnung des Kerns (<see cref="Kartenentwurf.HofstelleLagen"/>).</summary>
    private ((double X, double Z) Werkstatt, (double X, double Z) Wohnhaus, double Drehung) HofstelleLagen(double x, double z, double drehung)
    {
        if (_entwurf is null) return ((x, z), (x, z), Kartenraster.Drehung(drehung));
        var (w, h, d) = _entwurf.HofstelleLagen(x, z, drehung);
        return ((w.X, w.Z), (h.X, h.Z), d);
    }

    /// <summary>Zeigt den Umriss beider Plätze an der Stelle (x, z) – grün, gelb oder rot, je nach Prüfung.</summary>
    private void HofstelleUmrissZeigen(double x, double z, double drehung)
    {
        if (_entwurf is null || _entwurf.Katalog.Finde("wohnhausplatz") is not { } wohn) return;
        var (pw, ph, d) = HofstelleLagen(x, z, drehung);
        UmrissZeigen("werkstattplatz", pw.X, pw.Z, d, 1, null);
        if (_wsHofGeprueft != (x, z, d))
        {
            _wsHofGeprueft = (x, z, d);
            var befunde = Kartenpruefung.PruefeStelle(_entwurf, "wohnhausplatz", ph.X, ph.Z, d).ToList();
            _wsHofWohnFehler = befunde.Any(b => b.Schwere == Schwere.Fehler);
            _wsHofWohnHinweis = !_wsHofWohnFehler && befunde.Any(b => b.Schwere == Schwere.Hinweis);
            if (_wsHofWohnFehler && _wsText is not null && _wsGrund is null)
                _wsGrund = "Wohnhausplatz: " + befunde.First(b => b.Schwere == Schwere.Fehler).Text; // der Text am Umriss nennt auch den Grund des zweiten
        }
        var groesse = wohn.Groesse is { Length: 2 } g ? new Vector2((float)g[0], (float)g[1]) : new Vector2(8, 10);
        if (_wsHofWohn is null)
        {
            _wsHofWohn = new Node3D { Name = "Werkstatt-Hofstelle-Wohnhaus" };
            AddChild(_wsHofWohn);
            _wsHofWohnFarbe = Transparent(Colors.White, 0.45f);
            _wsHofWohnKoerper = Transparent(Colors.White, 0.16f);
            var flaeche = new MeshInstance3D { Name = "Flaeche", Mesh = new BoxMesh(), MaterialOverride = _wsHofWohnFarbe };
            var koerper = new MeshInstance3D { Name = "Koerper", Mesh = new BoxMesh(), MaterialOverride = _wsHofWohnKoerper };
            foreach (var teil in new[] { flaeche, koerper })
            {
                teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                _wsHofWohn.AddChild(teil);
            }
        }
        const float hoehe = 5;
        _wsHofWohn.Visible = true;
        _wsHofWohn.Position = new Vector3((float)ph.X, FundamentVon(new Vector3((float)ph.X, 0, (float)ph.Z), Mathf.DegToRad((float)d), groesse).Hoehe, (float)ph.Z);
        _wsHofWohn.Rotation = new Vector3(0, Mathf.DegToRad((float)d), 0);
        var farbe = _wsGrund is not null || _wsHofWohnFehler ? new Color(0.95f, 0.28f, 0.25f) : _wsHinweis is not null || _wsHofWohnHinweis ? new Color(0.96f, 0.80f, 0.25f) : new Color(0.35f, 0.9f, 0.35f);
        _wsHofWohnFarbe!.AlbedoColor = new Color(farbe, 0.45f);
        _wsHofWohnKoerper!.AlbedoColor = new Color(farbe, 0.16f);
        var f = (MeshInstance3D)_wsHofWohn.GetNode("Flaeche");
        ((BoxMesh)f.Mesh).Size = new Vector3(groesse.X, 0.12f, groesse.Y);
        f.Position = new Vector3(0, 0.14f, 0);
        var k = (MeshInstance3D)_wsHofWohn.GetNode("Koerper");
        ((BoxMesh)k.Mesh).Size = new Vector3(Mathf.Max(0.4f, groesse.X - 0.4f), hoehe, Mathf.Max(0.4f, groesse.Y - 0.4f));
        k.Position = new Vector3(0, hoehe / 2, 0);
        if (_wsText is not null) _wsText.Text = "Hofstelle – " + (_wsGrund ?? (_wsHinweis is not null ? "Hinweis: " + _wsHinweis : _wsHofWohnFehler ? "Wohnhausplatz passt nicht" : "passt"));
    }

    /// <summary>Blendet beide Umrisse der Hofstelle aus (anderes Werkzeug, Maus außerhalb).</summary>
    private void HofstelleUmrissAus()
    {
        if (_wsHofWohn is not null) _wsHofWohn.Visible = false;
        _wsHofGeprueft = null;
    }

    /// <summary>Setzt die Hofstelle dorthin, wo ihr Umriss steht (sonst unter die Maus): ein Befehl im Kern, ein Schritt für Strg+Z.</summary>
    private void HofstelleKlick(Vector2? boden)
    {
        if (_entwurf is null) return;
        var (x, z) = _wsOrt is null && boden is { } b ? Einrasten(b.X, b.Y) : _wsUmriss is { Visible: true } u ? Hofmitte(u.Position) : (double.NaN, 0);
        if (double.IsNaN(x)) return;
        if (Befehl(() => _entwurf.HofstelleSetzen(x, z, WerkstattDrehung)) is { } h) WerkstattWaehlen(h.Werkstatt);
    }

    /// <summary>Die Mitte des Ganzen aus der Lage des Werkstattumrisses (der Umriss steht auf dem Werkstattplatz, links von der Mitte).</summary>
    private (double, double) Hofmitte(Vector3 werkstatt)
    {
        var (pw, _, d) = HofstelleLagen(0, 0, WerkstattDrehung); // Versatz der Werkstatt zur Mitte bei dieser Drehung
        return (werkstatt.X - pw.X, werkstatt.Z - pw.Z);
    }
}
