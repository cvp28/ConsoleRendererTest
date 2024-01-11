
namespace ConsoleRendererTest;

public struct Pixel
{
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