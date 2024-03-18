
using Collections.Pooled;

namespace SharpCanvas;

internal class RenderBuffer
{
	// Contains 
	internal PooledDictionary<int, Pixel> ToClear { get; private set; }
	
	/// <summary>
	/// Contains pixels currently on the screen that will persist to the next frame
	/// </summary>
	internal PooledSet<Pixel> ToSkip { get; private set; }
	
	/// <summary>
	/// Contains pixels not currently on the screen that will be drawn for next frame
	/// </summary>
	internal PooledList<Pixel> ToDraw { get; private set; }
	
	internal RenderBuffer(int Capacity)
	{
		ToClear = new(Capacity, ClearMode.Never);
		ToSkip = new(Capacity, ClearMode.Never);
		ToDraw = new(Capacity, ClearMode.Never);
	}
}

/// <summary>
/// Basically just a combo class to combine 2 FrameBuffers into one logical object
/// </summary>
internal class DoubleRenderBuffer
{
	private RenderBuffer _FrameBuffer1;
	private RenderBuffer _FrameBuffer2;
	
	internal RenderBuffer FrontBuffer { get; private set; }
	internal RenderBuffer BackBuffer { get; private set; }
	
	internal DoubleRenderBuffer(int Capacity = 1000)
	{
		_FrameBuffer1 = new(Capacity);
		_FrameBuffer2 = new(Capacity);
		
		FrontBuffer = _FrameBuffer1;
		BackBuffer = _FrameBuffer2;
	}
	
	internal void Swap() => (FrontBuffer, BackBuffer) = (BackBuffer, FrontBuffer);
}