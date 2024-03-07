
namespace SharpCanvas;

internal struct Pixel : IEqualityComparer<Pixel>, IComparable<Pixel>
{
	public int Index { get; init; }
	
	public char Character { get; init; }
	
	public Color24 Foreground { get; init; }
	public Color24 Background { get; init; }

	public byte Style { get; init; }

	public static bool operator ==(Pixel x, Pixel y) => x.Equals(y);
	public static bool operator !=(Pixel x, Pixel y) => !x.Equals(y);
	
	public bool Equals(Pixel x, Pixel y) => x.GetHashCode() == y.GetHashCode();
	public override bool Equals(object obj) => GetHashCode() == obj.GetHashCode();

	private int _hash;

	public int GetHashCode(Pixel obj) => obj._hash;
	public override int GetHashCode() => _hash;
	
	public int CompareTo(Pixel other) => GetHashCode().CompareTo(other.GetHashCode());

	public void CalculateHash()
	{
		unchecked { _hash = HashCode.Combine(Index, Character, Foreground, Background, Style); }
	}
}

//	public enum PixelAction
//	{
//		Skip,		// Do not render
//		Clear,		// Currently on screen - to be cleared
//		Draw		// Not on screen - to be drawn
//	}

public struct Pixel8
{
	// Gives me 8 bytes with which to store character, colors, styling, and position
	private long Data;
	
	// Data = 6 components split across 8 bytes (lowest to highest)
	// Character -> Foreground -> Background -> Style -> X (Column) -> Y (Row)
	
	// All of this is a little ugly as each set requires a restructuring of the whole data type.
	
	// However, this should all be insanely fast as we can check every component that consitutes the data of a terminal cell neatly inside of 64-bits
	// This means we can use SIMD and cache, if available, to check many cells at once very quickly
	
	public ushort Character
	{
		get => (ushort) Data;
		set => Data = ((long) Y << 56) | ((long) X << 48) | ((long) Style << 32) | ((long) Background << 24) | ((long) Foreground << 16) | value;
	}
	
	public byte Foreground
	{
		get => (byte) (Data >> 16);
		set => Data = ((long) Y << 56) | ((long) X << 48) | ((long) Style << 32) | ((long) Background << 24) | ((long) value << 16) | Character;
	}
	
	public byte Background
	{
		get => (byte) (Data >> 24);
		set => Data = ((long) Y << 56) | ((long) X << 48) | ((long) Style << 32) | ((long) value << 24) | ((long) Foreground << 16) | Character;
	}
	
	public byte Style
	{
		get => (byte) (Data >> 32);
		set => Data = ((long) Y << 56) | ((long) X << 48) | ((long) value << 32) | ((long) Background << 24) | ((long) Foreground << 16) | Character;
	}
	
	// Coordinates are split across two data types as
	// We are almost always going to have more columns than rows.
	// Also, I think that being limited to 256 rows is fine for a renderer as otherwise capable as this one
	
	// Besides, this is all meant for terminal UIs first, general purpose graphics second
	// What UI will have more than 256 rows?
	
	public ushort X
	{
		get => (ushort) (Data >> 48);
		set => Data = ((long) Y << 56) | ((long) value << 48) | ((long) Style << 32) | ((long) Background << 24) | ((long) Foreground << 16) | Character;
	}
	
	public byte Y
	{
		get => (byte) (Data >> 56);
		set => Data = ((long) value << 56) | ((long) X << 48) | ((long) Style << 32) | ((long) Background << 24) | ((long) Foreground << 16) | Character;
	}
	
	public static implicit operator long(Pixel8 p) => p.Data;
}