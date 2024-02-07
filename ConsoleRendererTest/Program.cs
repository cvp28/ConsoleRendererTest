using System.Text;
using System.Collections.Concurrent;

using SharpCanvas;
using SharpCanvas.Codes;

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;

Console.ReadKey(true);

var c = new Canvas();

var InputQueue = new ConcurrentQueue<ConsoleKeyInfo>();
var Running = true;

// Worker task
new Thread(delegate()
{
	while (Running)
	{
		if (Console.KeyAvailable)
			InputQueue.Enqueue(Console.ReadKey(true));

		Thread.Sleep(50);
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

var Width = Console.WindowWidth;
var Height = Console.WindowHeight;

int X = 10;
int Y = 2;

double shift = 0;

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
			
			case ConsoleKey.C:
				c.ConcurrentRenderingEnabled = !c.ConcurrentRenderingEnabled;
				break;
		}
	}
	
	c.WriteAt(0, 0, $"Concurrent Rendering: {c.ConcurrentRenderingEnabled}");
	
	//	c.WriteAt(X, Y, "Other text", new(255, 0, 0), new(255, 255, 255), StyleCode.Bold | StyleCode.Italic | StyleCode.Underlined);
	//	c.WriteAt(10, 10, "Some text");
	DoRender();
	
	//Thread.Sleep(10);
	
	c.Flush();
	
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
	
	//c.Flush();
}