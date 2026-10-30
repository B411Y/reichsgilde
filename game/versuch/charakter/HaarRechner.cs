// Versuch Charakter Phase 4: schnelle Rechenschleifen für Haar und Bart (Testszene, kein Spielcode).
// charakter_haar.gd ruft sie, wenn das Skript geladen werden kann; sonst rechnet es dieselben Schleifen in GDScript
// (etwa hundertmal langsamer).
//   Mischen:   Versatz der Stützpunkte = Summe der Formversätze × Formgewicht
//   Verformen: Lage jedes Haarpunkts = Grundlage + gewichtete Versätze seiner vier Stützpunkte; Ausgabe für TressFX
//              (x, y, z, inverse Masse: die ersten zwei Punkte jeder Strähne fest)
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
}
