using System.Text;
using System.Collections.Concurrent;

using Collections.Pooled;

using SharpCanvas;
using SharpCanvas.Codes;

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;

Console.ReadKey(true);

var c = new Canvas();

Console.Write("Retrieving frames... ");

string[] FramePaths = Directory.GetFiles(@"C:\Users\CVPlanck\Documents\repos\ConsoleRendererTest\bad_apple_frames_ascii");
ConcurrentQueue<string> Frames = new();

Task t = Task.Run(delegate
{
	for (int i = 0; i < FramePaths.Length; i++)
	{
		Frames.Enqueue(File.ReadAllText(FramePaths[i]));
		Thread.Sleep(10);
	}
});

Console.WriteLine("done.");

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
	Console.Title = $"FPS: {CurrentFPS:N0} QF: {Frames.Count}";
	LastFPS = CurrentFPS;
	CurrentFPS = 0;
};

FPSTimer.Start();



int X = 10;
int Y = 2;

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

	int fY = 0;
	
	while (Frames.IsEmpty);
	
	Frames.TryDequeue(out var frame);
	
	for (int i = 0; i < frame.Length; i++)
	{
		if (frame[i] == '\n' || frame[i] == '\r')
			fY++;
		else
			c.WriteAt(i % 481, fY, frame[i], Color24.White, Color24.Black, 0);//(StyleCode) Random.Shared.Next(1, 256));
	}
	
	c.Flush();

	//c.WriteAt(X, Y, $"Concurrent Rendering!");
	//c.DrawBox(10, 5, 5, 5, "This is a window!");
	
	//c.WriteAt(10, 10, "Some text");
	//DoRender();

	//c.WriteAt(X, Y, "Other text", new(255, 0, 0), new(255, 255, 255), StyleCode.Bold | StyleCode.Italic | StyleCode.Underlined);

	//Thread.Sleep(12);
	
	//c.Flush();
	
	CurrentFPS++;
	f++;
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