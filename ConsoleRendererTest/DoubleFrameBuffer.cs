

using System.Collections.Frozen;
using Collections.Pooled;

namespace SharpCanvas;

internal class FrameBuffer
{
	internal PooledDictionary<int, Pixel> IndexUpdates;
	
	internal FrozenSet<Pixel> NewPixels { get; private set; }
	
	internal PooledDoubleSetBuffer<Pixel> OldPixels;
	
	internal FrameBuffer(int Capacity)
	{
		IndexUpdates = new(1000, ClearMode.Never);
		
		OldPixels = new(Capacity);
	}
	
	internal void ComputeNewPixels() => NewPixels = IndexUpdates.Values.ToFrozenSet();
	
	internal void SwapOldPixels() => OldPixels.Swap();
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
	
	internal DoubleFrameBuffer(int Capacity)
	{
		_FrameBuffer1 = new(Capacity);
		_FrameBuffer2 = new(Capacity);
		
		FrontBuffer = _FrameBuffer1;
		BackBuffer = _FrameBuffer2;
	}
	
	internal void Swap() => (FrontBuffer, BackBuffer) = (BackBuffer, FrontBuffer);
}