
using Utf8StringInterpolation;

namespace SharpCanvas;

public unsafe partial class Canvas
{
	private Thread WriteThread;
	private bool DoWrite = false;

	private DoubleFrameBuffer WriteBuffers = new();

	private Utf8StringBuffer FrontWriteBuffer => WriteBuffers.FrontBuffer;
	private Utf8StringBuffer BackWriteBuffer => WriteBuffers.BackBuffer;

	private Action<ReadOnlyMemory<byte>> PlatformWriteStdout;

	private void WriteThreadProc()
	{
	loop_start:
		WriteThreadWait = ExecuteTimed(delegate
		{
			while (!DoWrite) Thread.Yield();
		});

		if (FrontWriteBuffer.WrittenCount != 0)
			PlatformWriteStdout(FrontWriteBuffer.WrittenMemory);

		DoWrite = false;
		goto loop_start;
	}
}
