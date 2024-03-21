//#define BAD_APPLE

using System.Text;
using System.Collections.Concurrent;

using SharpCanvas;
using SharpCanvas.Codes;

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;

Console.ReadKey(true);

var c = new Canvas();

#if BAD_APPLE
Console.Write("Retrieving frames... ");
string[] FramePaths = Directory.GetFiles(@"C:\Users\CVPlanck\Documents\repos\ConsoleRendererTest\bad_apple_frames_ascii");
string[] Frames = new string[FramePaths.Length];
await Task.Run(delegate
{
	for (int i = 0; i < FramePaths.Length; i++)
		Frames[i] = File.ReadAllText(FramePaths[i]);
});
Console.WriteLine("done.");
#endif

var Width = Console.WindowWidth;
var Height = Console.WindowHeight;

var InputQueue = new ConcurrentQueue<ConsoleKeyInfo>();
var Running = true;

// Worker task
new Thread(delegate()
{
	while (Running)
	{
		if (Console.KeyAvailable)
			InputQueue.Enqueue(Console.ReadKey(true));

		Thread.Sleep(20);
	}
}).Start();

int CurrentFPS = 0;
int LastFPS = 0;

System.Timers.Timer FPSTimer = new() { Interval = 1000 };
FPSTimer.Elapsed += (sender, args) =>
{
	Console.Title = $"FPS: {CurrentFPS:N0}";
	LastFPS = CurrentFPS;
	CurrentFPS = 0;
};

FPSTimer.Start();

int X = 0;
int Y = 3;

double shift = 0;
int f = 0;

while (Running)
{
	if (InputQueue.Any())
	{
		InputQueue.TryDequeue(out var cki);
		switch (cki.Key)
		{
			case ConsoleKey.F1:
				c.DoBufferDump(1);
				break;
			
			case ConsoleKey.Escape:
				Running = false;
				FPSTimer.Stop();
				continue;
			
			case ConsoleKey.R:
				c.Resize();
				Width = Console.WindowWidth;
				Height = Console.WindowHeight;
				break;
			
			case ConsoleKey.UpArrow:
				Y--;
				break;
			
			case ConsoleKey.DownArrow:
				Y++;
				break;
			
			case ConsoleKey.LeftArrow:
				X--;
				break;
			
			case ConsoleKey.RightArrow:
				X++;
				break;
		}
	}

#if BAD_APPLE
	int fY = 0;
	
	string frame = Frames[f];
	
	for (int i = 0; i < frame.Length; i++)
	{
		if (frame[i] == '\n' || frame[i] == '\r')
			fY++;
		else
			c.WriteAt(i % 482, fY, frame[i]);
	}
	
	c.Flush();
	
	if (f == Frames.Length - 1)
		f = 0;
	else
		f++;
#else
	//DoRender();
	c.WriteAt(X, Y, "waaahhhhttttt");
	
	c.WriteAt(0, 0, $"MT-RT Wait: {c.MainThreadWait.TotalNanoseconds,-7} ns");
	c.WriteAt(0, 1, $"RT-MT Wait: {c.RenderThreadMTWait.TotalNanoseconds,-7} ns");
	c.WriteAt(0, 2, $"RT-WT Wait: {c.RenderThreadWTWait.TotalNanoseconds,-7} ns");
	c.WriteAt(0, 3, $"WT-RT Wait: {c.WriteThreadWait.TotalNanoseconds,-7} ns");
	
	c.Flush();
#endif

	CurrentFPS++;
}

void DoRender()
{
	for (int x = 0; x < Width; x++)
	{
		double y = Height / 2 + 10 * Math.Sin(0.05 * x + shift);
		c.WriteAt(x, (int) y, '*', new(255, 255, 255), new(0, 0, 0), StyleCode.None);
	}
	
	c.DrawLine(0, Height / 2, Width - 1, (int)(Height / 2 + 10 * Math.Sin(0.05 * (Width - 1) + shift)));
	
	shift += 0.1;
}