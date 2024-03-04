using Collections.Pooled;

namespace SharpCanvas;

public class PooledDoubleDictionaryBuffer<I, T>
{
	private PooledDictionary<I, T> BackBuffer1;
	private PooledDictionary<I, T> BackBuffer2;
	
	public PooledDictionary<I, T> MainBuffer { get; private set; }
	public PooledDictionary<I, T> SecondaryBuffer { get; private set; }
	
	public PooledDoubleDictionaryBuffer(int Capacity = 1000)
	{
		BackBuffer1 = new(Capacity, ClearMode.Never);
		BackBuffer2 = new(Capacity, ClearMode.Never);
		
		MainBuffer = BackBuffer1;
		SecondaryBuffer = BackBuffer2;
	}

	internal void Swap() => (MainBuffer, SecondaryBuffer) = (SecondaryBuffer, MainBuffer);
}

public class PooledDoubleSetBuffer<T>
{
	private PooledSet<T> BackBuffer1;
	private PooledSet<T> BackBuffer2;
	
	public PooledSet<T> MainBuffer { get; private set; }
	public PooledSet<T> SecondaryBuffer { get; private set; }
	
	public PooledDoubleSetBuffer(int Capacity = 1000)
	{
		BackBuffer1 = new(Capacity, ClearMode.Never);
		BackBuffer2 = new(Capacity, ClearMode.Never);
		
		MainBuffer = BackBuffer1;
		SecondaryBuffer = BackBuffer2;
	}

	internal void Swap() => (MainBuffer, SecondaryBuffer) = (SecondaryBuffer, MainBuffer);
}