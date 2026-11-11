using System.IO;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Zwischenspeicher der gebackenen Passanten (Teil der <c>partial class Stadtansicht</c>): Die Texturen der Vertex-Animation (Lage und Normale
/// je Ecke und Bild) liegen nach dem ersten Backen in <c>user://passanten_vat/&lt;datei&gt;.bin</c>; beim nächsten Start werden sie nur gelesen.
///
/// <para>Der Schlüssel ist ein MD5 aus der Modelldatei (die .glb, im exportierten Spiel ihre .import-Datei), den Clips und ihrer Bildzahl,
/// den Bildern je Sekunde, den ausgelassenen Netzen und <see cref="ProbeCacheFassung"/> (erhöhen, wenn sich das Backen selbst ändert). Passt er
/// nicht, wird neu gebacken und überschrieben. Eine kaputte oder fremde Datei (falsche Kennung, Fassung, Länge) gilt als nicht vorhanden.
/// Geschrieben wird erst in eine .tmp-Datei und dann umbenannt, damit ein abgebrochener Start keine halbe Datei hinterlässt.</para>
///
/// <para>Startoptionen: <c>--probecache=0</c> liest und schreibt nichts (immer backen); <c>--probecachetest=1</c> prüft Treffer, Fehlschlag,
/// kaputte Datei und geänderten Schlüssel ohne Bildschirm und schreibt eine Zeile <c>[passantencache]</c>.</para>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Gebackene Daten eines Netzes: Eckenzahl, Zeilen (Bilder aller Clips), Umriss, Lage (RGBAH) und Normale (RGBA8).</summary>
    private sealed record ProbeVatDaten(string Name, int Ecken, int Zeilen, Aabb Kasten, byte[] Lage, byte[] Richtung);

    /// <summary>Fassung des Backens und des Dateiformats; erhöhen, wenn sich am Ergebnis etwas ändert, das der Schlüssel nicht sieht.</summary>
    private const int ProbeCacheFassung = 1;
    private const uint ProbeCacheKennung = 0x54564752; // „RGVT“

    private static readonly bool ProbeCacheAn = Wasserarten.Startoption("probecache") is not ("0" or "aus");

    private static string ProbeCachePfad(int art) =>
        ProjectSettings.GlobalizePath($"user://passanten_vat/{System.IO.Path.GetFileName(ProbeArten[art].Datei)}.bin");

    /// <summary>Schlüssel einer Probedatei: alles, wovon die gebackenen Daten abhängen.</summary>
    private static string ProbeCacheSchluessel(int art, int zeilen)
    {
        var glb = $"res://assets/modelle/{ProbeArten[art].Datei}.glb";
        var quelle = Godot.FileAccess.FileExists(glb) ? Godot.FileAccess.GetMd5(glb)
                   : Godot.FileAccess.FileExists(glb + ".import") ? Godot.FileAccess.GetMd5(glb + ".import") : "ohne-quelle";
        var text = $"{ProbeCacheFassung}|{quelle}|{string.Join(",", ProbeKlipsVon(art).Select(k => ProbeVatBilderFuer(k, art)))}|{zeilen}|{string.Join(",", ProbeKlipsVon(art))}|{(art is 2 or 3 ? string.Join(",", ProbeUnfreiOhne.Order()) : "")}";
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Liest den Zwischenspeicher einer Probedatei; null, wenn er fehlt, abgeschaltet, kaputt oder von einem anderen Schlüssel ist.</summary>
    private static Dictionary<string, ProbeVatDaten>? ProbeCacheLesen(int art, string schluessel, string? pfad = null)
    {
        pfad ??= ProbeCachePfad(art);
        if (!ProbeCacheAn && pfad == ProbeCachePfad(art) || !File.Exists(pfad)) return null;
        try
        {
            using var datei = File.OpenRead(pfad);
            using var leser = new BinaryReader(datei, Encoding.UTF8);
            if (leser.ReadUInt32() != ProbeCacheKennung || leser.ReadInt32() != ProbeCacheFassung || leser.ReadString() != schluessel) return null;
            bool Block(out byte[] block)      // Länge prüfen, bevor gelesen wird (eine verfälschte Länge darf keinen Riesenpuffer anlegen)
            {
                var n = leser.ReadInt32();
                block = n >= 0 && n <= datei.Length - datei.Position ? leser.ReadBytes(n) : Array.Empty<byte>();
                return n >= 0 && block.Length == n;
            }
            var anzahl = leser.ReadInt32();
            if (anzahl is < 0 or > 1000) return null;
            var aus = new Dictionary<string, ProbeVatDaten>(StringComparer.Ordinal);
            for (var i = 0; i < anzahl; i++)
            {
                var name = leser.ReadString();
                var ecken = leser.ReadInt32();
                var zeilen = leser.ReadInt32();
                var kasten = new Aabb(new Vector3(leser.ReadSingle(), leser.ReadSingle(), leser.ReadSingle()), new Vector3(leser.ReadSingle(), leser.ReadSingle(), leser.ReadSingle()));
                if (Block(out var lage) is false || Block(out var richtung) is false) return null;
                if (ecken <= 0 || zeilen <= 0 || lage.Length != ecken * zeilen * 8 || richtung.Length != ecken * zeilen * 4) return null;
                aus[name] = new ProbeVatDaten(name, ecken, zeilen, kasten, lage, richtung);
            }
            return leser.ReadUInt32() == ProbeCacheKennung ? aus : null; // Endmarke: abgeschnittene Dateien fallen auf
        }
        catch (Exception fehler) when (fehler is IOException or EndOfStreamException or UnauthorizedAccessException or ArgumentException)
        {
            GD.Print($"[passantencache] {pfad} unlesbar ({fehler.GetType().Name}), wird neu gebacken");
            return null;
        }
    }

    /// <summary>Schreibt den Zwischenspeicher einer Probedatei: erst .tmp, dann umbenennen.</summary>
    private static void ProbeCacheSchreiben(int art, string schluessel, IReadOnlyList<ProbeVatDaten> netze, string? pfad = null)
    {
        pfad ??= ProbeCachePfad(art);
        if (!ProbeCacheAn && pfad == ProbeCachePfad(art)) return;
        var tmp = pfad + ".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pfad)!);
            using (var datei = File.Create(tmp))
            using (var schreiber = new BinaryWriter(datei, Encoding.UTF8))
            {
                schreiber.Write(ProbeCacheKennung);
                schreiber.Write(ProbeCacheFassung);
                schreiber.Write(schluessel);
                schreiber.Write(netze.Count);
                foreach (var n in netze)
                {
                    schreiber.Write(n.Name);
                    schreiber.Write(n.Ecken);
                    schreiber.Write(n.Zeilen);
                    schreiber.Write(n.Kasten.Position.X); schreiber.Write(n.Kasten.Position.Y); schreiber.Write(n.Kasten.Position.Z);
                    schreiber.Write(n.Kasten.Size.X); schreiber.Write(n.Kasten.Size.Y); schreiber.Write(n.Kasten.Size.Z);
                    schreiber.Write(n.Lage.Length); schreiber.Write(n.Lage);
                    schreiber.Write(n.Richtung.Length); schreiber.Write(n.Richtung);
                }
                schreiber.Write(ProbeCacheKennung);
            }
            File.Move(tmp, pfad, true);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            GD.Print($"[passantencache] {pfad} nicht geschrieben ({fehler.Message})");
            try { File.Delete(tmp); } catch (IOException) { }
        }
    }

    /// <summary>
    /// <c>--probecachetest=1</c>: Treffer, Fehlschlag (keine Datei), geänderter Schlüssel, abgeschnittene und verfälschte Datei, alles in einer
    /// eigenen Testdatei unter <c>user://passanten_vat/selbsttest.bin</c>. Eine Zeile mit „bestanden“ oder den Fehlern.
    /// </summary>
    private static void ProbeCacheSelbsttest()
    {
        var pfad = ProjectSettings.GlobalizePath("user://passanten_vat/selbsttest.bin");
        var fehler = new List<string>();
        var daten = new[]
        {
            new ProbeVatDaten("Nah_a", 3, 2, new Aabb(Vector3.One, Vector3.One), Enumerable.Range(0, 48).Select(i => (byte)i).ToArray(), new byte[24]),
            new ProbeVatDaten("Fern_b", 1, 2, new Aabb(Vector3.Zero, Vector3.One), new byte[16], Enumerable.Range(0, 8).Select(i => (byte)(200 + i)).ToArray()),
        };
        try { File.Delete(pfad); } catch (IOException) { }
        if (ProbeCacheLesen(0, "S1", pfad) is not null) fehler.Add("ohne Datei kein Fehlschlag");
        ProbeCacheSchreiben(0, "S1", daten, pfad);
        var gelesen = ProbeCacheLesen(0, "S1", pfad);
        if (gelesen is null || gelesen.Count != 2 || !gelesen["Nah_a"].Lage.SequenceEqual(daten[0].Lage) || !gelesen["Fern_b"].Richtung.SequenceEqual(daten[1].Richtung)
            || gelesen["Nah_a"].Kasten != daten[0].Kasten)
            fehler.Add("Treffer liefert andere Daten");
        if (ProbeCacheLesen(0, "S2", pfad) is not null) fehler.Add("geänderter Schlüssel trifft");
        if (File.Exists(pfad + ".tmp")) fehler.Add(".tmp blieb liegen");
        var roh = File.ReadAllBytes(pfad);
        File.WriteAllBytes(pfad, roh[..(roh.Length / 2)]);
        if (ProbeCacheLesen(0, "S1", pfad) is not null) fehler.Add("abgeschnittene Datei trifft");
        var falsch = (byte[])roh.Clone();
        falsch[0] ^= 0xFF;
        File.WriteAllBytes(pfad, falsch);
        if (ProbeCacheLesen(0, "S1", pfad) is not null) fehler.Add("falsche Kennung trifft");
        File.WriteAllBytes(pfad, Encoding.UTF8.GetBytes("kein Zwischenspeicher"));
        if (ProbeCacheLesen(0, "S1", pfad) is not null) fehler.Add("fremde Datei trifft");
        ProbeCacheSchreiben(0, "S1", daten, pfad); // überschreibt die kaputte Datei
        if (ProbeCacheLesen(0, "S1", pfad) is null) fehler.Add("Überschreiben einer kaputten Datei misslang");
        try { File.Delete(pfad); } catch (IOException) { }
        GD.Print($"[passantencache] Selbsttest {(fehler.Count == 0 ? "bestanden" : "FEHLER: " + string.Join("; ", fehler))}");
    }
}
