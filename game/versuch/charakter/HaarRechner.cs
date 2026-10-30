// Versuch Charakter Phase 4: schnelle Rechenschleifen für Haar und Bart (Testszene, kein Spielcode).
// charakter_haar.gd ruft sie, wenn das Skript geladen werden kann; sonst rechnet es dieselben Schleifen in GDScript
// (etwa hundertmal langsamer).
//   Mischen:   Versatz der Stützpunkte = Summe der Formversätze × Formgewicht
//   Verformen: Lage jedes Haarpunkts = Grundlage + gewichtete Versätze seiner vier Stützpunkte; Ausgabe für TressFX
//              (x, y, z, inverse Masse: die ersten zwei Punkte jeder Strähne fest)
//   Struktur:  Phase 5, Haarstruktur glatt – wellig – lockig: Wellen (in einer Ebene) bzw. Locken (Schraube) auf jede
//              Strähne legen, bei Bedarf feiner abgetastet (mehr Punkte je Strähne)
using Godot;

namespace Reichsgilde.Versuch;

public partial class HaarRechner : RefCounted
{
	public float[] Mischen(float[] versatz, int anzahl, int[] formen, float[] gewichte)
	{
		var d = new float[anzahl * 3];
		for (int f = 0; f < formen.Length; f++)
		{
			float w = gewichte[f];
			int start = formen[f] * anzahl * 3;
			for (int j = 0; j < anzahl * 3; j++)
				d[j] += w * versatz[start + j];
		}
		return d;
	}

	public float[] Verformen(float[] basis, int[] idx, float[] gew, float[] d, int punkteJeStraehne)
	{
		int n = basis.Length / 3;
		var aus = new float[n * 4];
		for (int i = 0; i < n; i++)
		{
			float x = basis[3 * i], y = basis[3 * i + 1], z = basis[3 * i + 2];
			for (int k = 0; k < 4; k++)
			{
				int c = idx[4 * i + k];
				float w = gew[4 * i + k];
				x += w * d[3 * c];
				y += w * d[3 * c + 1];
				z += w * d[3 * c + 2];
			}
			aus[4 * i] = x;
			aus[4 * i + 1] = y;
			aus[4 * i + 2] = z;
			aus[4 * i + 3] = (i % punkteJeStraehne) < 2 ? 0f : 1f;
		}
		return aus;
	}

	// pos: Ausgabe von Verformen (x, y, z, w je Punkt), S Strähnen zu P Punkten, neuP Punkte je Strähne danach (Zweierpotenz,
	// höchstens 64). hoehe: Ausschlag (m), wl: Wellenlänge (m), locke: 0 = Welle, 1 = Schraube. phase: je Strähne 0..1
	// (Strähnen eines Büschels haben dieselbe Phase, sonst wirkt das Haar zerzaust). mitte: Kopfmitte (für „außen“).
	public float[] Struktur(float[] pos, int S, int P, int neuP, float hoehe, float wl, float locke, float[] phase, Vector3 mitte)
	{
		var aus = new float[S * neuP * 4];
		var p = new Vector3[neuP];
		var bogen = new float[neuP];
		for (int s = 0; s < S; s++)
		{
			// neu abtasten (die Punkte liegen gleichmäßig nach Bogenlänge, also linear über den Index)
			for (int j = 0; j < neuP; j++)
			{
				float f = (float)j * (P - 1) / (neuP - 1);
				int a = Mathf.Min((int)f, P - 2);
				float t = f - a;
				int ia = 4 * (s * P + a), ib = ia + 4;
				p[j] = new Vector3(Mathf.Lerp(pos[ia], pos[ib], t), Mathf.Lerp(pos[ia + 1], pos[ib + 1], t), Mathf.Lerp(pos[ia + 2], pos[ib + 2], t));
				bogen[j] = j == 0 ? 0f : bogen[j - 1] + (p[j] - p[j - 1]).Length();
			}
			float ph = phase[s] * Mathf.Tau;
			float amp0 = hoehe * (0.75f + 0.5f * Mathf.PosMod(phase[s] * 7.31f, 1f));
			for (int j = 0; j < neuP; j++)
			{
				Vector3 tang = (p[Mathf.Min(j + 1, neuP - 1)] - p[Mathf.Max(j - 1, 0)]).Normalized();
				Vector3 raus = p[j] - mitte;
				raus = (raus - tang * raus.Dot(tang)).Normalized();
				Vector3 quer = tang.Cross(raus).Normalized();
				float an = Mathf.SmoothStep(0.008f, 0.035f, bogen[j]);       // an der Wurzel glatt
				float th = Mathf.Tau * bogen[j] / wl + ph;
				Vector3 d = quer * Mathf.Sin(th) + raus * (Mathf.Cos(th) * locke);
				Vector3 q = p[j] + d * (amp0 * an);
				int o = 4 * (s * neuP + j);
				aus[o] = q.X;
				aus[o + 1] = q.Y;
				aus[o + 2] = q.Z;
				aus[o + 3] = j < 2 ? 0f : 1f;
			}
		}
		return aus;
	}
}
