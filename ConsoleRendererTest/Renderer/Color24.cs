using System.Buffers;
using System.Text;
using Utf8StringInterpolation;

namespace SharpCanvas;

public enum ColorLevel
{
	Foreground,
	Background
}

public struct Color24
{
	public byte Red { get; init; }
	public byte Green { get; init; }
	public byte Blue { get; init; }

	public Color24(byte r, byte g, byte b)
	{
		Red = r;
		Green = g;
		Blue = b;
		_hash = HashCode.Combine(Red, Green, Blue);
	}
	
	public void AsForegroundVT(ref Utf8StringWriter<ArrayBufferWriter<byte>> sb)
	{
		sb.AppendFormat($"\u001b[38;2;{Red};{Green};{Blue}m");
	}
	public void AsBackgroundVT(ref Utf8StringWriter<ArrayBufferWriter<byte>> sb)
	{
		sb.AppendFormat($"\u001b[48;2;{Red};{Green};{Blue}m");
	}

	public static readonly Color24 White = new(255, 255, 255);
	public static readonly Color24 Black = new(0, 0, 0);
	
	public static bool operator==(Color24 first, Color24 second) => Equals(first, second);
	
	public static bool operator!=(Color24 first, Color24 second) => !Equals(first, second);
	
	//public static bool Equals(Color24 x, Color24 y) => x.Red == y.Red && x.Green == y.Green && x.Blue == y.Blue;
	
	public override bool Equals(object obj) => GetHashCode() == obj.GetHashCode();

	private int _hash;

	public override int GetHashCode() => _hash;
}