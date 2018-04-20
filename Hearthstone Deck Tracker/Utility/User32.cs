#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using HearthSim.Util;
using Point = System.Drawing.Point;

#endregion

namespace Hearthstone_Deck_Tracker
{
	public class User32
	{
		[Flags]
		public enum MouseEventFlags : uint
		{
			LeftDown = 0x00000002,
			LeftUp = 0x00000004,
			RightDown = 0x00000008,
			RightUp = 0x00000010,
			Wheel = 0x00000800
		}

		[DllImport("user32.dll")]
		public static extern IntPtr GetClientRect(IntPtr hWnd, ref Rect rect);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool GetWindowRect(IntPtr hwnd, out Rect lpRect);

		[DllImport("user32.dll")]
		public static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, UIntPtr dwExtraInfo);

		[DllImport("user32.dll")]
		public static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

		[DllImport("user32.dll")]
		public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

		[DllImport("user32.dll")]
		public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetCursorPos(out MousePoint lpPoint);

		public static Point GetMousePos() => GetCursorPos(out var p) ? new Point(p.X, p.Y) : Point.Empty;

		public static Process GetHearthstoneProc()
		{
			var window = HearthstoneWindow.Get();
			if(window == IntPtr.Zero)
				return null;
			try
			{
				GetWindowThreadProcessId(window, out uint procId);
				return Process.GetProcessById((int)procId);
			}
			catch
			{
				return null;
			}
		}

		public static Rectangle GetHearthstoneRect(bool dpiScaling)
		{
			// Returns the co-ordinates of Hearthstone's client area in screen co-ordinates
			var hsHandle = HearthstoneWindow.Get();
			var rect = new Rect();
			var ptUL = new Point();
			var ptLR = new Point();

			GetClientRect(hsHandle, ref rect);

			ptUL.X = rect.left;
			ptUL.Y = rect.top;

			ptLR.X = rect.right;
			ptLR.Y = rect.bottom;

			ClientToScreen(hsHandle, ref ptUL);
			ClientToScreen(hsHandle, ref ptLR);

			if(dpiScaling)
			{
				ptUL.X = (int)(ptUL.X / Helper.DpiScalingX);
				ptUL.Y = (int)(ptUL.Y / Helper.DpiScalingY);
				ptLR.X = (int)(ptLR.X / Helper.DpiScalingX);
				ptLR.Y = (int)(ptLR.Y / Helper.DpiScalingY);
			}

			return new Rectangle(ptUL.X, ptUL.Y, ptLR.X - ptUL.X, ptLR.Y - ptUL.Y);
		}


		//http://joelabrahamsson.com/detecting-mouse-and-keyboard-input-with-net/
		public class MouseInput : IDisposable
		{
			private const int WH_MOUSE_LL = 14;
			private const int WM_LBUTTONDOWN = 0x201;
			private const int WM_LBUTTONUP = 0x0202;
			private readonly WindowsHookHelper.HookDelegate _mouseDelegate;
			private readonly IntPtr _mouseHandle;
			private bool _disposed;

			public MouseInput()
			{
				_mouseDelegate = MouseHookDelegate; //crashes application if directly used for some reason
				_mouseHandle = WindowsHookHelper.SetWindowsHookEx(WH_MOUSE_LL, _mouseDelegate, IntPtr.Zero, 0);
			}

			public void Dispose() => Dispose(true);

			public event EventHandler<EventArgs> LmbDown;
			public event EventHandler<EventArgs> LmbUp;
			public event EventHandler<EventArgs> MouseMoved;

			private IntPtr MouseHookDelegate(int code, IntPtr wParam, IntPtr lParam)
			{
				if(code < 0)
					return WindowsHookHelper.CallNextHookEx(_mouseHandle, code, wParam, lParam);


				switch(wParam.ToInt32())
				{
					case WM_LBUTTONDOWN:
						LmbDown?.Invoke(this, new EventArgs());
						break;
					case WM_LBUTTONUP:
						LmbUp?.Invoke(this, new EventArgs());
						break;
					default:
						MouseMoved?.Invoke(this, new EventArgs());
						break;
				}

				return WindowsHookHelper.CallNextHookEx(_mouseHandle, code, wParam, lParam);
			}

			protected virtual void Dispose(bool disposing)
			{
				if(_disposed)
					return;
				if(_mouseHandle != IntPtr.Zero)
					WindowsHookHelper.UnhookWindowsHookEx(_mouseHandle);
				_disposed = true;
			}

			~MouseInput()
			{
				Dispose(false);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MousePoint
		{
			public readonly int X;
			public readonly int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Rect
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		public class WindowsHookHelper
		{
			public delegate IntPtr HookDelegate(int code, IntPtr wParam, IntPtr lParam);

			[DllImport("User32.dll")]
			public static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);

			[DllImport("User32.dll")]
			public static extern IntPtr UnhookWindowsHookEx(IntPtr hHook);

			[DllImport("User32.dll")]
			public static extern IntPtr SetWindowsHookEx(int idHook, HookDelegate lpfn, IntPtr hmod, int dwThreadId);
		}
	}
}
