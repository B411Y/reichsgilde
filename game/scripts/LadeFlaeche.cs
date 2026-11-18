using Godot;

namespace Reichsgilde;

/// <summary>
/// Eine Fläche der Zunftlade aus einem Stoff des Themas: Holz, Stein, Pergament. Sie ist ein <see cref="PanelContainer"/> (der Innenabstand kommt
/// aus dem Thema) und zeichnet selbst, was ein Stilkasten von Godot nicht kann: den Grund als <b>Kachel in fester Größe</b> (das Holz verzerrt
/// nicht, gleich wie groß die Fläche ist) und den <b>Steinrand aus neun Feldern in beliebiger Breite</b> (die Textur hat den Rand doppelt so
/// breit, damit er bei großer Oberfläche und in 4K scharf bleibt). Was gezeichnet wird, steht unter der Typ-Variante im Thema
/// (<c>zunftlade_thema.tres</c>, dort erklärt): <c>grund</c>, <c>grund_kachel</c>, <c>rahmen</c>, <c>rahmen_quelle</c>, <c>rahmen_breite</c>,
/// <c>kante</c>, <c>kante_breite</c>. Nur Texturen und eine schlichte Linie – keine Verläufe, keine Schatten.
/// </summary>
public partial class LadeFlaeche : PanelContainer
{
    [Flags]
    public enum Seiten { Keine = 0, Oben = 1, Unten = 2, Links = 4, Rechts = 8, Alle = 15 }

    /// <summary>An welchen Seiten der Rand steht (die Lade hat unten keinen: Sie steht auf dem Bildrand).</summary>
    public Seiten Rand { get; set; } = Seiten.Alle;

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Linear;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var flaeche = new Rect2(Vector2.Zero, Size);
        if (HasThemeIcon("grund")) GrundZeichnen(GetThemeIcon("grund"), flaeche, GetThemeConstant("grund_kachel"));
        if (HasThemeIcon("rahmen") && GetThemeConstant("rahmen_breite") > 0)
            RahmenZeichnen(GetThemeIcon("rahmen"), flaeche, GetThemeConstant("rahmen_quelle"), GetThemeConstant("rahmen_breite"));
        else if (HasThemeColor("kante") && GetThemeConstant("kante_breite") is > 0 and var breite)
            KanteZeichnen(flaeche, GetThemeColor("kante"), breite);
    }

    /// <summary>Kachelt die Textur mit <paramref name="kachel"/> Einheiten Kantenlänge; 0: einmal deckend, das Seitenverhältnis bleibt (Überstand fällt weg).</summary>
    private void GrundZeichnen(Texture2D bild, Rect2 flaeche, int kachel)
    {
        var quelle = bild.GetSize();
        if (kachel <= 0)
        {
            var mass = Mathf.Max(flaeche.Size.X / quelle.X, flaeche.Size.Y / quelle.Y);
            var teil = flaeche.Size / mass;
            DrawTextureRectRegion(bild, flaeche, new Rect2((quelle - teil) / 2, teil));
            return;
        }
        // Die Kacheln beginnen nicht an der eigenen Ecke, sondern im Raster der Eltern: Zwei Fächer nebeneinander zeigen nicht dasselbe Stück Holz.
        var je = quelle / kachel; // Texturpixel je Einheit
        var versatz = new Vector2(Mathf.PosMod(Position.X, kachel), Mathf.PosMod(Position.Y, kachel));
        for (var y = -versatz.Y; y < flaeche.Size.Y; y += kachel)
            for (var x = -versatz.X; x < flaeche.Size.X; x += kachel)
            {
                var von = new Vector2(Mathf.Max(x, 0), Mathf.Max(y, 0));
                var bis = new Vector2(Mathf.Min(x + kachel, flaeche.Size.X), Mathf.Min(y + kachel, flaeche.Size.Y));
                if (bis.X <= von.X || bis.Y <= von.Y) continue;
                DrawTextureRectRegion(bild, new Rect2(flaeche.Position + von, bis - von), new Rect2((von - new Vector2(x, y)) * je, (bis - von) * je));
            }
    }

    /// <summary>Neun Felder: vier Ecken, dazwischen läuft das Band gekachelt um. Fehlt eine Seite, laufen die Nachbarn bis an den Rand durch.</summary>
    private void RahmenZeichnen(Texture2D bild, Rect2 f, int q, int b)
    {
        var n = bild.GetSize();
        var lang = n.X - 2 * q;           // Länge des Bandes in der Textur
        var schritt = lang * b / q;       // … und im Bild
        bool oben = Rand.HasFlag(Seiten.Oben), unten = Rand.HasFlag(Seiten.Unten), links = Rand.HasFlag(Seiten.Links), rechts = Rand.HasFlag(Seiten.Rechts);
        float x0 = f.Position.X, y0 = f.Position.Y, x1 = f.End.X, y1 = f.End.Y;
        // Waagerechte Bänder (zwischen den Ecken, ohne senkrechten Nachbarn bis an den Rand)
        void Quer(float y, float quelleY)
        {
            var von = x0 + (links ? b : 0);
            var bis = x1 - (rechts ? b : 0);
            for (var x = von; x < bis; x += schritt)
            {
                var breite = Mathf.Min(schritt, bis - x);
                DrawTextureRectRegion(bild, new Rect2(x, y, breite, b), new Rect2(q, quelleY, breite * q / b, q));
            }
        }
        void Hoch(float x, float quelleX)
        {
            var von = y0 + (oben ? b : 0);
            var bis = y1 - (unten ? b : 0);
            for (var y = von; y < bis; y += schritt)
            {
                var hoehe = Mathf.Min(schritt, bis - y);
                DrawTextureRectRegion(bild, new Rect2(x, y, b, hoehe), new Rect2(quelleX, q, q, hoehe * q / b));
            }
        }
        if (oben) Quer(y0, 0);
        if (unten) Quer(y1 - b, n.Y - q);
        if (links) Hoch(x0, 0);
        if (rechts) Hoch(x1 - b, n.X - q);
        var eck = new Vector2(b, b);
        var qEck = new Vector2(q, q);
        if (oben && links) DrawTextureRectRegion(bild, new Rect2(x0, y0, eck.X, eck.Y), new Rect2(Vector2.Zero, qEck));
        if (oben && rechts) DrawTextureRectRegion(bild, new Rect2(x1 - b, y0, eck.X, eck.Y), new Rect2(new Vector2(n.X - q, 0), qEck));
        if (unten && links) DrawTextureRectRegion(bild, new Rect2(x0, y1 - b, eck.X, eck.Y), new Rect2(new Vector2(0, n.Y - q), qEck));
        if (unten && rechts) DrawTextureRectRegion(bild, new Rect2(x1 - b, y1 - b, eck.X, eck.Y), new Rect2(new Vector2(n.X - q, n.Y - q), qEck));
    }

    private void KanteZeichnen(Rect2 f, Color farbe, int breite)
    {
        if (Rand.HasFlag(Seiten.Oben)) DrawRect(new Rect2(f.Position, new Vector2(f.Size.X, breite)), farbe);
        if (Rand.HasFlag(Seiten.Unten)) DrawRect(new Rect2(f.Position.X, f.End.Y - breite, f.Size.X, breite), farbe);
        if (Rand.HasFlag(Seiten.Links)) DrawRect(new Rect2(f.Position, new Vector2(breite, f.Size.Y)), farbe);
        if (Rand.HasFlag(Seiten.Rechts)) DrawRect(new Rect2(f.End.X - breite, f.Position.Y, breite, f.Size.Y), farbe);
    }
}

/// <summary>
/// Die Maske des Porträt-Fachs: ein Rundbogen (Halbkreis oben, gerade Seiten), um <see cref="Rand"/> eingerückt. Sie zeichnet nur die Form;
/// ihre Kinder (Grund und Porträt) sind nur innerhalb dieser Form zu sehen (<see cref="CanvasItem.ClipChildren"/>). Darüber liegt der Steinbogen.
/// </summary>
public partial class LadeBogen : Control
{
    /// <summary>Einzug der Form gegenüber dem eigenen Rechteck (die Breite des Steinbogens).</summary>
    public float Rand { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipChildren = ClipChildrenMode.Only;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var mitte = new Vector2(Size.X / 2, Size.X / 2);
        var radius = Size.X / 2 - Rand;
        if (radius <= 0 || Size.Y - Rand <= mitte.Y) return;
        const int Schritte = 32;
        var punkte = new Vector2[Schritte + 3];
        punkte[0] = new Vector2(Rand, Size.Y - Rand);
        for (var i = 0; i <= Schritte; i++)
            punkte[i + 1] = mitte + Vector2.Left.Rotated(Mathf.Pi * i / Schritte) * radius;
        punkte[Schritte + 2] = new Vector2(Size.X - Rand, Size.Y - Rand);
        DrawColoredPolygon(punkte, Colors.White);
    }
}
