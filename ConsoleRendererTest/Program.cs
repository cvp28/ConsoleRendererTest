using System.Text;

using ConsoleRendererTest;
using ConsoleRendererTest.Codes;

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;
Console.Clear();

//	byte Current = StyleHelper.PackStyle(StyleCode.Bold, StyleCode.Underlined, StyleCode.Blink);
//	byte New = StyleHelper.PackStyle(StyleCode.Bold, StyleCode.Dim, StyleCode.Italic, StyleCode.Underlined, StyleCode.Blink, StyleCode.Inverted, StyleCode.CrossedOut);
//	byte Reset = (byte) (~New & Current);
//	byte Set = (byte) ((New | Reset) ^ Current);
//	
//	Console.WriteLine($"{Current:b7}");
//	Console.WriteLine($"{New:b7}");
//	Console.WriteLine($"{Reset:b7}");
//	Console.WriteLine($"{Set:b7}");

var c = new Canvas();

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

while (true)
{
	//c.WriteAt(40, 0, $"FPS: {LastFPS}");

	c.WriteAt(0, 0, "Hello");
	c.WriteAt(5, 0, "World", Color24.White, Color24.Black, StyleCode.Blink);
	c.WriteAt(15, 0, "normaltexthere");
	
	c.WriteAt(20, 10, "some more text!!", new(0, 255, 255), new(0, 0, 128));
	c.WriteAt(50, 10, "underlined this time", new(0, 255, 255), new(0, 0, 128), StyleCode.Underlined, StyleCode.Italic);

	c.WriteAt(Width - 1, 0, "Yo", new(0, 255, 255), Color24.Black, StyleCode.Blink);

	//c.DoBufferDump(1);
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