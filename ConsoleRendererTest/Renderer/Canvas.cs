using System.Buffers;
using System.Diagnostics;
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
	private PooledDictionary<int, Pixel> IndexUpdates = new(1000, ClearMode.Never);
	
	private Thread RenderThread;
	private Thread WriteThread;

	public bool DoRender = false;

	private Action<ReadOnlyMemory<byte>> PlatformWriteStdout;

	public int BufferDumpQuantity = 0;

	public Canvas()
	{
		Width = Console.WindowWidth;
		Height = Console.WindowHeight;
		
		PrecacheSequences();
		
		#region Platform-specific stdout write delegates
		if (OperatingSystem.IsWindows())
		{
			var Handle = Kernel32.GetStdHandle(-11);
			
			PlatformWriteStdout = delegate (ReadOnlyMemory<byte> Buffer)
			{
				using var hBuffer = Buffer.Pin();
				Kernel32.WriteFile(Handle, (byte*) hBuffer.Pointer, (uint) Buffer.Length, out _, nint.Zero);
			};
		}
		else if (OperatingSystem.IsLinux())
		{
			PlatformWriteStdout = delegate (ReadOnlyMemory<byte> Buffer)
			{
				using var hBuffer = Buffer.Pin();
				Libc.Write(1, (byte*) hBuffer.Pointer, (uint) Buffer.Length);
			};
		}
		#endregion
		
		RenderThread = new(RenderThreadProc)
		{
			Name = "Render Thread",
			IsBackground = true,
			Priority = ThreadPriority.AboveNormal
		};

		WriteThread = new(WriteThreadProc)
		{
			Name = "Write Thread",
			IsBackground = true,
			Priority = ThreadPriority.Normal
		};
		
		RenderThread.Start();
		WriteThread.Start();
		
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
		
		FrontBuffer.ToDraw.Clear();
		FrontBuffer.ToClear.Clear();
		FrontBuffer.ToSkip.Clear();
		
		BackBuffer.ToDraw.Clear();
		BackBuffer.ToClear.Clear();
		BackBuffer.ToSkip.Clear();

		InitScreen();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int ScreenIX(int X, int Y) => ((Y % Height) * Width + (X % Width));// % TotalCellCount; // This effectively wraps-around when input parameters go out-of-bounds

	public void DoBufferDump(int Quantity)
	{
		BufferDumpQuantity = Quantity;
		if (File.Exists(@".\BufferDump.txt"))
			File.Delete(@".\BufferDump.txt");

		File.AppendAllText(@".\BufferDump.txt", "Buffer Dump\n\n");
	}
	
	public double MainThreadWaitMs { get; private set; }
	public double RenderThreadWaitMs { get; private set; }
	public double WriteThreadWaitMs { get; private set; }
	
	public void Flush()
	{
		// Time our wait for the render thread to finish
		var start = Stopwatch.GetTimestamp();
		while (DoRender) Thread.Yield();
		MainThreadWaitMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
		
		FrameBuffers.Swap();
		
		DoRender = true;
		
		BackBuffer.ToClear.Clear();
		
		// Accessing FrontBuffer from the main thread would otherwise be wrong
		// But, we are only reading from it here - so it should be fine
		
		foreach (var p in FrontBuffer.ToSkip)
			BackBuffer.ToClear[p.Index] = p;
		
		foreach (var p in FrontBuffer.ToDraw)
			BackBuffer.ToClear[p.Index] = p;
		
		BackBuffer.ToDraw.Clear();
		BackBuffer.ToSkip.Clear();
	}
	
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void RenderPixels(ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		var clearEnumerator = FrontBuffer.ToClear.Keys.GetEnumerator();
		//var drawEnumerator = FrontBuffer.ToDraw.GetEnumerator();

		while (clearEnumerator.MoveNext())	
			RenderClearPixel(clearEnumerator.Current, ref writer);

		for (int i = 0; i < FrontBuffer.ToDraw.Span.Length; i++)
		{
			var NewPixel = FrontBuffer.ToDraw.Span[i];
			RenderPixel(ref NewPixel, ref writer);
		}
	}
	
	private DoubleRenderBuffer FrameBuffers = new(300);
	
	private RenderBuffer BackBuffer => FrameBuffers.BackBuffer;
	private RenderBuffer FrontBuffer => FrameBuffers.FrontBuffer;

	private void RenderThreadProc()
	{
	loop_start:
		var start = Stopwatch.GetTimestamp();
		while (!DoRender) Thread.Yield();
		RenderThreadWaitMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

		DoubleWriteBuffer.BackBuffer = Utf8String.CreateWriter(out var writer);
		
		RenderPixels(ref writer);
		
		writer.Flush(); // Fill buffer with writer contents
		writer.Dispose();

		while (DoWrite) Thread.Yield();

		DoubleWriteBuffer.Swap();

		DoWrite = true;

		//if (FinalFrame.WrittenCount != 0)
		//	PlatformWriteStdout(FinalFrame.WrittenMemory);
		
		//FinalFrame.Dispose();
		
		DoRender = false;
		goto loop_start;
	}

	private bool DoWrite = false;
	private DoubleFrameBuffer DoubleWriteBuffer = new();

	private Utf8StringBuffer FrontWriteBuffer => DoubleWriteBuffer.FrontBuffer;
	private Utf8StringBuffer BackWriteBuffer => DoubleWriteBuffer.BackBuffer;

	private void WriteThreadProc()
	{
	loop_start:
		var start = Stopwatch.GetTimestamp();
		while (!DoWrite) Thread.Yield();
		WriteThreadWaitMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

		if (FrontWriteBuffer.WrittenCount != 0)
			PlatformWriteStdout(FrontWriteBuffer.WrittenMemory);

		DoubleWriteBuffer.DisposeFrontBuffer();

		DoWrite = false;
		goto loop_start;
	}

	private Pixel LastPixel;
	
	private int GetY(int Index) => Index / Width;
	
	//[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void RenderPixel(ref Pixel NewPixel, ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		var LastIndex = LastPixel.Index;
		var CurrentIndex = NewPixel.Index;
		
		// First, handle position
		if (CurrentIndex - LastIndex != 1)
		{
			if (GetY(LastIndex) == GetY(CurrentIndex) && CurrentIndex > LastIndex)
			{
				int count = CurrentIndex - LastIndex - 1;
				writer.AppendFormat($"\u001b[{count}C");            // If indices are on same line and spaced further than 1 cell apart, shift right
			}
			else
			{
				(int Y, int X) = Math.DivRem(CurrentIndex, Width);
				writer.AppendFormat($"\u001b[{Y + 1};{X + 1}H");    // If anywhere else, set absolute position
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
			
			writer.Append(StyleTransitionSequences[ResetMask * SetMask]);
			
			//AppendStyleTransitionSequence(ResetMask, SetMask, ref writer);
		}
		
		writer.Append(NewPixel.Character);
		LastPixel = NewPixel;
	}

	//[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void RenderClearPixel(int CurrentIndex, ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		var LastIndex = LastPixel.Index;
		//var CurrentIndex = NewPixel.Index;
		
		// First, handle position
		if (CurrentIndex - LastIndex != 1)
		{
			if (GetY(LastIndex) == GetY(CurrentIndex) && CurrentIndex > LastIndex)
			{
				int count = CurrentIndex - LastIndex - 1;
				writer.AppendFormat($"\u001b[{count}C");            // If indices are on same line and spaced further than 1 cell apart, shift right
			}
			else
			{
				(int Y, int X) = Math.DivRem(CurrentIndex, Width);
				writer.AppendFormat($"\u001b[{Y + 1};{X + 1}H");    // If anywhere else, set absolute position
			}
		}
		
		writer.Append(' ');
		
		LastPixel.Index = CurrentIndex;
		
		//LastPixel = new Pixel()
		//{
		//	Index = CurrentIndex,
		//	Character = ' ',
		//	Foreground = Color24.White,
		//	Background = Color24.Black,
		//	Style = 0
		//};
	}
	
	private string[] StyleTransitionSequences = new string[65536];

	private void PrecacheSequences()
	{
		for (int r = 0; r < 256; r++)
			for (int s = 0; s < 256; s++)
				StyleTransitionSequences[r * s] = GetStyleTransitionSequence((byte) r, (byte) s);
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

	private string GetStyleTransitionSequence(byte ResetMask, byte SetMask)
	{
		string temp = "\u001b[";

		if (ResetMask != 0)
			temp += GetResetSequence(ResetMask);

		temp += GetSetSequence(SetMask);
		temp += 'm';

		return temp;
	}

	private string GetSetSequence(byte SetMask)
	{
		string temp = "\u001b[";

		Span<StyleCode> Codes = stackalloc StyleCode[8];

		StyleHelper.UnpackStyle(SetMask, ref Codes, out int Count);

		for (int i = 0; i < Count; i++)
			if ((SetMask & Codes[i].GetMask()) >= 1)
			{
				temp += $"{Codes[i].GetCode()}";
				if (i != Count - 1) temp += ';';
			}

		return temp;
	}

	private string GetResetSequence(byte ResetMask)
	{
		string temp = "\u001b[";

		Span<StyleCode> Codes = stackalloc StyleCode[8];

		StyleHelper.UnpackStyle(ResetMask, ref Codes, out int Count);

		for (int i = 0; i < Count; i++)
			if ((ResetMask & Codes[i].GetMask()) >= 1)
			{
				temp += $"{Codes[i].GetResetCode()}";
				if (i != Count - 1) temp += ';';
			}

		return temp;
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