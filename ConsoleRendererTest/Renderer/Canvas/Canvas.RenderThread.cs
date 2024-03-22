
using System.Buffers;

using Utf8StringInterpolation;

namespace SharpCanvas;

public unsafe partial class Canvas
{
	private Thread RenderThread;
	private bool DoRender = false;

	private DoubleRenderBuffer RenderBuffers = new(300);

	private RenderBuffer BackBuffer => RenderBuffers.BackBuffer;
	private RenderBuffer FrontBuffer => RenderBuffers.FrontBuffer;

	private void RenderThreadProc()
	{
	loop_start:
		RenderThreadMTWait = ExecuteTimed(delegate
		{
			while (!DoRender) Thread.Yield();
		});

		WriteBuffers.BackBuffer = Utf8String.CreateWriter(out var writer);

		//RenderPixels(ref writer);
		var clearEnumerator = FrontBuffer.ToClear.Keys.GetEnumerator();
		var drawEnumerator = FrontBuffer.ToDraw.Values.GetEnumerator();

		while (clearEnumerator.MoveNext())
			RenderClearPixel(clearEnumerator.Current, ref writer);

		while (drawEnumerator.MoveNext())
		{
			var NewPixel = drawEnumerator.Current;
			RenderPixel(in NewPixel, ref writer);
		}

		writer.Flush(); // Fill buffer with writer contents
		writer.Dispose();

		RenderThreadWTWait = ExecuteTimed(delegate
		{
			while (DoWrite) Thread.Yield();
		});

		WriteBuffers.Swap();

		DoWrite = true;

		DoRender = false;
		goto loop_start;
	}

	private void RenderPixels(ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
	{
		var clearEnumerator = FrontBuffer.ToClear.Keys.GetEnumerator();
		var drawEnumerator = FrontBuffer.ToDraw.Values.GetEnumerator();

		while (clearEnumerator.MoveNext())
			RenderClearPixel(clearEnumerator.Current, ref writer);

		while (drawEnumerator.MoveNext())
		{
			var NewPixel = drawEnumerator.Current;
			RenderPixel(in NewPixel, ref writer);
		}
	}

	private Pixel LastPixel;
	private int GetY(int Index) => Index / Width;

	private void RenderPixel(in Pixel NewPixel, ref Utf8StringWriter<ArrayBufferWriter<byte>> writer)
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

			writer.Append(StyleTransitionSequences[ResetMask, SetMask]);
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

		LastPixel = new Pixel(CurrentIndex, ' ', Color24.White, Color24.Black, 0);
	}
}
