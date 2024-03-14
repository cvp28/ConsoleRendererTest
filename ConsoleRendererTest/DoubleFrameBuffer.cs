
using Collections.Pooled;

namespace SharpCanvas;

internal class FrameBuffer
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
	internal PooledSet<Pixel> ToDraw { get; private set; }
	
	internal FrameBuffer(int Capacity)
	{
		ToClear = new(Capacity, ClearMode.Never);
		ToSkip = new(Capacity, ClearMode.Never);
		ToDraw = new(Capacity, ClearMode.Never);
	}
}

/// <summary>
/// Basically just a combo class to combine 2 FrameBuffers into one logical object
/// </summary>
internal class DoubleFrameBuffer
{
	private FrameBuffer _FrameBuffer1;
	private FrameBuffer _FrameBuffer2;
	
	internal FrameBuffer FrontBuffer { get; private set; }
	internal FrameBuffer BackBuffer { get; private set; }
	
	internal DoubleFrameBuffer(int Capacity = 1000)
	{
		_FrameBuffer1 = new(Capacity);
		_FrameBuffer2 = new(Capacity);
		
		FrontBuffer = _FrameBuffer1;
		BackBuffer = _FrameBuffer2;
	}
	
	internal void Swap() => (FrontBuffer, BackBuffer) = (BackBuffer, FrontBuffer);
}