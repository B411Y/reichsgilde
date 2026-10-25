using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Begründungen der KI für die sichtbare Ratssitzung (PR #202): je Stimme ein kurzer Satz aus derselben Rechnung wie die Stimme
/// (<see cref="StimmtFuerStatut"/>, <see cref="StimmtFuerWegantrag"/>, <see cref="StimmtFuerRatsantrag"/>, <see cref="Wahlstimme"/>).
/// Die Stimme selbst kommt nie von hier – nur der Satz dazu.
/// </summary>
public sealed partial class KiRivale
{
    private static string Ganz(double wert) => Zahlenformat.Ganz(wert);

    /// <summary>Warum die KI das Statut beantragt hat.</summary>
    internal string AntragsgrundStatut(Fronstatut statut)
    {
        var mehr = ErwartungUnter(statut) - ErwartungUnter(_w.Statut);
        return mehr > 0
            ? $"Unter „{statut.Name()}“ brächten unsere Betriebe etwa {Ganz(mehr)} Gulden mehr im Jahr."
            : $"Die Stadt soll unter „{statut.Name()}“ wirtschaften.";
    }

    internal string GrundStatut(Fronstatut statut, bool? ja)
    {
        var mehr = ErwartungUnter(statut) - ErwartungUnter(_w.Statut);
        if (ja == true) return $"Bringt den eigenen Betrieben etwa {Ganz(mehr)} Gulden mehr im Jahr.";
        return mehr > 0
            ? $"Bringt nur {Ganz(mehr)} Gulden mehr im Jahr – zu wenig für einen Wechsel."
            : $"Brächte den eigenen Betrieben {Ganz(-mehr)} Gulden weniger im Jahr.";
    }

    internal string GrundWegantrag(int weg, double von, double bis, bool? ja)
    {
        var wb = _w.Wegebau;
        var verkehr = wb.MittlererVerkehr(weg, von, bis);
        if (verkehr >= wb.Daten!.Rat.AntragVerkehr) return $"Viel Verkehr ({Ganz(verkehr)} Fahrten im Jahr) – und die Stadt zahlt.";
        return ja == true ? "Die eigenen Karren fahren dort – und die Stadt zahlt." : "Die eigenen Karren fahren dort nicht.";
    }

    /// <summary>Warum die KI den Antrag zu Steuer, Bau oder Fest gestellt hat (wie in ihrer Meldung beim Beantragen).</summary>
    internal string AntragsgrundRatsantrag(Ratsantrag a)
    {
        var kasse = _w.Kasse;
        return a.Art switch
        {
            Antragsart.Steuersatz when a.Prozent > kasse.SteuersatzProzent => $"Die Stadtkasse ist leer ({Ganz(kasse.Stand)} Gulden).",
            Antragsart.Steuersatz => $"Wir zahlten im Vorjahr {Ganz(Konto.Vorjahr?.Steuer ?? 0)} Gulden Steuern – das drückt das Gewerbe.",
            Antragsart.Bauvorhaben when _w.Ratsantraege.BauvorhabenNamens(a.Bauvorhaben) is { } b => $"{b.Name}: {Nutzen(b)}",
            Antragsart.Bauvorhaben => "Das nützt der ganzen Stadt.",
            _ => "Ein Fest bringt Käufer auf den Markt und Gäste in die Wirtshäuser.",
        };
    }

    internal string GrundRatsantrag(Ratsantrag a, bool? ja)
    {
        var kasse = _w.Kasse;
        var ra = _w.Ratsantraege;
        var nachher = kasse.Stand - ra.Kosten(a);
        switch (a.Art)
        {
            case Antragsart.Steuersatz when a.Prozent < kasse.SteuersatzProzent:
                return "Weniger Steuer für das eigene Gewerbe.";
            case Antragsart.Steuersatz:
                return ja == true ? "Die Kasse ist im Minus – das Amtsgehalt soll sicher sein." : "Mehr Steuer schadet dem eigenen Gewerbe.";
            case Antragsart.Bauvorhaben:
                if (nachher < 0) return "Die Stadtkasse fiele ins Minus.";
                return ra.BauvorhabenNamens(a.Bauvorhaben) is { } b && ja == true ? $"{b.Name}: {Nutzen(b)}" : "Nützt dem eigenen Haus nicht.";
            default:
                if (nachher < 0) return "Die Stadtkasse fiele ins Minus.";
                if (a.Antragsteller == FuehrendesHaus) return "Das Ansehen ginge an das führende Haus.";
                return ja == true ? "Mehr Gäste und Käufer für die eigenen Betriebe." : "Kein Wirtshaus und keine Werkstatt – das Fest bringt nichts.";
        }
    }

    /// <summary>Was ein Bauvorhaben dem Haus bringt, in einem halben Satz.</summary>
    private static string Nutzen(BauvorhabenDaten b) => b.Wirkung switch
    {
        Bauwirkung.Hafen => "Die eigenen Schiffe laden schneller.",
        Bauwirkung.Nachfrage => "Mehr Käufer für die eigenen Werkstätten.",
        Bauwirkung.Nachtschutz => "Die eigenen Betriebe sind nachts sicherer.",
        _ => "Die Familie bleibt gesünder.",
    };

    /// <summary>Warum die KI bei der Wahl so stimmt – nach denselben Schritten wie <see cref="Wahlstimme"/>.</summary>
    internal string GrundWahl(Amt amt, IReadOnlyList<Konto?> kandidaten, Konto? wahl)
    {
        var inhaber = amt.Ratsherr is not null ? null : amt.Konto;
        var feind = kandidaten.FirstOrDefault(k => k is not null && HatGeschadet(k));
        var ueberFeind = feind is not null && feind != wahl ? $" {feind.Inhaber} hat dem Haus geschadet." : "";
        if (Profil.Gaunerei <= 0 && wahl == inhaber && kandidaten.Contains(inhaber) && (inhaber is null || !HatGeschadet(inhaber)))
            return $"Vorsichtig: bleibt beim Amtsinhaber.{ueberFeind}";
        var fuehrend = Profil.Gaunerei >= 2 && kandidaten.Count > 1 && FuehrendesHaus is { } f && kandidaten.Contains(f) && f != wahl
            ? $" Nie für das führende Haus ({f.Inhaber})." : "";
        var grund = wahl is null ? "Ein Ratsherr ist ungefährlicher als jedes Haus." : $"{wahl.Inhaber} ist am wenigsten gefährlich.";
        return $"{grund}{fuehrend}{ueberFeind}";
    }
}
