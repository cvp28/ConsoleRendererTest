using Collections.Pooled;
using SharpCanvas;

namespace ConsoleRendererTest;

public class CanvasBufferProvider
{
	private PooledList<Pixel> BackBuffer1 = new(1000, ClearMode.Never);
	private PooledList<Pixel> BackBuffer2 = new(1000, ClearMode.Never);
	
	public PooledList<Pixel> MainThreadBuffer { get; private set; }
	public PooledList<Pixel> RenderThreadBuffer { get; private set; }
	
	internal CanvasBufferProvider()
	{
		MainThreadBuffer = BackBuffer1;
		RenderThreadBuffer = BackBuffer2;
	}
	
	internal void Swap()
	{
		if (ReferenceEquals(MainThreadBuffer, BackBuffer1))
		{
			MainThreadBuffer = BackBuffer2;
			RenderThreadBuffer = BackBuffer1;
		}
		else
		{
			MainThreadBuffer = BackBuffer1;
			RenderThreadBuffer = BackBuffer2;
		}
	}
	
	public void ClearMainThreadBuffer() => MainThreadBuffer.Clear();
	public void ClearRenderThreadBuffer() => RenderThreadBuffer.Clear();
}
