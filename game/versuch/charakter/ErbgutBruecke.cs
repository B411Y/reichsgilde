// Versuch Charakter Phase 6 (S0): Brücke zum echten Erbgut des Kerns (Testszene, kein Spielcode).
// Galerie und Bögen würfeln und vererben damit genau wie das Spiel (Erbgut.Wuerfeln / Erbgut.Vererben mit Dominanz,
// Mutation und Korrelationen) statt mit einem eigenen Würfel. Der Kern wird nur gelesen.
//
// GDScript hält kein C#-Objekt des Kerns; die Brücke merkt sich jedes Erbgut unter einer Nummer (Griff):
//   var b = load("res://versuch/charakter/ErbgutBruecke.cs").new()
//   b.VorlageLaden("")                                   # Erbgutvorlage.Standard (game/daten/erbgut.json im Kern)
//   b.VorlageLaden("res://versuch/charakter/erbgut_vorschlag.json")   # Vorschlagsdatei (Erbgutvorlage.AusJson)
//   var vater = b.Wuerfeln(6201, "Mensch")             # Griff
//   var kind = b.Vererben(6221, vater, mutter, "Mensch")
//   b.SichtbarerWert(kind, "bartwuchs", "w")          # bei Frauen das Gen aus „beiFrauen“
//   b.SichtbareWerte(kind, "m")                        # alle Gene als Dictionary
//   b.Pruefen(10001, 1000)                              # Abnahme S0 (siehe unten)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;

namespace Reichsgilde.Versuch;

public partial class ErbgutBruecke : RefCounted
{
	private Erbgutvorlage _vorlage = Erbgutvorlage.Standard;
	private readonly Dictionary<int, (Erbgut Erbgut, Volk Volk)> _griffe = new();
	private int _naechster = 1;

	/// <summary>Vorlage wählen: "" = Erbgutvorlage.Standard, sonst eine Datei (res:// oder Pfad). Gibt "" oder den Fehler zurück.</summary>
	public string VorlageLaden(string pfad)
	{
		try
		{
			_vorlage = string.IsNullOrEmpty(pfad) ? Erbgutvorlage.Standard : Erbgutvorlage.AusDatei(Pfad(pfad));
			return "";
		}
		catch (Exception ex) when (ex is ErbgutvorlageFehler or IOException)
		{
			return ex is ErbgutvorlageFehler f ? string.Join("\n", f.Fehler) : ex.Message;
		}
	}

	/// <summary>Version und Zahl der Gene der gewählten Vorlage.</summary>
	public int VorlageVersion() => _vorlage.Version;

	public string[] Gene() => _vorlage.Gene.Select(g => g.Kennung).ToArray();

	/// <summary>Erbgut für jemanden ohne Eltern (Erbgut.Wuerfeln); Ergebnis ist ein Griff.</summary>
	public int Wuerfeln(int saat, string volk)
	{
		var v = VolkAus(volk);
		return Merken(Erbgut.Wuerfeln(_vorlage, saat, v), v);
	}

	/// <summary>Erbgut eines Kindes (Erbgut.Vererben); vater/mutter sind Griffe (0 = unbekannt). Volk leer = das des Vaters.</summary>
	public int Vererben(int saat, int vater, int mutter, string volk = "")
	{
		var ev = vater != 0 ? _griffe[vater] : ((Erbgut, Volk)?)null;
		var em = mutter != 0 ? _griffe[mutter] : ((Erbgut, Volk)?)null;
		var v = !string.IsNullOrEmpty(volk) ? VolkAus(volk) : ev?.Item2 ?? em?.Item2 ?? Volk.Mensch;
		return Merken(Erbgut.Vererben(_vorlage, saat, v, ev?.Item1, em?.Item1), v);
	}

	public int SichtbarerWert(int griff, string gen, string geschlecht) =>
		_griffe[griff].Erbgut.SichtbarerWert(gen, GeschlechtAus(geschlecht));

	public int VerdeckterWert(int griff, string gen) => _griffe[griff].Erbgut.VerdeckterWert(gen);

	/// <summary>Alle sichtbaren Werte (wie die Darstellung sie liest: SichtbarerWert(gen, geschlecht)).</summary>
	public Godot.Collections.Dictionary SichtbareWerte(int griff, string geschlecht)
	{
		var e = _griffe[griff].Erbgut;
		var g = GeschlechtAus(geschlecht);
		var aus = new Godot.Collections.Dictionary();
		foreach (var gen in e.Vorlage.Gene)
			aus[gen.Kennung] = e.SichtbarerWert(gen.Kennung, g);
		return aus;
	}

	public Godot.Collections.Dictionary VerdeckteWerte(int griff)
	{
		var e = _griffe[griff].Erbgut;
		var aus = new Godot.Collections.Dictionary();
		foreach (var gen in e.Vorlage.Gene)
			aus[gen.Kennung] = e.VerdeckterWert(gen.Kennung);
		return aus;
	}

	public string AlsText(int griff) => _griffe[griff].Erbgut.AlsText();

	public void Vergessen() => _griffe.Clear();

	/// <summary>
	/// Abnahme S0 (Charakter Phase 6): Für <paramref name="anzahl"/> Startwerte ab <paramref name="von"/> und alle Völker
	///   (1) ergibt derselbe Startwert zweimal dieselben Bytes (Würfeln und Vererben),
	///   (2) sind alle bestehenden Gene mit und ohne Vorschlagsdatei gleich (sichtbares und verdecktes Allel) – beim Würfeln
	///       und beim Vererben über zwei Generationen.
	/// Vorschlag: <paramref name="vorschlag"/> (Datei) oder, wenn leer, eine Probe = erbgut.json + ein Probe-Gen mit einer
	/// Korrelation aus einem bestehenden Gen hinein (so wie S7 neue Gene anlegt).
	/// </summary>
	public Godot.Collections.Dictionary Pruefen(int von, int anzahl, string vorschlag = "")
	{
		var std = Erbgutvorlage.Standard;
		Erbgutvorlage vor;
		try
		{
			vor = string.IsNullOrEmpty(vorschlag) ? Erbgutvorlage.AusJson(Probevorlage()) : Erbgutvorlage.AusDatei(Pfad(vorschlag));
		}
		catch (ErbgutvorlageFehler f)
		{
			return new Godot.Collections.Dictionary { ["fehler"] = string.Join("\n", f.Fehler) };
		}
		var abwW = 0;
		var abwV = 0;
		var bytesUngleich = 0;
		var vergleiche = 0;
		foreach (var volk in Enum.GetValues<Volk>())
		{
			for (var s = von; s < von + anzahl; s++)
			{
				var a1 = Erbgut.Wuerfeln(std, s, volk);
				var a2 = Erbgut.Wuerfeln(std, s, volk);
				if (a1.AlsText() != a2.AlsText()) bytesUngleich++;
				var b1 = Erbgut.Wuerfeln(vor, s, volk);
				abwW += Abweichungen(std, a1, b1);
				// zwei Generationen: Eltern aus s und s + 500000, Kind aus s + 1000000, Enkel mit einem gewürfelten Partner
				var am = Erbgut.Wuerfeln(std, s + 500000, volk);
				var bm = Erbgut.Wuerfeln(vor, s + 500000, volk);
				var ak = Erbgut.Vererben(std, s + 1000000, volk, a1, am);
				var ak2 = Erbgut.Vererben(std, s + 1000000, volk, a1, am);
				if (ak.AlsText() != ak2.AlsText()) bytesUngleich++;
				var bk = Erbgut.Vererben(vor, s + 1000000, volk, b1, bm);
				abwV += Abweichungen(std, ak, bk);
				var ap = Erbgut.Wuerfeln(std, s + 1500000, volk);
				var bp = Erbgut.Wuerfeln(vor, s + 1500000, volk);
				abwV += Abweichungen(std, Erbgut.Vererben(std, s + 2000000, volk, ap, ak), Erbgut.Vererben(vor, s + 2000000, volk, bp, bk));
				vergleiche += 3 * std.Gene.Count;
			}
		}
		return new Godot.Collections.Dictionary
		{
			["anzahl"] = anzahl,
			["voelker"] = Enum.GetValues<Volk>().Length,
			["gene_standard"] = std.Gene.Count,
			["gene_vorschlag"] = vor.Gene.Count,
			["korrelationen_vorschlag"] = vor.Korrelationen.Count,
			["vergleiche"] = vergleiche,
			["abweichungen_wuerfeln"] = abwW,
			["abweichungen_vererben"] = abwV,
			["gleiche_bytes_ungleich"] = bytesUngleich,
		};
	}

	// Zahl der bestehenden Gene (Standard), deren Allele in b anders sind als in a
	private static int Abweichungen(Erbgutvorlage std, Erbgut a, Erbgut b)
	{
		var n = 0;
		foreach (var g in std.Gene)
			if (a.SichtbarerWert(g.Kennung) != b.SichtbarerWert(g.Kennung) || a.VerdeckterWert(g.Kennung) != b.VerdeckterWert(g.Kennung))
				n++;
		return n;
	}

	// erbgut.json + Probe-Gen „probe_s0“ (alle Völker, Nicht-Menschen Streuung 0) + Korrelation kiefer_breite → probe_s0
	private static string Probevorlage()
	{
		var wurzel = JsonNode.Parse(File.ReadAllText(Pfad("res://daten/erbgut.json")))!;
		var voelker = new JsonObject();
		foreach (var v in Enum.GetValues<Volk>())
			voelker[v.ToString()] = new JsonObject
			{
				["min"] = 16, ["mittel"] = 128, ["max"] = 240, ["streuung"] = v == Volk.Mensch ? 32 : 0,
			};
		wurzel["gene"]!.AsArray().Add(new JsonObject
		{
			["kennung"] = "probe_s0", ["name"] = "Probe S0", ["art"] = "Merkmal", ["dominanz"] = 50, ["mutation"] = 6,
			["voelker"] = voelker,
		});
		var korr = wurzel["korrelationen"]?.AsArray();
		if (korr is null)
		{
			korr = new JsonArray();
			wurzel["korrelationen"] = korr;
		}
		korr.Add(new JsonObject { ["von"] = "kiefer_breite", ["zu"] = "probe_s0", ["staerke"] = 40, ["grund"] = "Probe S0" });
		return wurzel.ToJsonString();
	}

	private int Merken(Erbgut e, Volk v)
	{
		var g = _naechster++;
		_griffe[g] = (e, v);
		return g;
	}

	private static string Pfad(string pfad) => pfad.StartsWith("res://") || pfad.StartsWith("user://") ? ProjectSettings.GlobalizePath(pfad) : pfad;

	private static Volk VolkAus(string volk) => Enum.TryParse<Volk>(volk, out var v) ? v : Volk.Mensch;

	private static Geschlecht GeschlechtAus(string g) => g is "w" or "Frau" or "0" ? Geschlecht.Frau : Geschlecht.Mann;
}
