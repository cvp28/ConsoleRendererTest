
namespace SharpCanvas;
using Codes;

public partial class Canvas
{
	public Color24 DefaultForeground = Color24.White;
	public Color24 DefaultBackground = Color24.Black;
	
	public void WriteAt(int X, int Y, string Text) => WriteAt(X, Y, Text, DefaultForeground, DefaultBackground, StyleCode.None);

	public void WriteAt(int X, int Y, string Text, Color24 Foreground, Color24 Background, StyleCode Style)
	{
		for (int i = 0; i < Text.Length; i++)
		{
			int Index = IX(X + i, Y);
			
			TryModifyPixel(Index, Text[i], Foreground, Background, (byte) Style);
		}
	}
	
	public void DrawBox(int X, int Y, int Width, int Height, string Title = "")
	{
		var TopLeftIndex = IX(X, Y);
		var TopRightIndex = IX(X + Width - 1, Y);
		var BottomLeftIndex = IX(X, Y + Height - 1);
		var BottomRightIndex = IX(X + Width - 1, Y + Height - 1);
		
		TryModifyPixel(TopLeftIndex, '╭', Color24.White, Color24.Black, 0);
		TryModifyPixel(TopRightIndex, '╮', Color24.White, Color24.Black, 0);
		TryModifyPixel(BottomLeftIndex, '╰', Color24.White, Color24.Black, 0);
		TryModifyPixel(BottomRightIndex, '╯', Color24.White, Color24.Black, 0);
		
		for (int i = 0; i < Width - 2; i++)
		{
			var TopIndex = IX(X + 1 + i, Y);
			var BottomIndex = IX(X + 1 + i, Y + Height - 1);
			
			TryModifyPixel(TopIndex, '─', Color24.White, Color24.Black, 0);
			TryModifyPixel(BottomIndex, '─', Color24.White, Color24.Black, 0);
		}
		
		for (int i = 1; i < Height - 1; i++)
		{
			var NextLeftIndex = IX(X, Y + i);
			var NextRightIndex = IX(X + Width - 1, Y + i);
			
			TryModifyPixel(NextLeftIndex, '│', Color24.White, Color24.Black, 0);
			TryModifyPixel(NextRightIndex, '│', Color24.White, Color24.Black, 0);
		}
	}

	// Entry point for modifying the pixel array
	// Handles pixel modifications and optimizes redundant ones away when able
	private void TryModifyPixel(int Index, char Character, Color24 Foreground, Color24 Background, byte StyleMask)
	{
		var NewPixel = new Pixel()
		{
			Character = Character,
			Foreground = Foreground,
			Background = Background,
			Style = StyleMask
		};

		if (!Pixel.NotEqual(LastFramePixels[Index], NewPixel))
		{
			ModifiedIndices.Remove(Index);
			return;
		}

		if (!ModifiedIndices.Span.Contains(Index))
			ModifiedIndices.Add(Index);

		CurrentFramePixels[Index] = NewPixel;
	}
}
