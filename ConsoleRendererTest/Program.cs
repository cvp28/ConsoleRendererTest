using System.Collections.Concurrent;
using System.Text;

using SharpCanvas;

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;
Console.Clear();

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
	}
}).Start();

int CurrentFPS = 0;
int LastFPS = 0;

System.Timers.Timer FPSTimer = new() { Interval = 1000 };
FPSTimer.Elapsed += (sender, args) =>
{
	Console.Title = $"FPS: {CurrentFPS}";
	LastFPS = CurrentFPS;
	CurrentFPS = 0;
};

FPSTimer.Start();

var Width = Console.WindowWidth;
var Height = Console.WindowHeight;

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
		}
	}
	
	DoRender();
	
	CurrentFPS++;
}

void DoRender()
{	
	for (int x = 0; x < Width; x++)
	{
		double y = Height / 2 + 10 * Math.Sin(0.1 * x + shift);
		
		c.WriteAt(x, (int) y, '*', new(255, 255, 255), new(0,0,0), SharpCanvas.Codes.StyleCode.None);
	}
	
	c.DrawLine(0, Height / 2, Width - 1, (int)(Height / 2 + 10 * Math.Sin(0.1 * (Width - 1) + shift)));
	
	shift += 0.05;
	
	c.Flush();
}