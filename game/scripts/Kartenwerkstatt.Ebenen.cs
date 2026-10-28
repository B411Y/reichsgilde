using Godot;

namespace Reichsgilde;

/// <summary>
/// Der Knopf „Ebenen“ in der Kopfleiste – ein Teil der <c>partial class Kartenwerkstatt</c>: öffnet eine kleine Liste mit Häkchen, mit der sich
/// Bauzonen, Baum- und Naturzonen, Requisiten-Zonen, Linien und Punkte (Wege, Mauern, Gewässer, Kartenrand) und Beschriftungen einzeln ausblenden
/// lassen – damit man das Gelände darunter sieht. Taste H blendet alles aus (oder wieder ein). Der Zustand gilt nur für die Sitzung und ändert
/// die Karte nicht; die Knoten setzt <see cref="Stadtansicht.WerkstattEbenenAnwenden"/>.
/// </summary>
public partial class Kartenwerkstatt
{
    private static readonly (Werkstattebene Ebene, string Name, string Tipp)[] Ebenen =
    {
        (Werkstattebene.Bauzonen, "Bauzonen", "Bauzonen\nDie Flächen für Werkstatt-, Wohn- und Einwohnerplätze (Kern, Mauerring, Vorstadt, Umland) mit ihren Rändern und der Kapazitätsanzeige."),
        (Werkstattebene.Natur, "Baum- und Naturzonen", "Baum- und Naturzonen\nWald-, Feld- und Seeflächen samt Bäumen, Äckern und einzeln gesetzten Pflanzen."),
        (Werkstattebene.Requisiten, "Requisiten-Zonen", "Requisiten-Zonen\nStreuflächen mit Büschen, Gras, Steinen und Holz."),
        (Werkstattebene.Linien, "Wege und Grenzen", "Wege und Grenzen\nDie Linien und Punkte der Bearbeitung: Markierungen der Wege und Mauern, Pfeile der Gewässer und der Kartenrand. Wege, Mauern und Wasser selbst bleiben."),
        (Werkstattebene.Beschriftungen, "Beschriftungen", "Beschriftungen\nAlle Namen in der Karte (Gebäude, Flächen, Wege, Tore)."),
    };

    private PanelContainer? _ebenenTafel;
    private Button? _ebenenKnopf;
    private readonly Dictionary<Werkstattebene, CheckBox> _ebenenHaken = new();

    private void EbenenKnopfBauen(Container zeile)
    {
        _ebenenKnopf = KopfKnopf(zeile, "", "Ebenen", EbenenUmschalten, "Ebenen\nBauzonen, Natur, Requisiten, Linien und Beschriftungen ein- oder ausblenden (nur für diese Sitzung). Taste H: alles aus oder an.", umschalter: true);
    }

    private void EbenenUmschalten()
    {
        if (_ebenenKnopf is null) return;
        if (!_ebenenKnopf.ButtonPressed)
        {
            if (_ebenenTafel is not null) _ebenenTafel.Visible = false;
            return;
        }
        if (_ebenenTafel is null) EbenenTafelBauen();
        _ebenenTafel!.Visible = true;
        EbenenAuffrischen();
        // unter den Knopf – erst nach dem Legen der Kopfleiste (beim Start per Option ist sie noch nicht gelegt)
        Callable.From(() =>
        {
            if (_ebenenKnopf is null || _ebenenTafel is null) return;
            var ort = _ebenenKnopf.GlobalPosition + new Vector2(0, _ebenenKnopf.Size.Y + 4);
            (_ebenenTafel.OffsetLeft, _ebenenTafel.OffsetTop) = (ort.X, ort.Y);
        }).CallDeferred();
    }

    private void EbenenTafelBauen()
    {
        var (tafel, spalte) = Tafel(rand: 10, abstand: 4);
        (tafel.AnchorLeft, tafel.AnchorTop, tafel.AnchorRight, tafel.AnchorBottom) = (0, 0, 0, 0);
        _oberflaeche.AddChild(tafel);
        _ebenenTafel = tafel;
        Hauptmenue.Text(spalte, "Ebenen", 18, Gold);
        foreach (var (ebene, name, tipp) in Ebenen)
        {
            var haken = new CheckBox { Text = name, ButtonPressed = _stadt.WerkstattEbeneSichtbar(ebene), FocusMode = Control.FocusModeEnum.None, TooltipText = tipp };
            haken.AddThemeFontSizeOverride("font_size", 14);
            var meine = ebene;
            haken.Toggled += an => _stadt.WerkstattEbeneSetzen(meine, an);
            spalte.AddChild(haken);
            _ebenenHaken[ebene] = haken;
        }
        var knoepfe = new HBoxContainer();
        knoepfe.AddThemeConstantOverride("separation", 6);
        spalte.AddChild(knoepfe);
        foreach (var (text, an) in new[] { ("Alle an", true), ("Alle aus", false) })
        {
            var knopf = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, TooltipText = an ? "Alle Ebenen einblenden" : "Alle Ebenen ausblenden (Taste H)" };
            knopf.AddThemeFontSizeOverride("font_size", 13);
            var wert = an;
            knopf.Pressed += () => EbenenAlle(wert);
            knoepfe.AddChild(knopf);
        }
    }

    /// <summary>Alle Ebenen an oder aus (Haken der Liste folgen).</summary>
    private void EbenenAlle(bool an)
    {
        foreach (var (ebene, _, _) in Ebenen) _stadt.WerkstattEbeneSetzen(ebene, an);
        EbenenAuffrischen();
    }

    private void EbenenAuffrischen()
    {
        foreach (var (ebene, haken) in _ebenenHaken) haken.SetPressedNoSignal(_stadt.WerkstattEbeneSichtbar(ebene));
    }

    /// <summary>Taste H: ist noch etwas sichtbar, alles aus – sonst alles an.</summary>
    private void EbenenTaste() => EbenenAlle(!Ebenen.Any(e => _stadt.WerkstattEbeneSichtbar(e.Ebene)));
}
