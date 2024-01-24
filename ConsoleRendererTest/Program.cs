using System.Collections.Concurrent;
using System.Text;

using SharpCanvas;

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;
Console.Clear();

var c = new Canvas()
{
	
};

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
	LastFPS = CurrentFPS;
	CurrentFPS = 0;
};

FPSTimer.Start();

var Width = Console.WindowWidth;
var Height = Console.WindowHeight;

int X = 10;
int Y = 15;

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
			
			case ConsoleKey.RightArrow:
				X++;
				break;
			
			case ConsoleKey.R:
				c.Resize();
				Width = Console.WindowWidth;
				Height = Console.WindowHeight;
				break;
		}
	}
	
	// With just one write, it gets 7+ million iterations (not frames) per second in this loop
	//	c.WriteAt(40, 0,  "             ");
	c.WriteAt(40, 0, $"FPS: {LastFPS}");
	c.WriteAt(Width - 1, Height - 1, "H");
	
	//	c.WriteAt(0, 0, "Hello");
	//	c.WriteAt(5, 0, "World", new(255,255,255), new(0,0,0), StyleCode.Blink | StyleCode.Inverted);
	//	c.WriteAt(15, 0, "normaltexthere");
	//	
	//	c.WriteAt(20, 10, "some more text!!", new(0, 255, 255), new(0, 0, 128), StyleCode.None);
	//	c.WriteAt(50, 10, "underlined this time", new(0, 255, 255), new(0, 0, 128), StyleCode.Underlined | StyleCode.Italic);
	//	
	//	c.WriteAt(Width - 1, 0, "Yo", new(0, 255, 255), new(0,0,0), StyleCode.Blink);
	//	
	c.DrawBox(X, Y, 30, 12);
	
	c.Flush();

	CurrentFPS++;
}

//Console.ReadKey(true);

//	int Offset = 0;

//	while (true)
//	{
//		c.WriteAt(2, 1 + Offset, "Hello, World");
//		c.Flush();
//	
//		c.WriteAt(30, 10 + Offset, "more text", new(153, 0, 0), new(0, 255, 0));
//		c.Flush();
//		
//		Offset++;
//		Thread.Sleep(100);
//		
//		if (Console.KeyAvailable)
//			break;
//	}
//	goto loop;