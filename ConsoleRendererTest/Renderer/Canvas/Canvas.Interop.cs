
using System.Runtime.InteropServices;

namespace SharpCanvas;

public unsafe partial class Canvas
{
	[DllImport("libc", SetLastError = true, EntryPoint = "write")]
	internal static extern int libcWrite(int fd, byte* buf, uint len);

	[DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetStdHandle")]
	internal static extern IntPtr k32GetStdHandle(int nStdHandle);

	[DllImport("kernel32.dll", EntryPoint = "WriteFile")]
	internal static extern bool k32WriteFile(nint fFile, byte* lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, nint lpOverlapped);
}
