using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ShellHotReload
{
	internal class SSTPSender
	{
		private static readonly Encoding ShiftJIS = Encoding.GetEncoding("Shift_JIS");

		//SSPへの単純データ送信
		public static void RaiseSSTP(ProtocolBuilder data, SakuraFMORecord target)
		{
			var serializedData = data.Serialize();
			var dataBytes = ShiftJIS.GetBytes(serializedData);
			var dataPtr = Marshal.AllocHGlobal(dataBytes.Length);
			Marshal.Copy(dataBytes, 0, dataPtr, dataBytes.Length);

			var copydata = new Win32Import.CopyDataStruct()
			{
				dwData = Win32Import.SSTP_DWDATA,
				cbData = (uint)dataBytes.Length,
				lpData = dataPtr
			};
			var h = GCHandle.Alloc(copydata, GCHandleType.Pinned);

			//TODO: マジックナンバーフラグの定数化
			Win32Import.SendMessageTimeoutA(target.HWnd, Win32Import.WM_COPYDATA, IntPtr.Zero, h.AddrOfPinnedObject(), 2, 5000, IntPtr.Zero);

			Marshal.FreeHGlobal(dataPtr);
		}

		//SEND SSTPの送信
		public static void SendSSTP(SakuraFMORecord fmoRecord, string script, bool useOwnedSSTP = true, bool noTranslate = true, IntPtr hWnd = default(IntPtr))
		{
			var sstpBuilder = new ProtocolBuilder();
			sstpBuilder.Command = "SEND SSTP/1.0";
			sstpBuilder.Parameters["Script"] = script;
			sstpBuilder.Parameters["Charset"] = "Shift_JIS";
			sstpBuilder.Parameters["Sender"] = "うかしゃーぷ";

			if (hWnd != default(IntPtr))
			{
				sstpBuilder.Parameters["HWnd"] = hWnd.ToString();
			}

			if (noTranslate)
			{
				sstpBuilder.Parameters["Option"] = "notranslate";
			}

			if (useOwnedSSTP)
			{
				sstpBuilder.Parameters["ID"] = fmoRecord.ID;
			}

			RaiseSSTP(sstpBuilder, fmoRecord);
		}
	}

	//SHIORI等のプロトコルビルダ
	public class ProtocolBuilder
	{
		public const string CommandGetShiori = "GET SHIORI/3.0";

		public string Command { get; set; }
		//EXECUTEについてくる追加データ
		public string AppendData { get; set; }
		public Dictionary<string, string> Parameters { get; private set; }

		public ProtocolBuilder()
		{
			Parameters = new Dictionary<string, string>();
			Command = string.Empty;
		}

		public string Serialize()
		{
			var param = string.Join("\r\n", Parameters.Select(o => string.Format("{0}: {1}", o.Key, o.Value)));
			return string.Format("{1}{0}{2}{0}{0}", "\r\n", Command, param);
		}
	}
}
