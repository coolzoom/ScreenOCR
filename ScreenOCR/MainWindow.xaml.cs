using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using Tesseract;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Clipboard = System.Windows.Clipboard;
using Point = System.Windows.Point;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace ScreenOCR
{

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private MouseHook mh;
		public MainWindow() {
			InitializeComponent();
			thresholdLabel.Content = slider.Value;
			Mask.onMouseUp += new Mask.MouseUpHandler(GetImage);
			Mask.onMouseUp += () => Show();
			slider.ValueChanged += Slider_ValueChanged;

		}

		private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			thresholdLabel.Content = (int)e.NewValue;

		}
		#region Getting a mouse coordinates during mouse down and up events
		internal static Point startPoint = new Point();
		internal static Point endPoint = new Point();
		public void GetMouseDown(object sender, MouseHookEventArgs e) {
			startPoint.X = e.position.X;
			startPoint.Y = e.position.Y;
			Debug.WriteLine("{0},{1}", startPoint.X, startPoint.Y);
		}
		public void GetMouseUp(object sender, MouseHookEventArgs e) {
			endPoint.X = e.position.X;
			endPoint.Y = e.position.Y;
			if (mh != null) {
				mh.Dispose();
			}
			Debug.WriteLine("{0},{1}", endPoint.X, endPoint.Y);

		}

		#endregion


		public static Bitmap BitmapFromSource(BitmapSource source) {
			using (MemoryStream outStream = new MemoryStream()) {
				BitmapEncoder enc = new PngBitmapEncoder();
				enc.Frames.Add(BitmapFrame.Create(source));
				enc.Save(outStream);
				Bitmap bitmap = new Bitmap(outStream);

				return new Bitmap(bitmap);
			}
		}
		private void DoOCR(Bitmap img) {
			try {
				using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default)) {
					//using (var img = ClipboardImgToBitmap())
					//using (var img = Pix.LoadFromFile(imagePath))

					using (var page = engine.Process(img)) {
						var text = page.GetText();
						text = text.Replace(" ", "");
						text = Regex.Replace(Regex.Replace(text, @"\n\n", "\n"), @"\n\n", "\n");
						outputBlock.Text = text;
					}


				}
			}
			catch (Exception e) {
				Debug.Indent();
				Trace.TraceError(e.ToString());
				Debug.WriteLine("Unexpected Error: " + e.Message);
				Debug.WriteLine("Details: ");
				Debug.WriteLine(e.ToString());
			}
		}

		private void GetImage() {

			int width = Math.Abs((int)endPoint.X - (int)startPoint.X - 4);
			int height = Math.Abs((int)endPoint.Y - (int)startPoint.Y - 4);
			if (height > 10 && width > 10) {
				using (Bitmap bitmap = ImageProcessor.GetBitmap((int)startPoint.X + 2, (int)startPoint.Y + 2, width, height)) {
					ImageProcessor.SetContrast(bitmap, (int)slider.Value);
					image.Source = ImageProcessor.ImageSourceFromBitmap(bitmap);
					DoOCR(bitmap);
				}
			}
		}

		

		private static void PrintByteArray(byte[] rgbValues) {
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < 300; i++) {
				sb.Append(rgbValues[i] + ",");
			}
			Console.WriteLine(sb.ToString());
		}

		private void SelectAreaButton_Click(object sender, RoutedEventArgs e) {
			mh = new MouseHook();
			mh.MouseDown += new MouseHook.MouseDownEventHandler(GetMouseDown);
			mh.MouseUp += new MouseHook.MouseUpEventHandler(GetMouseUp);
			Hide();
			Mask mask = new Mask();
			mask.Show();
		}

		/// <summary>
		/// Helper class containing User32 API functions
		/// </summary>
		private class User32
		{
			[StructLayout(LayoutKind.Sequential)]
			public struct RECT
			{
				public int left;
				public int top;
				public int right;
				public int bottom;
			}
			[DllImport("user32.dll")]
			public static extern IntPtr GetDesktopWindow();
			[DllImport("user32.dll")]
			public static extern IntPtr GetWindowDC(IntPtr hWnd);
			[DllImport("user32.dll")]
			public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
			[DllImport("user32.dll")]
			public static extern IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);
		}

		/// <summary>
		/// Helper class containing Gdi32 API functions
		/// </summary>
		private class GDI32
		{

			public const int SRCCOPY = 0x00CC0020; // BitBlt dwRop parameter
			[DllImport("gdi32.dll")]
			public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest,
				int nWidth, int nHeight, IntPtr hObjectSource,
				int nXSrc, int nYSrc, int dwRop);
			[DllImport("gdi32.dll")]
			public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth,
				int nHeight);
			[DllImport("gdi32.dll")]
			public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
			[DllImport("gdi32.dll")]
			public static extern bool DeleteDC(IntPtr hDC);
			[DllImport("gdi32.dll")]
			public static extern bool DeleteObject(IntPtr hObject);
			[DllImport("gdi32.dll")]
			public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
		}
		/// <summary>
		/// Creates an Image object containing a screen shot of a specific window
		/// </summary>
		/// <param name="handle">The handle to the window. (In windows forms, this is obtained by the Handle property)</param>
		/// <returns></returns>
		public Image CaptureWindow(IntPtr handle)
		{
			// get te hDC of the target window
			IntPtr hdcSrc = User32.GetWindowDC(handle);
			// get the size
			User32.RECT windowRect = new User32.RECT();
			User32.GetWindowRect(handle, ref windowRect);
			int width = windowRect.right - windowRect.left;
			int height = windowRect.bottom - windowRect.top;
			// create a device context we can copy to
			IntPtr hdcDest = GDI32.CreateCompatibleDC(hdcSrc);
			// create a bitmap we can copy it to,
			// using GetDeviceCaps to get the width/height
			IntPtr hBitmap = GDI32.CreateCompatibleBitmap(hdcSrc, width, height);
			// select the bitmap object
			IntPtr hOld = GDI32.SelectObject(hdcDest, hBitmap);
			// bitblt over
			GDI32.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, GDI32.SRCCOPY);
			// restore selection
			GDI32.SelectObject(hdcDest, hOld);
			// clean up 
			GDI32.DeleteDC(hdcDest);
			User32.ReleaseDC(handle, hdcSrc);
			// get a .NET image object for it
			Image img = Image.FromHbitmap(hBitmap);
			// free up the Bitmap object
			GDI32.DeleteObject(hBitmap);
			return img;
		}
		private void selectActiveWindow_Click(object sender, RoutedEventArgs e)
        {
			

			Process[] process = Process.GetProcessesByName("devenv");

			Image img = CaptureWindow(process[0].Handle);

			// get the size
			User32.RECT windowRect = new User32.RECT();
			User32.GetWindowRect(process[0].Handle, ref windowRect);
			int width = windowRect.right - windowRect.left;
			int height = windowRect.bottom - windowRect.top;

			if (height > 10 && width > 10)
			{
				using (Bitmap bitmap = ImageProcessor.GetBitmap(windowRect.left, windowRect.top, width, height))
				{
					ImageProcessor.SetContrast(bitmap, (int)slider.Value);
					image.Source = ImageProcessor.ImageSourceFromBitmap(bitmap);

					//image.Source = System.Windows.Interop.Imaging.fr( img);
					DoOCR(bitmap);
				}
			}
		}
    }
}
