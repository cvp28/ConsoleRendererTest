using System.Text;
using System.Runtime.CompilerServices;

using Collections.Pooled;

namespace ConsoleRendererTest;
using Codes;

public unsafe partial class Canvas
{
	private int Width;
	private int Height;
	
	private Pixel[] CurrentFramePixels;
	private Pixel[] LastFramePixels;
	private StringBuilder Buffer;

	private PooledList<int> ModifiedIndices;
	
	private Action<byte[]> PlatformWriteStdout;

	public int BufferDumpQuantity = 0;
	
	/// <summary>
	/// Creates a terminal canvas
	/// </summary>
	/// <param name="PresizeBuffers">Sizes some of the internally-used lists and buffers to a preset capacity before that capacity is needed (during Canvas construction), instead of dynamically when it IS needed</param>
	public Canvas(bool PresizeBuffers = false)
	{
		Width = Console.WindowWidth;
		Height = Console.WindowHeight;
		
		CurrentFramePixels = new Pixel[Width * Height];
		LastFramePixels = new Pixel[Width * Height];
		
		Buffer = new(1024);
		
		if (PresizeBuffers)
			ModifiedIndices = new(1024, false);
		else
			ModifiedIndices = [];
		
		if (OperatingSystem.IsWindows())
		{
			var Handle = Kernel32.GetStdHandle(-11);
			
			PlatformWriteStdout = delegate (byte[] Buffer)
			{
				Kernel32.WriteFile(Handle, Buffer, (uint) Buffer.Length, out _, nint.Zero);
			};
		}
		else if (OperatingSystem.IsLinux())
		{
			PlatformWriteStdout = delegate (byte[] Buffer)
			{
				fixed (byte* ptr = Buffer)
					Libc.Write(1, ptr, (uint) Buffer.Length);
			};
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int IX(int X, int Y) => (Y * Width + X) % CurrentFramePixels.Length; // This effectively wraps-around when input parameters go out-of-bounds
	
	private bool BufferDumpEnabled = false;
	
	public void DoBufferDump(int Quantity)
	{
		BufferDumpQuantity = Quantity;
		BufferDumpEnabled = true;
		
		File.AppendAllText(@".\BufferDump.txt", "Buffer Dump\n\n");
	}
	
	public void Flush()
	{
		if (BufferDumpQuantity > 0)
			File.AppendAllText(@".\BufferDump.txt", $"Frame {BufferDumpQuantity}\n");

		if (ModifiedIndices.Count != 0)
		{
			Buffer.Clear();
			RenderModifiedPixels();
		}
		else
		{
			return;
		}
		
		// Draw step
		if (Buffer.Length > 0)
		{
			byte[] FinalFrame = Encoding.UTF8.GetBytes(Buffer.ToString());
			PlatformWriteStdout(FinalFrame);
			ModifiedIndices.Clear();
		}
		
	}

	private Pixel LastPixel = new();

	// Only set to true at the start of the application
	// (in which case LastPixel does not represent valid data that is actually on the screen)
	private bool LastPixelInvalid = true;

	private void RenderModifiedPixels()
	{
		if (ModifiedIndices.Count == 0)
			return;

		ModifiedIndices.Sort();

		foreach (var i in ModifiedIndices.Span)
		{
			// Nifty little way of converting indices to coordinates
			(int Y, int X) = Math.DivRem(i, Width);

			var CurrentPixel = CurrentFramePixels[i];

			if (LastPixelInvalid)
			{
				Buffer.Append($"\u001b[{Y + 1};{X + 1}H");							// Write coordinates
				CurrentPixel.Background.AsBackgroundVT(ref Buffer);					// Write background
				CurrentPixel.Foreground.AsForegroundVT(ref Buffer);					// Write foreground
				if (CurrentPixel.Styled) AppendSetSequence(CurrentPixel.Style);		// Write style (if set)
				Buffer.Append(CurrentPixel.Character);								// Write character

				LastPixelInvalid = false;											// Finally, set to no longer be invalid
			}
			
			
		}
	}

	private void RenderModifiedPixels2()
	{
		//	if (ModifiedIndices.Count == 0)
		//		return;
		//	
		//	ModifiedIndices.Sort();
		//	
		//	int X = ModifiedIndices[0] % Width;
		//	int Y = ModifiedIndices[0] / Width;
		//	
		//	int LastIndex = ModifiedIndices[0];
		//	Pixel LastPixel = CurrentFramePixels[LastIndex];
		//	Pixel CurrentPixel = CurrentFramePixels[LastIndex];
		//	
		//	Buffer.Append($"\u001b[{Y + 1};{X + 1}H");
		//	Buffer.Append(LastPixel.Background.AsBackgroundVT());
		//	Buffer.Append(LastPixel.Foreground.AsForegroundVT());
		//	
		//	if (LastPixel.Styled)
		//		AppendSetSequence(LastPixel.Style);
		//	
		//	Buffer.Append(LastPixel.Character);
		//	LastFramePixels[ModifiedIndices[0]] = LastPixel;
		//	
		//	if (ModifiedIndices.Count == 1)
		//		return;
		//	
		//	foreach (var i in ModifiedIndices.Span.Slice(1))
		//	{
		//		X = i % Width;
		//		Y = i / Width;
		//		CurrentPixel = CurrentFramePixels[i];
		//		
		//		if (i - LastIndex != 1)
		//			Buffer.Append($"\u001b[{Y + 1};{X + 1}H");
		//		
		//		if (CurrentPixel.Background != LastPixel.Background)
		//			Buffer.Append(CurrentPixel.Background.AsBackgroundVT());
		//		
		//		if (CurrentPixel.Foreground != LastPixel.Foreground)
		//			Buffer.Append(CurrentPixel.Foreground.AsForegroundVT());
		//		
		//		if (LastPixel.Style != CurrentPixel.Style)
		//		{
		//			(byte ResetMask, byte SetMask) = MakeStyleTransitionMasks(LastPixel.Style, CurrentPixel.Style);
		//			AppendStyleTransitionSequence(ResetMask, SetMask);
		//		}
		//		
		//		Buffer.Append(CurrentFramePixels[i].Character);
		//		
		//		LastFramePixels[i] = CurrentFramePixels[i];
		//		
		//		LastPixel = CurrentPixel;
		//	}
		//	
		//	if (CurrentPixel.Style != 0)
		//		AppendResetSequence(CurrentPixel.Style);
	}
	
	/// <summary>
	/// <para>Takes two style masks: a current one and a new one and produces</para>
	/// <br>
	/// <para>a reset mask (all of the styles that need to be reset)</para>
	/// <br>
	/// <para>and a set mask (all of the styles that need to be set)</para>
	/// </summary>
	/// <param name="CurrentStyle">The currently applied style mask (all styles that have been flushed to the screen)</param>
	/// <param name="NewStyle">The desired style mask to be applied</param>
	/// <returns></returns>
	private static (byte ResetMask, byte SetMask) MakeStyleTransitionMasks(byte CurrentStyle, byte NewStyle)
	{
		byte r = (byte) (~NewStyle & CurrentStyle);
		byte s = (byte) ((NewStyle | r) ^ CurrentStyle);
		
		return (r, s);
	}
	
	private void AppendStyleTransitionSequence(byte ResetMask, byte SetMask)
	{
		Buffer.Append("\u001b[");

		//	// Process reset mask
		//	
		//	if ((ResetMask & StyleCode.Bold.GetMask()) >= 1 || (ResetMask & StyleCode.Dim.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)ResetCode.NormalIntensity};");
		//	
		//	if ((ResetMask & StyleCode.Italic.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)ResetCode.NotItalicised};");
		//	
		//	if ((ResetMask & StyleCode.Underlined.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)ResetCode.NotUnderlined};");
		//	
		//	if ((ResetMask & StyleCode.Blink.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)ResetCode.NotBlinking};");
		//	
		//	if ((ResetMask & StyleCode.Inverted.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)ResetCode.NotInverted};");
		//	
		//	if ((ResetMask & StyleCode.CrossedOut.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)ResetCode.NotCrossedOut};");
		//	
		//	// Now process set mask
		//	
		AppendResetSequence(ResetMask);

		if (SetMask == 0)
			goto end;

		AppendSetSequence(SetMask);

		//	
		//	if ((SetMask & StyleCode.Bold.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)StyleCode.Bold};");
		//	
		//	if ((SetMask & StyleCode.Dim.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)StyleCode.Dim};");
		//	
		//	if ((SetMask & StyleCode.Italic.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)StyleCode.Italic};");
		//	
		//	if ((SetMask & StyleCode.Underlined.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)StyleCode.Underlined};");
		//	
		//	if ((SetMask & StyleCode.Blink.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)StyleCode.Blink};");
		//	
		//	if ((SetMask & StyleCode.Inverted.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)StyleCode.Inverted};");
		//	
		//	if ((SetMask & StyleCode.CrossedOut.GetMask()) >= 1)
		//		Buffer.Append($"{(byte)StyleCode.CrossedOut};");

	end:

		Buffer.Remove(Buffer.Length - 1, 1);
		Buffer.Append('m');
	}
	
	public void AppendResetSequence(byte ResetMask)
	{
		Buffer.Append("\u001b[");
		
		if ((ResetMask & StyleCode.Bold.GetMask()) >= 1 || (ResetMask & StyleCode.Dim.GetMask()) >= 1)
		{
			Buffer.Append((byte) ResetCode.NormalIntensity);
			Buffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.Italic.GetMask()) >= 1)
		{
			Buffer.Append((byte) ResetCode.NotItalicised);
			Buffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.Underlined.GetMask()) >= 1)
		{
			Buffer.Append((byte) ResetCode.NotUnderlined);
			Buffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.Blink.GetMask()) >= 1)
		{
			Buffer.Append((byte) ResetCode.NotBlinking);
			Buffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.Inverted.GetMask()) >= 1)
		{
			Buffer.Append((byte) ResetCode.NotInverted);
			Buffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.CrossedOut.GetMask()) >= 1)
		{
			Buffer.Append((byte) ResetCode.NotCrossedOut);
			Buffer.Append(';');
		}
		
		//	Buffer.Remove(Buffer.Length - 1, 1);
		//	Buffer.Append('m');
	}
	
	/// <summary>
	/// Produces a single VT escape sequence of style codes chained together using a bitmask
	/// </summary>
	/// <param name="SetMask">The mask to set</param>
	/// <returns>The resulting VT escape sequence</returns>
	public void AppendSetSequence(byte SetMask)
	{
		Buffer.Append("\u001b[");

		Span<StyleCode> Codes = stackalloc StyleCode[8];

		StyleHelper.UnpackStyle(SetMask, ref Codes, out int Count);

		for (int i = 0; i < Count; i++)
			if ((SetMask & Codes[i].GetMask()) >= 1)
			{
				Buffer.Append((byte)Codes[i]);
				Buffer.Append(';');
			}

		//	if ((SetMask & StyleCode.Bold.GetMask()) >= 1)
		//	{
		//		Buffer.Append((byte) StyleCode.Bold);
		//		Buffer.Append(';');	
		//	}
		//	
		//	if ((SetMask & StyleCode.Dim.GetMask()) >= 1)
		//	{
		//		Buffer.Append((byte) StyleCode.Dim);
		//		Buffer.Append(';');
		//	}
		//	
		//	if ((SetMask & StyleCode.Italic.GetMask()) >= 1)
		//	{
		//		Buffer.Append((byte) StyleCode.Italic);
		//		Buffer.Append(';');
		//	}
		//	
		//	if ((SetMask & StyleCode.Underlined.GetMask()) >= 1)
		//	{
		//		Buffer.Append((byte) StyleCode.Underlined);
		//		Buffer.Append(';');
		//	}
		//	
		//	if ((SetMask & StyleCode.Blink.GetMask()) >= 1)
		//	{
		//		Buffer.Append((byte) StyleCode.Blink);
		//		Buffer.Append(';');
		//	}
		//	
		//	if ((SetMask & StyleCode.Inverted.GetMask()) >= 1)
		//	{
		//		Buffer.Append((byte) StyleCode.Inverted);
		//		Buffer.Append(';');
		//	}
		//	
		//	if ((SetMask & StyleCode.CrossedOut.GetMask()) >= 1)
		//	{
		//		Buffer.Append((byte) StyleCode.CrossedOut);
		//		Buffer.Append(';');
		//	}
		
		//Buffer.Remove(Buffer.Length - 1, 0);
		//Buffer.Append('m');
	}
}

public enum CanvasRenderMode
{
	Parallel,
	SynchronousRowIteration,
	OnlyModifiedPixels
}

public static class StyleHelper
{
	public static byte MakeStyle(bool Bold, bool Dim, bool Italic, bool Underlined, bool Blink, bool Inverted, bool CrossedOut)
	{
		byte Temp = 0;
		
		if (Bold)
			Temp |= 0b00000001;
		
		if (Dim)
			Temp |= 0b00000010;
		
		if (Italic)
			Temp |= 0b00000100;
		
		if (Underlined)
			Temp |= 0b00001000;
		
		if (Blink)
			Temp |= 0b00010000;
		
		if (Inverted)
			Temp |= 0b00100000;
		
		if (CrossedOut)
			Temp |= 0b01000000;
		
		return Temp;
	}
	
	// Packs an array of stylecode enums into a single byte
	public static byte PackStyle(params StyleCode[] StyleCodes)
	{
		byte Temp = 0;

		for (int i = 0; i < StyleCodes.Length; i++)
			Temp = (byte) (Temp | StyleCodes[i].GetMask());

		return Temp;
	}

	/// <summary>
	/// <para>Gets every stylecode enum contained in the packed byte and returns them in "Dest"</para>
	/// <para>This allows client code to easily prevent heap allocations from parsing packed styles</para>
	/// <br>
	/// <para>(Dest should be a minimum size of 8)</para>
	/// </summary>
	/// <param name="PackedStyle">Packed style byte</param>
	/// <param name="Dest">Unpacked style codes</param>
	/// <param name="Length">Count of style codes that were written to the span</param>
	public static void UnpackStyle(byte PackedStyle, ref Span<StyleCode> Dest, out int Length)
	{
		if (Dest.Length < 8)
		{
			Length = 0;
			return;
		}
		
		int Index = 0;
		
		for (int i = 0; i < 7; i++)
			if ((PackedStyle & Globals.AllStyles[i].GetMask()) >= 1)
			{
				Dest[Index] = Globals.AllStyles[i];
				Index++;
			}
		
		Length = Index;
	}
}