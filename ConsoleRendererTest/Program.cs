using System.Text;
using System.Collections.Concurrent;

using SharpCanvas;
using SharpCanvas.Codes;

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;

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

int X = 10;
int Y = 5;

double shift = 0;
bool thing = true;

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
	
	//	c.Flush();
	
	//if (thing)
	//	X++;
	//else
	//	X--;
	
	//DoRender();
	c.WriteAt(10, 10, "Some text");
	
	c.WriteAt(X, Y, "Other text", new(255, 0, 0), Color24.Black, StyleCode.None);
	
	thing = !thing;
	
	//Thread.Sleep(1);
	
	c.Flush();
	
	CurrentFPS++;
}



void DoRender()
{
	for (int x = 0; x < Width; x++)
	{
		double y = Height / 2 + 10 * Math.Sin(0.05 * x + shift);
		
		c.WriteAt(x, (int) y, '*', new(255, 0, 0), new(0,0,0), StyleCode.Bold | StyleCode.Underlined);
	}
	
	c.DrawLine(0, Height / 2, Width - 1, (int)(Height / 2 + 10 * Math.Sin(0.05 * (Width - 1) + shift)));
	
	shift += 0.05;
	
	c.Flush();
}