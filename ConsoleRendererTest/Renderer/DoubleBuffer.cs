using Collections.Pooled;

namespace SharpCanvas;

public class DoubleBuffer<T>
{
	private PooledSet<T> BackBuffer1;
	private PooledSet<T> BackBuffer2;
	
	public PooledSet<T> MainBuffer { get; private set; }
	public PooledSet<T> SecondaryBuffer { get; private set; }
	
	public DoubleBuffer(int Capacity = 1000)
	{
		BackBuffer1 = new(Capacity, ClearMode.Never);
		BackBuffer2 = new(Capacity, ClearMode.Never);
		
		MainBuffer = BackBuffer1;
		SecondaryBuffer = BackBuffer2;
	}

	internal void Swap() => (MainBuffer, SecondaryBuffer) = (SecondaryBuffer, MainBuffer);
}

public class DoubleBufferList<T>
{
	private PooledList<T> BackBuffer1;
	private PooledList<T> BackBuffer2;

	public PooledList<T> MainBuffer { get; private set; }
	public PooledList<T> SecondaryBuffer { get; private set; }

	public DoubleBufferList(int Capacity = 1000)
	{
		BackBuffer1 = new(Capacity, ClearMode.Never);
		BackBuffer2 = new(Capacity, ClearMode.Never);

		MainBuffer = BackBuffer1;
		SecondaryBuffer = BackBuffer2;
	}

	internal void Swap() => (MainBuffer, SecondaryBuffer) = (SecondaryBuffer, MainBuffer);
}