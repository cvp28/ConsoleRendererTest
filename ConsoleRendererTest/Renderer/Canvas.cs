using System.Text;
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
	
	private bool _ConcurrentRenderingEnabled;
	private readonly object _RendererLock = new();
	
	public bool ConcurrentRenderingEnabled
	{
		get => _ConcurrentRenderingEnabled;
		
		set
		{
			lock (_RendererLock)
			{
				_ConcurrentRenderingEnabled = value;
				
				Console.Clear();
				
				OldPixels.Clear();
				IndexUpdates.Clear();
				
				if (value)
				{
					AsyncRenderTask.Wait();
					AsyncPixelBuffer.Clear();
					
					RenderPixels = AsyncPixelBuffer;
					FlushDelegate = ConcurrentFlush;
				}
				else
				{
					AsyncRenderTask.Wait();
					AsyncPixelBuffer.Clear();
					
					RenderPixels = DiffedPixels;
					FlushDelegate = SynchronousFlush;
				}
			}
		}
	}
	
	// General purpose pixel buffers
	private PooledList<Pixel> OldPixels = new(1024, false);

	// Contains updated indices for each new frame
	private PooledDictionary<int, Pixel> IndexUpdates = new(1024);

	// Constructed from reconciling the current and previous frames
	private PooledList<Pixel> DiffedPixels = new(1024, false);
	
	private PooledList<Pixel> AsyncPixelBuffer = new(1024, false);
	
	private PooledList<Pixel> RenderPixels;
	
	// Final string buffer containing writeable data
	private StringBuilder FinalWriteBuffer = new(1024);

	private Action<byte[]> PlatformWriteStdout;
	private Action FlushDelegate;
	
	public int BufferDumpQuantity = 0;
	
	/// <summary>
	/// 
	/// </summary>
	/// <param name="EnableConcurrentRendering">Enables concurrent executing of the Flush() method for improved renderer performance</param>
	public Canvas(bool EnableConcurrentRendering = true)
	{
		Width = Console.WindowWidth;
		Height = Console.WindowHeight;
		
		#region Platform-specific stdout write delegates
		if (OperatingSystem.IsWindows())
		{
			var Handle = Kernel32.GetStdHandle(-11);

			PlatformWriteStdout = (byte[] Buffer) => Kernel32.WriteFile(Handle, Buffer, (uint) Buffer.Length, out _, nint.Zero);
		}
		else if (OperatingSystem.IsLinux())
		{
			PlatformWriteStdout = delegate (byte[] Buffer)
			{
				fixed (byte* buf = Buffer)
					Libc.Write(1, buf, (uint) Buffer.Length);
			};
		}
		#endregion
		
		ConcurrentRenderingEnabled = EnableConcurrentRendering;
		
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
	}
	
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

		OldPixels.Clear();
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
	
	public void Clear()
	{
		//	Array.Copy(ClearPixels, CurrentFramePixels, CurrentFramePixels.Length);
		//	Array.Copy(ClearPixels, LastFramePixels, LastFramePixels.Length);
		//	Console.Clear();
	}
	
	public void Flush() { lock(_RendererLock) FlushDelegate(); }
	
	private void SynchronousFlush()
	{
		if (!IndexUpdates.Any())
			return;

		DiffedPixels.Clear();
		ReconcileFrames();
		
		if (DiffedPixels.Count == 0)
			return;
		
		FinalWriteBuffer.Clear();
		RenderModifiedPixels();

		byte[] FinalFrame = Encoding.UTF8.GetBytes(FinalWriteBuffer.ToString());

		PlatformWriteStdout(FinalFrame);
	}
	
	private void ConcurrentFlush()
	{
		// This line of code will REALLY start to shine when I use eventually build a UI library using this renderer
		if (!IndexUpdates.Any())
			return;

		DiffedPixels.Clear();
		ReconcileFrames();
		
		if (DiffedPixels.Count == 0)
			return;

		//	if (BufferDumpQuantity > 0)
		//	{
		//		File.AppendAllText(@"BufferDump.txt", FinalWriteBuffer.ToString() + "\n\n");
		//		BufferDumpQuantity--;
		//	}

		// Wait for previous render to finish
		AsyncRenderTask.Wait();

		RenderPixels.AddRange(DiffedPixels.Span);

		AsyncRenderTask = Task.Run(delegate
		{
			FinalWriteBuffer.Clear();
			RenderModifiedPixels();
			
			byte[] FinalFrame = Encoding.UTF8.GetBytes(FinalWriteBuffer.ToString());
			
			PlatformWriteStdout(FinalFrame);

			RenderPixels.Clear();
		});
	}
	
	private Task AsyncRenderTask = Task.CompletedTask;
	
	private void ReconcileFrames()
	{
		if (!OldPixels.Any())
		{
			//	If there is no data on the screen to diff against, submit all writes to the renderer
			foreach (var p in IndexUpdates.Values)
			{
				OldPixels.Add(p);
				DiffedPixels.Add(p);
			}
			return;
		}

		// Notes:
		// DONT SORT PIXELS EVER :)

		var NewPixels = IndexUpdates.Values;

		int IndexSelector(Pixel p) => p.Index;

		//	Span<Pixel> OldPixelsCopy = stackalloc Pixel[OldPixels.Count];
		//	OldPixels.CopyTo(OldPixelsCopy);
		
		var ToSkip = OldPixels.Intersect(NewPixels);
		//	var ToSkipIndices = ToSkip.Select(IndexSelector);
		//	
		//	for ( int i = 0; i < OldPixels.Span.Length; i++)		// Determine pixels to clear
		//		if (!ToSkipIndices.Contains(OldPixels.Span[i].Index))
		//			DiffedPixels.Add(new()
		//			{
		//				Index = OldPixels.Span[i].Index,
		//				Character = ' ',
		//				Foreground = Color24.White,
		//				Background = Color24.Black,
		//				Style = 0
		//			});
		//	
		//	for (int i = 0; i < DiffedPixels.Span.Length; i++)		// Remove cleared pixels from screen state (DiffedPixels only contains the cleared pixels at this point so just use that)
		//		OldPixels.Remove(DiffedPixels.Span[i]);
		//	
		//	// At this point, OldPixels only contains the skipped pixels
		//	
		//	// Now determine what to draw and add that to both OldPixels and DiffedPixels
		//	foreach (var p in NewPixels)
		//		if (!ToSkip.Contains(p))
		//		{
		//			OldPixels.Add(p);
		//			DiffedPixels.Add(p);
		//		}
		
		var ToClear = OldPixels.Select(IndexSelector).Except(ToSkip.Select(IndexSelector));
		var ToDraw = NewPixels.Except(ToSkip);
		
		foreach (var i in ToClear)
		{
			DiffedPixels.Add(new()
			{
				Index = i,
				Character = ' ',
				Foreground = Color24.White,
				Background = Color24.Black,
				Style = 0
			});
		}
		
		OldPixels.Clear();
		OldPixels.AddRange(ToSkip);
		
		foreach (var p in ToDraw) { OldPixels.Add(p); DiffedPixels.Add(p); }

		IndexUpdates.Clear();
	}
	
	private void ReconcileFramesEx()
	{
		if (!OldPixels.Any())
		{
			//	If there is no data on the screen to diff against, submit all writes to the renderer
			foreach (var p in IndexUpdates.Values)
			{
				OldPixels.Add(p);
				DiffedPixels.Add(p);
			}
			return;
		}
		
		var NewPixels = new HashSet<Pixel>(IndexUpdates.Values);
		
		int IndexSelector(Pixel p) => p.Index;
		//OldPixels.UnionWith(NewPixels);
		
		
		
		var ToSkip = OldPixels.Intersect(NewPixels);
		var ToClear = OldPixels.Select(IndexSelector).Except(ToSkip.Select(IndexSelector));
		var ToDraw = NewPixels.Except(ToSkip);
		
		foreach (var i in ToClear)
			DiffedPixels.Add(new()
			{
				Index = i,
				Character = ' ',
				Foreground = Color24.White,
				Background = Color24.Black,
				Style = 0
			});
		
		DiffedPixels.AddRange(ToDraw);
		
		OldPixels.Clear();

		IndexUpdates.Clear();
	}
	
	private Pixel LastPixel;
	
	private void RenderModifiedPixels()
	{
		var buf = RenderPixels;

		for(int idx = 0; idx < buf.Count; idx++)
		{
			var LastIndex = LastPixel.Index;
			var CurrentIndex = buf[idx].Index;
			
			var CurrentPixel = buf[idx];
			
			// First, handle position
			if (CurrentIndex - LastIndex != 1)
			{
				if (GetY(LastIndex) == GetY(CurrentIndex) && CurrentIndex > LastIndex)
				{
					FinalWriteBuffer.Append("\u001b[").Append(CurrentIndex - LastIndex - 1).Append("C");		// If indices are on same line and spaced further than 1 cell apart, shift right
				}
				else
				{
					(int Y, int X) = Math.DivRem(CurrentIndex, Width);
					FinalWriteBuffer.Append("\u001b[").Append(Y + 1).Append(';').Append(X + 1).Append("H");     // If anywhere else, set absolute position
				}
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
			LastPixel = CurrentPixel;
		}
	}
	
	private int GetY(int Index) => Index / Width;
	
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

