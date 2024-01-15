using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ConsoleRendererTest;

public enum ColorLevel
{
	Foreground,
	Background
}

public struct Color24
{
	public byte Red;
	public byte Green;
	public byte Blue;
	
	public Color24(byte r, byte g, byte b)
	{
		Red = r;
		Green = g;
		Blue = b;
	}
	
	public void AsForegroundVT(ref StringBuilder sb)
	{
		sb.Append("\u001b[38;2;");
		sb.Append(Red);
		sb.Append(';');
		sb.Append(Green);
		sb.Append(';');
		sb.Append(Blue);
		sb.Append('m');
	}
	public void AsBackgroundVT(ref StringBuilder sb)
	{
		sb.Append("\u001b[48;2;");
		sb.Append(Red);
		sb.Append(';');
		sb.Append(Green);
		sb.Append(';');
		sb.Append(Blue);
		sb.Append('m');
	}

	public static readonly Color24 White = new(255, 255, 255);
	public static readonly Color24 Black = new(0, 0, 0);
	
	public static bool operator==(Color24 first, Color24 second) => Equals(first, second);
	
	public static bool operator!=(Color24 first, Color24 second) => !Equals(first, second);
	
	public static bool Equals(Color24 x, Color24 y) => x.Red == y.Red && x.Green == y.Green && x.Blue == y.Blue;
}