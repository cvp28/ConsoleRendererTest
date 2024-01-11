
namespace ConsoleRendererTest;
using Codes;

public partial class Canvas
{
	public void WriteAt(int X, int Y, string Text) => WriteAt(X, Y, Text, Color24.White, Color24.Black, StyleCode.None);

	public void WriteAt(int X, int Y, string Text, Color24 Foreground, Color24 Background, params StyleCode[] Style)
	{
		for (int i = 0; i < Text.Length; i++)
		{
			int Index = IX(X + i, Y);

			byte RequestedStyle = Style.Length != 0 ? StyleHelper.PackStyle(Style) : (byte)0;
			TryModifyPixel(Index, Text[i], Foreground, Background, RequestedStyle);
		}
	}

	// Entry point for modifying the pixel array
	// Handles pixel modifications and optimizes redundant ones when able
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
