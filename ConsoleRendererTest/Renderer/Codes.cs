
namespace SharpCanvas.Codes;

public static class CodesGlobals
{
	public static readonly StyleCode[] AllStyles = { StyleCode.Bold, StyleCode.Dim, StyleCode.Italic, StyleCode.Underlined, StyleCode.Blink, StyleCode.Inverted, StyleCode.CrossedOut };
}

public enum StyleCode : byte
{
	None		= 0,
	Bold		= 1,
	Dim			= 2,
	Italic		= 4,
	Underlined	= 8,
	Blink		= 16,
	Inverted	= 32,
	CrossedOut	= 64
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
	
	public static byte GetCode(this StyleCode Style) => Style switch
	{
		StyleCode.Bold			=> 1,
		StyleCode.Dim			=> 2,
		StyleCode.Italic		=> 3,
		StyleCode.Underlined	=> 4,
		StyleCode.Blink			=> 5,
		StyleCode.Inverted		=> 7,
		StyleCode.CrossedOut	=> 9,

		StyleCode.None			=> 0,
		_						=> 0
	};
	
	public static byte GetResetCode(this StyleCode Style) => Style switch
	{
		StyleCode.Bold			=> 22,
		StyleCode.Dim			=> 22,
		StyleCode.Italic		=> 23,
		StyleCode.Underlined	=> 24,
		StyleCode.Blink			=> 25,
		StyleCode.Inverted		=> 27,
		StyleCode.CrossedOut	=> 29,

		StyleCode.None			=> 0,
		_						=> 0
	};
}