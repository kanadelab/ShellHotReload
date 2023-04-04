using ShellHotReload;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShellHotReload
{
	public class SakuraFMOReader
	{
		private static readonly Encoding ShiftJIS = Encoding.GetEncoding("Shift_JIS");
		private const int MutexTimeoutMs = 1000;
		private const string MutexName = "SakuraFMO";
		private const string FMOName = "Sakura";
		private const string SSPFMOHeader = "ssp_fmo_header_";
		private Dictionary<string, SakuraFMORecord> records;

		public ReadOnlyDictionary<string, SakuraFMORecord> Records
		{
			get
			{
				return new ReadOnlyDictionary<string, SakuraFMORecord>(records);
			}
		}

		public SakuraFMOReader()
		{
			records = new Dictionary<string, SakuraFMORecord>();
		}

		public static void DumpToFile(string fileName)
		{
			using (var mutex = new Mutex(false, MutexName))
			{
				if (mutex.WaitOne(MutexTimeoutMs))
				{
					try
					{
						using (var fmo = MemoryMappedFile.OpenExisting(FMOName))
						{
							using (var fmoStream = fmo.CreateViewStream())
							{
								using (var reader = new BinaryReader(fmoStream))
								{
									byte[] data = new byte[fmoStream.Length];
									reader.Read(data, 0, (int)fmoStream.Length);

									File.WriteAllBytes(fileName, data);
								}
							}
						}
					}
					catch (FileNotFoundException)
					{
						//fmoうまく読めなかった…
						throw;
					}
					finally
					{
						mutex.ReleaseMutex();
					}
				}
			}
		}

		//場合によって同期待ちするので注意
		public void Read()
		{
			records.Clear();
			var lines = new List<string>();

			using (var mutex = new Mutex(false, MutexName))
			{
				if (mutex.WaitOne(MutexTimeoutMs))
				{
					try
					{
						using (var fmo = MemoryMappedFile.OpenExisting(FMOName))
						{
							using (var fmoStream = fmo.CreateViewStream())
							{
								using (var reader = new StreamReader(fmoStream, ShiftJIS))
								{
									while (true)
									{
										var line = reader.ReadLine();
										if (line != null)
											lines.Add(line);
										else
											break;
									}
								}
							}
						}
					}
					catch
					{
						//fmoうまく読めなかった…
					}
					finally
					{
						mutex.ReleaseMutex();
					}
				}
			}

			//ここまででFMOを読めたので、解析する
			//http://ssp.shillest.net/docs/fmo.html
			foreach (var line in lines)
			{
				int headerPos = line.IndexOf(SSPFMOHeader);
				if (headerPos < 0)
					continue;

				var rawData = line.Substring(headerPos);

				//fmoの識別子の後データ本体とは . で区切られている
				var data = rawData.Split(new char[] { '.' }, 2, StringSplitOptions.None);

				var fmoId = data[0];        //ゴーストごとの識別子
				var dataBody = data[1];

				//データのkey,valueはバイト値1で区切られている
				var dataKeyValue = dataBody.Split(new char[] { (char)1 }, 2, StringSplitOptions.None);
				var key = dataKeyValue[0];
				var value = dataKeyValue[1];

				//データを追加
				if (!records.ContainsKey(fmoId))
					records.Add(fmoId, new SakuraFMORecord(fmoId));
				records[fmoId].Parse(key, value);
			}

			//基準を満たしていない情報を削除
			foreach (var item in records.ToArray())
			{
				if (string.IsNullOrEmpty(item.Value.GhostPath) ||
					string.IsNullOrEmpty(item.Value.GhostName) ||
					string.IsNullOrEmpty(item.Value.ExecutablePath) ||
					Win32Import.IsWindow(item.Value.HWnd) == Win32Import.FALSE
					)
					records.Remove(item.Key);
			}
		}
	}

	public class SakuraFMORecord
	{
		public string ID { get; private set; }
		public string SakuraName { get; private set; }
		public string KeroName { get; private set; }
		public string GhostName { get; private set; }
		public string GhostPath { get; private set; }
		public string ExecutablePath { get; private set; }
		public IntPtr HWnd { get; private set; }


		public SakuraFMORecord(string id)
		{
			ID = id;
		}

		public void Parse(string key, string value)
		{
			switch (key)
			{
				case "hwnd":
					HWnd = (IntPtr)ulong.Parse(value);
					break;
				case "name":
					SakuraName = value;
					break;
				case "ghostpath":
					GhostPath = value;
					break;
				case "keroname":
					KeroName = value;
					break;
				case "fullname":
					GhostName = value;
					break;
				case "path":
					ExecutablePath = Path.Combine(value, "ssp.exe");
					break;
			}
		}
	}

	public static class Win32Import
	{
		public const int WM_COPYDATA = 0x004a;
		public const int TRUE = 1;
		public const int FALSE = 0;

		public static readonly UIntPtr SSTP_DWDATA = (UIntPtr)9801;
		public static readonly UIntPtr RECV_DWDATA = (UIntPtr)0;

		public delegate IntPtr WndProcDelegate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
		public struct NativeWindowClassEx
		{
			public uint cbSize;
			public uint style;
			[MarshalAs(UnmanagedType.FunctionPtr)]
			public WndProcDelegate lpfnWndProc;
			public int cbClsExtra;
			public int cbWndExtra;
			public IntPtr hInstance;
			public IntPtr hIcon;
			public IntPtr hCursor;
			public IntPtr hbrBackGround;
			[MarshalAs(UnmanagedType.LPStr)]
			public string lpszMenuName;
			[MarshalAs(UnmanagedType.LPStr)]
			public string lpszClassName;
			public IntPtr hIconSm;
		}

		public struct CopyDataStruct
		{
			public UIntPtr dwData;
			public uint cbData;
			public IntPtr lpData;
		}

		public enum SW
		{
			HIDE = 0,
			SHOWNORMAL = 1,
			SHOWMINIMIZED = 2,
			SHOWMAXIMIZED = 3,
			SHOWNOACTIVATE = 4,
			SHOW = 5,
			MINIMIZE = 6,
			SHOWMINNOACTIVE = 7,
			SHOWNA = 8,
			RESTORE = 9,
			SHOWDEFAULT = 10,
		}


		//れしばとしてやりとりするためのウインドウ操作系API
		[DllImport("user32.dll", EntryPoint = "RegisterClassExA", SetLastError = true)]
		public static extern ushort RegisterClassEx(ref NativeWindowClassEx classEx);
		[DllImport("kernel32.dll", EntryPoint = "GetModuleHandle", SetLastError = true)]
		public static extern IntPtr GetModuleHandle(IntPtr name);
		[DllImport("user32.dll", EntryPoint = "CreateWindowExA", SetLastError = true)]
		public static extern IntPtr CreateWindow(int exstyle, [MarshalAs(UnmanagedType.LPStr)] string className, [MarshalAs(UnmanagedType.LPStr)] string windowName, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr hInstance, IntPtr param);
		[DllImport("user32.dll", EntryPoint = "DefWindowProc", SetLastError = true)]
		public static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
		[DllImport("user32.dll", EntryPoint = "UnregisterClassA", SetLastError = true)]
		public static extern int UnregisterClass([MarshalAs(UnmanagedType.LPStr)] string className, IntPtr hInstance);
		[DllImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
		public static extern int DestroyWindow(IntPtr hWnd);
		[DllImport("user32.dll")]
		public static extern IntPtr SendMessageTimeoutA(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, IntPtr lpdwResult);
		[DllImport("user32.dll")]
		public static extern int IsWindow(IntPtr hwnd);

	}
}
