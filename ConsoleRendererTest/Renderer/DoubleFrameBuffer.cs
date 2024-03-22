
using Utf8StringInterpolation;

namespace SharpCanvas;

/// <summary>
/// Basically just a combo class to combine 2 FrameBuffers into one logical object
/// </summary>
internal class DoubleFrameBuffer
{
	private Utf8StringBuffer _FrameBuffer1;
	private Utf8StringBuffer _FrameBuffer2;
	
	internal Utf8StringBuffer FrontBuffer { get; private set; }
	internal Utf8StringBuffer BackBuffer { get; set; }
	
	internal DoubleFrameBuffer()
	{
		_FrameBuffer1 = new();
		_FrameBuffer2 = new();
		
		FrontBuffer = _FrameBuffer1;
		BackBuffer = _FrameBuffer2;
	}
	
	internal void Swap() => (FrontBuffer, BackBuffer) = (BackBuffer, FrontBuffer);

	internal void DisposeFrontBuffer() => FrontBuffer.Dispose();
	internal void DisposeBackBuffer() => BackBuffer.Dispose();
}