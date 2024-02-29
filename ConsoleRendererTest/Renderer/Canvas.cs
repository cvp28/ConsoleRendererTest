using System.Buffers;
using System.Runtime.CompilerServices;

using Collections.Pooled;
using Utf8StringInterpolation;

namespace SharpCanvas;
using Interop;
using Codes;

public unsafe partial class Canvas
{
	public int Width { get; private set; }
	public int Height { get; private set; }

	private int TotalCellCount => Width * Height;
	
	// Contains updated indices for each new frame
	private PooledDictionary<int, Pixel> IndexUpdates = new(ClearMode.Never);
	
	private Thread RenderThread;
	
	public bool DoRender = false;
	
	// Final string buffer containing writeable data
	//private StringBuilder FinalWriteBuffer = new(1024);

	private Action<ReadOnlyMemory<byte>> PlatformWriteStdout;
	
	public int BufferDumpQuantity = 0;
	
	public Canvas()
	{
		Width = Console.WindowWidth;
		Height = Console.WindowHeight;
		
		#region Platform-specific stdout write delegates
		if (OperatingSystem.IsWindows())
		{
			var Handle = Kernel32.GetStdHandle(-11);
			
			PlatformWriteStdout = delegate (ReadOnlyMemory<byte> Buffer)
			{
				fixed (byte* buf = Buffer.Span)
					Kernel32.WriteFile(Handle, buf, (uint) Buffer.Length, out _, nint.Zero);
			};
		}
		else if (OperatingSystem.IsLinux())
		{
			PlatformWriteStdout = delegate (ReadOnlyMemory<byte> Buffer)
			{
				fixed (byte* buf = Buffer.Span)
					Libc.Write(1, buf, (uint) Buffer.Length);
			};
		}
		#endregion

		RenderThread = new(RenderThreadProc)
		{
			Name = "Render Thread"
		};

		RenderThread.Start();
		
		InitScreen();
	}

	private void InitScreen()
	{
		// Set up the screen state (set screen to full white on black and reset cursor to home)
		Console.Write($"\u001b[0m\u001b[38;2;255;255;255m\u001b[48;2;0;0;0m{new string(' ', TotalCellCount)}\u001b[;H");

		// Set up render state
		LastPixel = new()
		{
			Index = 0,
			Character = ' ',
			Foreground = new(255, 255, 255),
			Background = new(0, 0, 0),
			Style = 0
		};

		LastPixel.CalculateHash();
	}
	
	/// <summary>
	/// Resizes the canvas.
	/// </summary>
	/// <param name="NewWidth">The new width</param>
	/// <param name="NewHeight">The new height</param>
	public void Resize(int NewWidth = 0, int NewHeight = 0)
	{
		while (DoRender);
		
		if (NewWidth == 0)
			NewWidth = Console.WindowWidth;
		
		if (NewHeight == 0)
			NewHeight = Console.WindowHeight;
		
		Width = NewWidth;
		Height = NewHeight;

		Console.Clear();

		OldPixels.MainBuffer.Clear();
		OldPixels.SecondaryBuffer.Clear();
		IndexUpdates.Clear();

		InitScreen();
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int ScreenIX(int X, int Y) => (Y * Width + X) % TotalCellCount; // This effectively wraps-around when input parameters go out-of-bounds
	
	public void DoBufferDump(int Quantity)
	{
		BufferDumpQuantity = Quantity;
		if (File.Exists(@".\BufferDump.txt"))
			File.Delete(@".\BufferDump.txt");
		
		File.AppendAllText(@".\BufferDump.txt", "Buffer Dump\n\n");
	}
	
	public void Flush() => ConcurrentFlush();
	
	private PooledSet<Pixel> NewPixels = new(1000, ClearMode.Never);
	private DoubleBuffer<Pixel> OldPixels = new();
	
	public void SynchronousFlush()
	{
		// This line of code will REALLY start to shine when I use eventually build a UI library using this renderer
		if (!IndexUpdates.Any())
			return;
		
		foreach (var p in IndexUpdates.Values) NewPixels.Add(p);
		
		using var FinalFrameTempBuffer = Utf8String.CreateWriter(out var writer);
		
		RenderPixels(ref writer);
		
		writer.Flush();
		
		if (FinalFrameTempBuffer.WrittenCount == 0)
			return;
		
		//byte[] FinalFrame = Encoding.UTF8.GetBytes(FinalWriteBuffer.ToString());
		
		PlatformWriteStdout(FinalFrameTempBuffer.WrittenMemory);
	}
	
	private void ConcurrentFlush()
	{
		// This line of code will REALLY start to shine when I use eventually build a UI library using this renderer
		if (!IndexUpdates.Any())
			return;

		while (DoRender);
		
		foreach (var p in IndexUpdates.Values) NewPixels.Add(p);
		DoRender = true;
		
		IndexUpdates.Clear();
	}
	
	private void RenderPixels(ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		if (!OldPixels.MainBuffer.Any())
		{
			//	If there is no data on the screen to diff against, submit all writes to the renderer
			foreach (var p in NewPixels)
			{
				OldPixels.MainBuffer.Add(p);
				
				var NewPixel = p;

				RenderPixel(ref LastPixel, ref NewPixel, ref writer);
				LastPixel = NewPixel;
			}
			
			NewPixels.Clear();
			return;
		}
		
		var opEnumerator = OldPixels.MainBuffer.GetEnumerator();
		var npEnumerator = NewPixels.GetEnumerator();
		
		while (opEnumerator.MoveNext())
		{
			if (NewPixels.Contains(opEnumerator.Current))
			{
				OldPixels.SecondaryBuffer.Add(opEnumerator.Current);
				continue;
			}
			
			var NewPixel = new Pixel()
			{
				Index = opEnumerator.Current.Index,
				Character = ' ',
				Foreground = Color24.White,
				Background = Color24.Black,
				Style = 0
			};

			RenderPixel(ref LastPixel, ref NewPixel, ref writer);
			LastPixel = NewPixel;
		}

		while (npEnumerator.MoveNext())
		{
			if (!OldPixels.MainBuffer.Contains(npEnumerator.Current))
			{
				var NewPixel = npEnumerator.Current;

				RenderPixel(ref LastPixel, ref NewPixel, ref writer);
				LastPixel = NewPixel;
			}
			
			OldPixels.SecondaryBuffer.Add(npEnumerator.Current);
		}
		
		NewPixels.Clear();
		
		OldPixels.MainBuffer.Clear();
		OldPixels.Swap();
	}
	
	private void RenderThreadProc()
	{
	loop_start:
	
		while (!DoRender);
		
		var FinalFrame = Utf8String.CreateWriter(out var writer);
		
		RenderPixels(ref writer);
		
		writer.Flush();	// Fill buffer with writer contents
		
		if (FinalFrame.WrittenCount == 0)
			goto loop_end;
		
		PlatformWriteStdout(FinalFrame.WrittenMemory);
		
	loop_end:
		FinalFrame.Dispose();
		writer.Dispose();
		
		DoRender = false;
		goto loop_start;
	}
	
	private Pixel LastPixel;
	
	private int GetY(int Index) => Index / Width;

	private void RenderPixel(ref Pixel LastPixel, ref Pixel NewPixel, ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		var LastIndex = LastPixel.Index;
		var CurrentIndex = NewPixel.Index;

		// First, handle position
		if (CurrentIndex - LastIndex != 1)
		{
			if (GetY(LastIndex) == GetY(CurrentIndex) && CurrentIndex > LastIndex)
			{
				int count = CurrentIndex - LastIndex - 1;
				writer.AppendFormat($"\u001b[{count}C"); 			// If indices are on same line and spaced further than 1 cell apart, shift right
			}
			else
			{
				(int Y, int X) = Math.DivRem(CurrentIndex, Width);
				writer.AppendFormat($"\u001b[{Y + 1};{X + 1}H");	// If anywhere else, set absolute position
			}
		}
		
		// Then, handle colors and styling
		if (NewPixel.Foreground != LastPixel.Foreground)
			NewPixel.Foreground.AsForegroundVT(ref writer);
		
		if (NewPixel.Background != LastPixel.Background)
			NewPixel.Background.AsBackgroundVT(ref writer);
		
		if (NewPixel.Style != LastPixel.Style)
		{
			(byte ResetMask, byte SetMask) = MakeStyleTransitionMasks(LastPixel.Style, NewPixel.Style);
			AppendStyleTransitionSequence(ResetMask, SetMask, ref writer);
		}
		
		writer.Append(NewPixel.Character);
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
	
	private void AppendStyleTransitionSequence(byte ResetMask, byte SetMask, ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		writer.Append("\u001b[");
		
		if (ResetMask != 0)
			AppendResetSequence(ResetMask, ref writer);
		
		AppendSetSequence(SetMask, ref writer);
		
		writer.Append('m');
	}
	
	public void AppendResetSequence(byte ResetMask, ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		writer.Append("\u001b[");

		Span<StyleCode> Codes = stackalloc StyleCode[8];

		StyleHelper.UnpackStyle(ResetMask, ref Codes, out int Count);

		for (int i = 0; i < Count; i++)
			if ((ResetMask & Codes[i].GetMask()) >= 1)
			{
				writer.AppendFormat($"{Codes[i].GetResetCode()}");
				if (i != Count - 1) writer.Append(';');
			}
	}
	
	/// <summary>
	/// Produces a single VT escape sequence of style codes chained together using a bitmask
	/// </summary>
	/// <param name="SetMask">The mask to set</param>
	/// <returns>The resulting VT escape sequence</returns>
	public void AppendSetSequence(byte SetMask, ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		writer.Append("\u001b[");

		Span<StyleCode> Codes = stackalloc StyleCode[8];

		StyleHelper.UnpackStyle(SetMask, ref Codes, out int Count);

		for (int i = 0; i < Count; i++)
			if ((SetMask & Codes[i].GetMask()) >= 1)
			{
				writer.AppendFormat($"{Codes[i].GetCode()}");
				if (i != Count - 1) writer.Append(';');
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