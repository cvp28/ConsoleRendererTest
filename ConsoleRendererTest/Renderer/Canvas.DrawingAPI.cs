
namespace ConsoleRendererTest;
using Codes;

public partial class Canvas
{
	public Color24 DefaultForeground = Color24.White;
	public Color24 DefaultBackground = Color24.Black;
	
	public void WriteAt(int X, int Y, string Text) => WriteAt(X, Y, Text, new Color24(255, 255, 255), new Color24(0, 0, 0), StyleCode.None);

	public void WriteAt(int X, int Y, string Text, Color24 Foreground, Color24 Background, StyleCode Style)
	{
		for (int i = 0; i < Text.Length; i++)
		{
			int Index = IX(X + i, Y);
			
			TryModifyPixel(Index, Text[i], Foreground, Background, (byte) Style);
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
