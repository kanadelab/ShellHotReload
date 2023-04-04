using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellHotReload
{
	internal class ShellWatcher : IDisposable
	{
		private FileSystemWatcher pngWatcher;
		private FileSystemWatcher txtWatcher;
		public event Action Changed;

		public ShellWatcher(string path)
		{
			//profileの更新に巻き込まれないように、txtはフォルダ直下のみ、pngは全体とする
			pngWatcher = new FileSystemWatcher(path);
			pngWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
			pngWatcher.Filter = "*.png";
			pngWatcher.IncludeSubdirectories = true;
			pngWatcher.EnableRaisingEvents = true;
			pngWatcher.Renamed += Watcher_Renamed;
			pngWatcher.Changed += Watcher_Changed;
			pngWatcher.Created += Watcher_Created;

			txtWatcher = new FileSystemWatcher(path);
			txtWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
			txtWatcher.Filter = "*.txt";
			txtWatcher.IncludeSubdirectories = false;
			txtWatcher.EnableRaisingEvents = true;
			txtWatcher.Renamed += Watcher_Renamed;
			txtWatcher.Changed += Watcher_Changed;
			txtWatcher.Created += Watcher_Created;
		}

		private void Watcher_Renamed(object sender, RenamedEventArgs e)
		{
			if(Changed != null)
				Changed.Invoke();
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			if (Changed != null)
				Changed.Invoke();
		}

		private void Watcher_Changed(object sender, FileSystemEventArgs e)
		{
			if (Changed != null)
				Changed.Invoke();
		}

		public void Dispose()
		{
			if(pngWatcher != null)
			{
				pngWatcher.Dispose();
				pngWatcher = null;
			}

			if(txtWatcher != null)
			{
				txtWatcher.Dispose();
				txtWatcher = null;
			}

			if(Changed != null)
			{
				Changed = null;
			}
		}
	}
}
