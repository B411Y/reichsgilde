using Godot;

namespace Reichsgilde;

// Zeiger-Symbole des 3D-Handschuhs (Modell: zeiger_symbole.glb, Skript tools/modelle/handschuh/zeiger_symbole.py):
// - Windrose (Messing, vier Spitzen) am Platz des Fragezeichens, solange die Fläche unter der Maus verschiebbar ist (Stammbaum: Zeiger-Art Verschieben).
// - Messing-Textcursor (römisches I) über Textfeldern: die Hand schrumpft um die Fingerspitze bis weg, dann wächst das I; Zustandslogik SymbolWechsel (ZeigerSymbole.cs).
// Beides steht in derselben Bühne wie die Hand, also im selben Bild – die Spiegel-Ebenen der Popups (HandschuhSpiegel) zeigen es ohne eigenen Code mit.
public partial class HandschuhZeiger
{
    private const string SymbolePfad = "res://assets/modelle/zeiger/zeiger_symbole.glb";

    /// <summary>Die Windrose wird so weit (Meter) zur Kamera hin vor das Fragezeichen gestellt, damit die Hand sie nicht verdeckt; die Kamera ist orthogonal, Lage und Größe im Bild bleiben gleich.</summary>
    private const float RoseVor = 0.12f;

    /// <summary>Abstand des I vor der Kamera (Meter): Blickpunkt der Bühne, dort liegt auch die Hand.</summary>
    private const float IAbstand = 0.8f;

    private readonly SymbolWechsel _symbol = new();
    private Node3D? _rose, _textI;
    private Transform3D _modellGrund = Transform3D.Identity;
    private Vector3 _spitzeModell; // Zeigerpunkt der Hand im Raum des Modells: um ihn schrumpft die Hand
    private bool _verschieben, _verschiebenFest; // Fläche unter der Maus verschiebbar; beim Drücken darüber festgehalten bis zum Loslassen

    /// <summary>Lädt Windrose und I in die Bühne; fehlt die Datei, bleibt es bei Hand und Fragezeichen (über Textfeldern dann die Hand ohne Wechsel).</summary>
    private void SymboleLaden()
    {
        if (_modell is null || _skelett is null) return;
        _modellGrund = _modell.Transform;
        _spitzeModell = _modell.GlobalTransform.AffineInverse() * (_skelett.GlobalTransform * _spitzeLokal).Origin;
        var szene = ResourceLoader.Exists(SymbolePfad) ? GD.Load<PackedScene>(SymbolePfad) : null;
        if (szene?.Instantiate() is not Node3D wurzel)
        {
            GD.PushWarning($"[handschuh] Zeiger-Symbole fehlen oder nicht ladbar: {SymbolePfad}; ohne Windrose und Messing-I");
            return;
        }
        MaterialienEinrichten(wurzel);
        _rose = Halter("Windrose", wurzel, _bild, "Windrose", "Windrose_Umriss");
        _textI = Halter("Text_I", wurzel, _kamera, "Text_I", "Text_I_Umriss");
        if (_textI is not null) _textI.Position = new Vector3(0, 0, -IAbstand);
        wurzel.Free();
        GD.Print($"[handschuh] Zeiger-Symbole geladen: Windrose {(_rose is null ? "FEHLT" : "da")}, Text_I {(_textI is null ? "FEHLT" : "da")}; Wechsel Hand/I {SymbolWechsel.DauerSekunden:0.00} s je Richtung");
    }

    /// <summary>Hängt die Netze aus der .glb unter einen neuen, zuerst unsichtbaren Halter; null, wenn das Hauptnetz fehlt.</summary>
    private static Node3D? Halter(string name, Node wurzel, Node eltern, params string[] netze)
    {
        if (wurzel.FindChild(netze[0], true, false) is null) return null;
        var halter = new Node3D { Name = "Symbol_" + name, Visible = false };
        eltern.AddChild(halter);
        foreach (var netzName in netze)
            if (wurzel.FindChild(netzName, true, false) is Node3D netz)
            {
                netz.Owner = null; // gehörte zur Szene der Datei
                netz.GetParent().RemoveChild(netz);
                netz.Transform = Transform3D.Identity;
                halter.AddChild(netz);
            }
        return halter;
    }

    /// <summary>Jedes Bild: Wechsel Hand/I weiterrechnen, Hand um die Fingerspitze skalieren, I und Windrose stellen, Fragezeichen nur ohne Windrose.</summary>
    private void SymboleAktualisieren(double dt)
    {
        _symbol.Schritt(dt, (_text || TextImFensterNehmen()) && _textI is not null); // Textfeld im Hauptfenster oder in einem Popup (HandschuhZeiger.Spiegel.cs)
        var hand = (float)_symbol.HandGroesse;
        if (_modell is not null)
        {
            var s = Math.Max(hand, 0.001f);
            var p = _modellGrund * _spitzeModell;
            _modell.Transform = new Transform3D(Basis.FromScale(Vector3.One * s), p * (1 - s)) * _modellGrund;
            _modell.Visible = hand > 0.001f;
        }
        if (_textI is not null)
        {
            var i = (float)_symbol.IGroesse;
            _textI.Scale = Vector3.One * Math.Max(i, 0.001f);
            _textI.Visible = i > 0.001f;
        }
        var (frage, rose) = ZeigerMarken.Waehlen(_hilfe, (_verschieben || _verschiebenFest) && _rose is not null);
        if (_marke is not null) _marke.Visible = frage;
        if (_markeUmriss is not null) _markeUmriss.Visible = frage;
        if (_rose is not null)
        {
            _rose.Visible = rose && hand > 0.001f;
            if (_rose.Visible && _marke is not null)
            {
                // am Platz des Fragezeichens, aber zum Betrachter gedreht (Spitzen genau oben, rechts, unten, links): glTF +Y der Datei zeigt zur Kamera
                var kamera = _kamera.GlobalTransform.Basis.Orthonormalized();
                var basis = kamera * new Basis(Vector3.Right, Mathf.Pi / 2) * Basis.FromScale(Vector3.One * MarkeFaktor * Math.Max(hand, 0.001f));
                _rose.GlobalTransform = new Transform3D(basis, _marke.GlobalPosition + kamera.Z * RoseVor);
            }
        }
    }

    /// <summary>Zeigerpunkt im Bild, solange das I gilt: die Mitte des I-Balkens (sonst null).</summary>
    private Vector2? SymbolZeigerpunkt(float skala) =>
        _textI is not null && _symbol.ZeigerpunktIstI ? _kamera.UnprojectPosition(_textI.GlobalPosition) * skala : null;
}
