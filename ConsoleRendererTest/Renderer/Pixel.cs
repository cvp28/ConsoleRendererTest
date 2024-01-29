
using System.Runtime.Intrinsics;

namespace SharpCanvas;

public struct Pixel
{
	public int Index;
	
	public char Character;
	
	public Color24 Foreground;
	public Color24 Background;
	
	public byte Style;
	
	public readonly bool Styled => Style != 0;

	public static bool Equals(Pixel first, Pixel second) =>	first.Character == second.Character		&&
															first.Foreground == second.Foreground	&&
															first.Background == second.Background	&&
															first.Style == second.Style;
	
	// Slightly more efficient as there is a chance not all of these conditionals will need to be checked to return a result
	public static bool NotEqual(Pixel first, Pixel second) =>	first.Character != second.Character		||
																first.Foreground != second.Foreground	||
																first.Background != second.Background	||
																first.Style != second.Style;
}

public struct LPixel
{
	// Gives me 8 bytes with which to store character, colors, and styling
	private long Data;
	
	// Data = 4 components split across 8 bytes (lowest to highest)
	// Character -> Foreground -> Background -> Style
	
	public ushort Character
	{
		get;
		set;
	}
	
	public byte Foreground
	{
		get;
		set;
	}
	
	public byte Background
	{
		get;
		set;
	}
	
	public byte Style
	{
		get;
		set;
	}
}