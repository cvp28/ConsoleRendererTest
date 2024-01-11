
namespace ConsoleRendererTest.Codes;

public static class Globals
{
	public static readonly StyleCode[] AllStyles = { StyleCode.Bold, StyleCode.Dim, StyleCode.Italic, StyleCode.Underlined, StyleCode.Blink, StyleCode.Inverted, StyleCode.CrossedOut };
}

public enum StyleCode : byte
{
	None		= 0,
	Bold		= 1,
	Dim			= 2,
	Italic		= 3,
	Underlined	= 4,
	Blink		= 5,
	Inverted	= 7,
	CrossedOut	= 9
}

public enum ResetCode : byte
{
	ResetAll		= 0,
	NormalIntensity	= 22,
	NotItalicised	= 23,
	NotUnderlined	= 24,
	NotBlinking		= 25,
	NotInverted		= 27,
	NotCrossedOut	= 29
}

public static class CodeHelper
{
	public static byte GetMask(this StyleCode Style) => Style switch
	{
		StyleCode.Bold			=> 0b00000001,
		StyleCode.Dim			=> 0b00000010,
		StyleCode.Italic		=> 0b00000100,
		StyleCode.Underlined	=> 0b00001000,
		StyleCode.Blink			=> 0b00010000,
		StyleCode.Inverted		=> 0b00100000,
		StyleCode.CrossedOut	=> 0b01000000,

		StyleCode.None			=> 0b00000000,
		_						=> 0b00000000
	};
	
	public static ResetCode GetResetCode(this StyleCode Code) => Code switch
	{
		StyleCode.Bold			=> ResetCode.NormalIntensity,
		StyleCode.Dim			=> ResetCode.NormalIntensity,

		StyleCode.Italic		=> ResetCode.NotItalicised,
		StyleCode.Underlined	=> ResetCode.NotUnderlined,

		StyleCode.Blink			=> ResetCode.NotBlinking,

		StyleCode.Inverted		=> ResetCode.NotInverted,
		StyleCode.CrossedOut	=> ResetCode.NotCrossedOut,
		
		StyleCode.None			=> ResetCode.ResetAll,
		_						=> ResetCode.ResetAll
	};
}