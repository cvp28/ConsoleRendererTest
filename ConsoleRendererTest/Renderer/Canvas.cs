using System.Text;
using System.Runtime.CompilerServices;

using Collections.Pooled;

namespace SharpCanvas;
using Interop;
using Codes;
using System.Security.Cryptography.X509Certificates;

public unsafe partial class Canvas
{
	public int Width { get; private set; }
	public int Height { get; private set; }
	
	// General purpose pixel buffers
	private HashSet<Pixel> PreviousPixelBuffer = new(1024);
	private HashSet<Pixel> CurrentPixelBuffer = new(1024);
	
	// Constructed from reconciling the current and previous frames
	private PooledList<Pixel> FinalPixelBuffer = new(1024, false);
	
	// Final string buffer containing writeable data
	private StringBuilder FinalWriteBuffer = new(1024);
	
	private Action<byte[]> PlatformWriteStdout;

	public int BufferDumpQuantity = 0;
	
	/// <summary>
	/// Creates a terminal canvas
	/// </summary>
	/// <param name="PresizeBuffers">Sizes some of the internally-used lists and buffers to a preset capacity before that capacity is needed (during Canvas construction), instead of dynamically WHEN it is needed</param>
	public Canvas()
	{
		Width = Console.WindowWidth;
		Height = Console.WindowHeight;
		
		#region Platform-specific stdout write delegates
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
		#endregion
		
		// Set up the screen state (set screen to full white on black and reset cursor to home)
		Console.Write($"\u001b[38;2;255;255;255m\u001b[48;2;0;0;0m{new string(' ', Width * Height)}\u001b[;H");
		
		// Set up render state
		LastPixel = new()
		{
			Index = 0,
			Character = ' ',
			Foreground = new(255,255,255),
			Background = new(0,0,0),
			Style = 0
		};
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int ScreenIX(int X, int Y) => (Y * Width + X) % (Width * Height); // This effectively wraps-around when input parameters go out-of-bounds
	
	public void DoBufferDump(int Quantity)
	{
		BufferDumpQuantity = Quantity;
		if (File.Exists(@".\BufferDump.txt"))
			File.Delete(@".\BufferDump.txt");
		
		File.AppendAllText(@".\BufferDump.txt", "Buffer Dump\n\n");
	}
	
	public void Clear()
	{
		//	Array.Copy(ClearPixels, CurrentFramePixels, CurrentFramePixels.Length);
		//	Array.Copy(ClearPixels, LastFramePixels, LastFramePixels.Length);
		//	Console.Clear();
	}
	
	public void Flush()
	{
		if (CurrentPixelBuffer.Count != 0)
		{
			ReconcileFrames();
			RenderModifiedPixels();
		}
		else
		{
			return;
		}
		
		// Draw step
		if (FinalWriteBuffer.Length > 0)
		{	
			if (BufferDumpQuantity > 0)
			{
				File.AppendAllText(@"BufferDump.txt", FinalWriteBuffer.ToString() + "\n\n");
				BufferDumpQuantity--;
			}
			
			byte[] FinalFrame = Encoding.UTF8.GetBytes(FinalWriteBuffer.ToString());
			PlatformWriteStdout(FinalFrame);
			
			FinalPixelBuffer.Clear();
			FinalWriteBuffer.Clear();
		}
	}
	
	private int SortPixelsByIndices(Pixel x, Pixel y) => x.Index.CompareTo(y.Index);
	
	private void ReconcileFrames()
	{
		if (!PreviousPixelBuffer.Any())
		{
			//	If there is no data on the screen to diff against, submit all writes to the renderer
			foreach (var p in CurrentPixelBuffer)
			{
				PreviousPixelBuffer.Add(p);
				FinalPixelBuffer.Add(p);
			}
			return;
		}

		static int IndexSelector(Pixel i) => i.Index;

		var PreviousIndices = PreviousPixelBuffer.Select(IndexSelector);
		var CurrentIndices = CurrentPixelBuffer.Select(IndexSelector);

		var DataToKeep = PreviousPixelBuffer.Intersect(CurrentPixelBuffer).ToPooledList();
		var DataToClear = PreviousPixelBuffer.ExceptBy(CurrentIndices, IndexSelector).ToPooledList();
		var DataToWrite = CurrentPixelBuffer.ExceptBy(PreviousIndices, IndexSelector).ToPooledList();

		CurrentPixelBuffer.Clear();

		PreviousPixelBuffer.ExceptWith(DataToClear);				// Remove cleared data from screen state
		foreach (var p in DataToWrite) PreviousPixelBuffer.Add(p);	// Add written data to screen state

		for (int i = 0; i < DataToClear.Count; i++)
		{
			var temp = DataToClear[i];

			temp.Character = ' ';
			
			if (temp.Background != Color24.Black)
			{
				temp.Foreground = Color24.White;
				temp.Background = Color24.Black;
			}

			temp.Style = 0;

			DataToClear[i] = temp;
		}

		FinalPixelBuffer.AddRange(DataToClear.Span);
		FinalPixelBuffer.AddRange(DataToWrite.Span);

	}
	
	private int LastIndex = 0;
	private Pixel LastPixel;
	
	private void RenderModifiedPixels()
	{
		if (FinalPixelBuffer.Count == 0)
			return;
		
		//FinalPixelBuffer.Span.Sort((x, y) => x.Index.CompareTo(y.Index));
		
		for(int idx = 0; idx < FinalPixelBuffer.Span.Length; idx++)
		{
			var i = FinalPixelBuffer[idx].Index;
			
			// Nifty little way of converting indices to coordinates
			(int Y, int X) = Math.DivRem(i, Width);
			
			var CurrentPixel = FinalPixelBuffer.Span[idx];
			
			// First, handle position
			if (i - LastIndex != 1)
			{
				if (GetY(LastIndex) == GetY(i) && i > LastIndex)
					FinalWriteBuffer.Append("\u001b[").Append(i - LastIndex - 1).Append("C");					// If indices are on same line and spaced further than 1 cell apart, shift right
				else
					FinalWriteBuffer.Append("\u001b[").Append(Y + 1).Append(';').Append(X + 1).Append("H");		// If anywhere else, set cursor pos
			}
			
			// Then, handle colors and styling
			if (CurrentPixel.Foreground != LastPixel.Foreground)
				CurrentPixel.Foreground.AsForegroundVT(ref FinalWriteBuffer);
			
			if (CurrentPixel.Background != LastPixel.Background)
				CurrentPixel.Background.AsBackgroundVT(ref FinalWriteBuffer);
			
			if (CurrentPixel.Style != LastPixel.Style)
			{
				(byte ResetMask, byte SetMask) = MakeStyleTransitionMasks(LastPixel.Style, CurrentPixel.Style);
				AppendStyleTransitionSequence(ResetMask, SetMask);
			}
			
			FinalWriteBuffer.Append(CurrentPixel.Character);
			
			// Store this state for future reference
			LastIndex = i;
			LastPixel = CurrentPixel;
		}
	}
	
	private int GetY(int Index) => Index / Width;
	
	/// <summary>
	/// Resizes the canvas.
	/// </summary>
	/// <param name="NewWidth">The new width</param>
	/// <param name="NewHeight">The new height</param>
	public void Resize(int NewWidth = 0, int NewHeight = 0)
	{
		if (NewWidth == 0)
			NewWidth = Console.WindowWidth;
		
		if (NewHeight == 0)
			NewHeight = Console.WindowHeight;
		
		Width = NewWidth;
		Height = NewHeight;
		
		Console.Clear();
		PreviousPixelBuffer.Clear();
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
		FinalWriteBuffer.Append("\u001b[");
		
		if (ResetMask != 0)
			AppendResetSequence(ResetMask);
		
		if (SetMask != 0)
			AppendSetSequence(SetMask);

		FinalWriteBuffer.Remove(FinalWriteBuffer.Length - 1, 1);
		FinalWriteBuffer.Append('m');
	}
	
	public void AppendResetSequence(byte ResetMask)
	{
		FinalWriteBuffer.Append("\u001b[");
		
		if ((ResetMask & StyleCode.Bold.GetMask()) >= 1 || (ResetMask & StyleCode.Dim.GetMask()) >= 1)
		{
			FinalWriteBuffer.Append((byte) ResetCode.NormalIntensity);
			FinalWriteBuffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.Italic.GetMask()) >= 1)
		{
			FinalWriteBuffer.Append((byte) ResetCode.NotItalicised);
			FinalWriteBuffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.Underlined.GetMask()) >= 1)
		{
			FinalWriteBuffer.Append((byte) ResetCode.NotUnderlined);
			FinalWriteBuffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.Blink.GetMask()) >= 1)
		{
			FinalWriteBuffer.Append((byte) ResetCode.NotBlinking);
			FinalWriteBuffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.Inverted.GetMask()) >= 1)
		{
			FinalWriteBuffer.Append((byte) ResetCode.NotInverted);
			FinalWriteBuffer.Append(';');
		}
		
		if ((ResetMask & StyleCode.CrossedOut.GetMask()) >= 1)
		{
			FinalWriteBuffer.Append((byte) ResetCode.NotCrossedOut);
			FinalWriteBuffer.Append(';');
		}
	}
	
	/// <summary>
	/// Produces a single VT escape sequence of style codes chained together using a bitmask
	/// </summary>
	/// <param name="SetMask">The mask to set</param>
	/// <returns>The resulting VT escape sequence</returns>
	public void AppendSetSequence(byte SetMask)
	{
		FinalWriteBuffer.Append("\u001b[");

		Span<StyleCode> Codes = stackalloc StyleCode[8];

		StyleHelper.UnpackStyle(SetMask, ref Codes, out int Count);

		for (int i = 0; i < Count; i++)
			if ((SetMask & Codes[i].GetMask()) >= 1)
			{
				FinalWriteBuffer.Append(Codes[i].GetCode());
				FinalWriteBuffer.Append(';');
			}
	}
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
			if ((PackedStyle & CodesGlobals.AllStyles[i].GetMask()) >= 1)
			{
				Dest[Index] = CodesGlobals.AllStyles[i];
				Index++;
			}
		
		Length = Index;
	}
}

