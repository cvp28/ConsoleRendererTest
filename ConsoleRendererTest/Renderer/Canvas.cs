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
	/// <param name="PresizeBuffers">Sizes some of the internally-used lists and buffers to a preset capacity before that capacity is needed (during Canvas construction), instead of dynamically WHEN it is needed</param>
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
		
		// Set up the screen state (set screen to full white on black and reset cursor to home)
		Console.Write($"\u001b[38;2;255;255;255m\u001b[48;2;0;0;0m{new string(' ', Width * Height)}\u001b[;H");
		
		// Set up render state
		LastPixel = new()
		{
			Character = ' ',
			Foreground = new(255,255,255),
			Background = new(0,0,0),
			Style = 0
		};
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int IX(int X, int Y) => (Y * Width + X) % CurrentFramePixels.Length; // This effectively wraps-around when input parameters go out-of-bounds
	
	public void DoBufferDump(int Quantity)
	{
		BufferDumpQuantity = Quantity;
		File.AppendAllText(@".\BufferDump.txt", "Buffer Dump\n\n");
	}
	
	public void Flush()
	{
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
			if (BufferDumpQuantity > 0)
			{
				File.AppendAllText(@"BufferDump.txt", Buffer.ToString() + "\n\n");
				BufferDumpQuantity--;
			}
			
			byte[] FinalFrame = Encoding.UTF8.GetBytes(Buffer.ToString());
			PlatformWriteStdout(FinalFrame);
			ModifiedIndices.Clear();
		}
		
	}
	
	private int LastIndex = 0;
	private int LastY = 0;
	private Pixel LastPixel;

	// Only set to true at the start of the application
	// (in which case LastPixel does not represent valid data that is actually on the screen)

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
			
			// First, handle position
			if (i - LastIndex != 1)
				Buffer.Append("\u001b[").Append(Y + 1).Append(';').Append(X + 1).Append("H");
			
			// Then, handle colors and styling
			if (CurrentPixel.Foreground != LastPixel.Foreground)
				CurrentPixel.Foreground.AsForegroundVT(ref Buffer);
			
			if (CurrentPixel.Background != LastPixel.Background)
				CurrentPixel.Background.AsBackgroundVT(ref Buffer);
			
			if (CurrentPixel.Style != LastPixel.Style)
			{
				(byte ResetMask, byte SetMask) = MakeStyleTransitionMasks(LastPixel.Style, CurrentPixel.Style);
				AppendStyleTransitionSequence(ResetMask, SetMask);
			}
			
			Buffer.Append(CurrentPixel.Character);
			
			// Write our modifications back to the array
			CurrentFramePixels[i] = CurrentPixel;
			LastFramePixels[i] = CurrentPixel;
			
			// Store this state for future reference
			LastIndex = i;
			LastY = Y;
			LastPixel = CurrentPixel;
		}
	}
	
	/// <summary>
	/// <para>Takes two style masks: a current one and a new one and produces</para>
	/// <br/>
	/// <para>a reset mask (all of the styles that need to be reset)</para>
	/// <br/>
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
				Buffer.Append(Codes[i].GetCode());
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
	/// <br/>
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

