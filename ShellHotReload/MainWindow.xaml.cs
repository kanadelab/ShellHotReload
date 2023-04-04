using ShellHotReload;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ShellHotrReload
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			DataContext = new MainViewModel();
		}
	}

	internal class MainViewModel : NotificationObject
	{
		private Timer reloadTimer;
		private ShellWatcher watcher;
		private List<GhostViewModel> ghosts;
		private GhostViewModel selectedGhost;
		private string selectedShell;
		private string afterReloadScript;

		public ReadOnlyCollection<GhostViewModel> Ghosts => new ReadOnlyCollection<GhostViewModel>(ghosts);
		public GhostViewModel SelectedGhost
		{
			get => selectedGhost;
			set
			{
				if(selectedGhost != value)
				{
					selectedGhost = value;
					//ゴーストが変更されたらシェルを未選択にする
					SelectedShell = null;
					NotifyChanged();
					UpdateWatcher();
				}
			}
		}

		public string SelectedShell
		{
			get => selectedShell;
			set
			{
				if(selectedShell != value)
				{
					selectedShell = value;
					NotifyChanged();
					UpdateWatcher();
				}
			}
		}

		public string AfterReloadScript
		{
			get => afterReloadScript;
			set
			{
				afterReloadScript = value;
				NotifyChanged();
			}
		}

		public MainViewModel()
		{
			afterReloadScript = @"\0\s[0]\_w[1000000]";
			Reload();
		}

		public void Reload()
		{
			SakuraFMOReader reader = new SakuraFMOReader();
			reader.Read();
			ghosts = new List<GhostViewModel>(reader.Records.Select(o => new GhostViewModel(o.Value)));
		}

		private void UpdateWatcher()
		{
			lock (this)
			{
				if (watcher != null)
				{
					watcher.Dispose();
					watcher = null;
				}

				if (!string.IsNullOrEmpty(selectedShell) && selectedGhost != null)
				{
					watcher = new ShellWatcher(System.IO.Path.Combine(selectedGhost.Path, "shell", selectedShell));
					watcher.Changed += () => { ShellChanged(); };
				}
			}
		}

		private void ShellChanged()
		{
			lock (this)
			{
				//多発回避のために少し待つ
				if (reloadTimer != null)
					return;
				reloadTimer = new Timer();
				reloadTimer.AutoReset = false;
				reloadTimer.Interval = 100;
				reloadTimer.Elapsed += ReloadTimer_Elapsed;
				reloadTimer.Start();
			}
		}

		private void ReloadTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			lock(this)
			{
				reloadTimer.Dispose();
				reloadTimer = null;
			}

			if (selectedGhost != null)
			{
				SSTPSender.SendSSTP(selectedGhost.FMORecord, @"\![reload,shell]"+ afterReloadScript);
			}
		}
	}

	internal class GhostViewModel
	{
		private SakuraFMORecord fmoRecord;
		private List<string> shellDirectories;

		public SakuraFMORecord FMORecord => fmoRecord;
		public string Name => fmoRecord.GhostName;
		public string Path => fmoRecord.GhostPath;
		public ReadOnlyCollection<string> ShellDirectories => new ReadOnlyCollection<string>(shellDirectories);

		public GhostViewModel(SakuraFMORecord record)
		{
			fmoRecord = record;
			shellDirectories = new List<string>(Directory.GetDirectories(System.IO.Path.Combine(fmoRecord.GhostPath, "shell")).Select(o => System.IO.Path.GetFileName(o)));
		}
	}


	public class NotificationObject : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		protected void NotifyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
