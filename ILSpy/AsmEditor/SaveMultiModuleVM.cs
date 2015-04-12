﻿
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using dnlib.DotNet;

namespace ICSharpCode.ILSpy.AsmEditor
{
	sealed class SaveMultiModuleVM : INotifyPropertyChanged
	{
		ObservableCollection<SaveModuleOptionsVM> modules = new ObservableCollection<SaveModuleOptionsVM>();

		enum SaveState
		{
			/// <summary>
			/// We haven't started saving yet
			/// </summary>
			Loaded,

			/// <summary>
			/// We're saving
			/// </summary>
			Saving,

			/// <summary>
			/// We're canceling
			/// </summary>
			Canceling,

			/// <summary>
			/// Final state even if some files weren't saved
			/// </summary>
			Saved,
		}

		SaveState State {
			get { return saveState; }
			set {
				if (value != saveState) {
					saveState = value;
					OnPropertyChanged("IsLoaded");
					OnPropertyChanged("IsSaving");
					OnPropertyChanged("IsCanceling");
					OnPropertyChanged("IsSaved");
					OnPropertyChanged("CanSave");
					OnPropertyChanged("CanCancel");
					OnPropertyChanged("CanClose");
					OnPropertyChanged("IsSavingOrCanceling");

					if (saveState == SaveState.Saved && OnSavedEvent != null)
						OnSavedEvent(this, EventArgs.Empty);
				}
			}
		}
		SaveState saveState = SaveState.Loaded;

		public ICommand SaveCommand {
			get { return saveCommand ?? (saveCommand = new RelayCommand(a => Save(), a => IsLoaded)); }
		}
		ICommand saveCommand;

		public ICommand CancelSaveCommand {
			get { return cancelSaveCommand ?? (cancelSaveCommand = new RelayCommand(a => CancelSave(), a => IsSaving && moduleSaver != null)); }
		}
		ICommand cancelSaveCommand;

		public event EventHandler OnSavedEvent;

		public bool IsLoaded {
			get { return State == SaveState.Loaded; }
		}

		public bool IsSaving {
			get { return State == SaveState.Saving; }
		}

		public bool IsCanceling {
			get { return State == SaveState.Canceling; }
		}

		public bool IsSaved {
			get { return State == SaveState.Saved; }
		}

		public bool CanSave {
			get { return IsLoaded; }
		}

		public bool CanCancel {
			get { return IsLoaded || IsSaving; }
		}

		public bool CanClose {
			get { return !CanCancel; }
		}

		public bool IsSavingOrCanceling {
			get { return IsSaving || IsCanceling; }
		}

		public bool HasError {
			get { return hasError; }
			private set {
				if (hasError != value) {
					hasError = value;
					OnPropertyChanged("HasError");
					OnPropertyChanged("HasNoError");
				}
			}
		}
		bool hasError;

		public bool HasNoError {
			get { return !HasError; }
		}

		public int ErrorCount {
			get { return errorCount; }
			set {
				if (errorCount != value) {
					errorCount = value;
					OnPropertyChanged("ErrorCount");
					HasError = errorCount != 0;
				}
			}
		}
		int errorCount;

		public string LogMessage {
			get { return logMessage.ToString(); }
		}
		StringBuilder logMessage = new StringBuilder();

		public double ProgressMinimum {
			get { return 0; }
		}

		public double ProgressMaximum {
			get { return 100; }
		}

		public double TotalProgress {
			get { return totalProgress; }
			private set {
				if (totalProgress != value) {
					totalProgress = value;
					OnPropertyChanged("TotalProgress");
				}
			}
		}
		double totalProgress = 0;

		public double CurrentFileProgress {
			get { return currentFileProgress; }
			private set {
				if (currentFileProgress != value) {
					currentFileProgress = value;
					OnPropertyChanged("CurrentFileProgress");
				}
			}
		}
		double currentFileProgress = 0;

		public string CurrentFileName {
			get { return currentFileName; }
			set {
				if (currentFileName != value) {
					currentFileName = value;
					OnPropertyChanged("CurrentFileName");
				}
			}
		}
		string currentFileName = string.Empty;

		public ObservableCollection<SaveModuleOptionsVM> Modules {
			get { return modules; }
		}

		public SaveMultiModuleVM(SaveModuleOptionsVM options)
		{
			this.modules.Add(options);
		}

		public SaveMultiModuleVM(IEnumerable<ModuleDef> modules)
		{
			this.modules.AddRange(modules.Select(m => new SaveModuleOptionsVM(m)));
		}

		public bool WasSaved(ModuleDef module)
		{
			var data = modules.FirstOrDefault(a => a.Module == module);
			if (data == null)
				return false;
			bool saved;
			savedFile.TryGetValue(data, out saved);
			return saved;
		}

		public void Save()
		{
			if (!IsLoaded)
				return;
			State = SaveState.Saving;
			TotalProgress = 0;
			CurrentFileProgress = 0;
			CurrentFileName = string.Empty;
			savedFile.Clear();

			var mods = modules.ToArray();
			new Thread(() => SaveAsync(mods)).Start();
		}

		void ExecInOldThread(Action action)
		{
			var disp = App.Current.Dispatcher;
			if (disp != null)
				disp.BeginInvoke(DispatcherPriority.Background, action);
		}

		ModuleSaver moduleSaver;
		void SaveAsync(SaveModuleOptionsVM[] mods)
		{
			try {
				moduleSaver = new ModuleSaver(mods);
				moduleSaver.OnProgressUpdated += moduleSaver_OnProgressUpdated;
				moduleSaver.OnLogMessage += moduleSaver_OnLogMessage;
				moduleSaver.OnWritingFile += moduleSaver_OnWritingFile;
				moduleSaver.SaveAll();
				AsyncAddMessage("All files written to disk.", false, false);
			}
			catch (TaskCanceledException) {
				AsyncAddMessage("Save was canceled by user.", true, false);
			}
			catch (UnauthorizedAccessException ex) {
				AsyncAddMessage(string.Format("Access error: {0}", ex.Message), true, false);
			}
			catch (IOException ex) {
				AsyncAddMessage(string.Format("File error: {0}", ex.Message), true, false);
			}
			catch (Exception ex) {
				AsyncAddMessage(string.Format("An exception occurred\n\n{0}", ex), true, false);
			}
			moduleSaver = null;

			ExecInOldThread(() => {
				CurrentFileName = string.Empty;
				State = SaveState.Saved;
			});
		}

		void moduleSaver_OnWritingFile(object sender, ModuleSaverWriteEventArgs e)
		{
			if (e.Starting) {
				ExecInOldThread(() => {
					CurrentFileName = e.File.FileName;
				});
				AsyncAddMessage(string.Format("Writing {0}...", e.File.FileName), false, false);
			}
			else {
				shownMessages.Clear();
				savedFile.Add(e.File, true);
			}
		}
		Dictionary<SaveModuleOptionsVM, bool> savedFile = new Dictionary<SaveModuleOptionsVM, bool>();

		void moduleSaver_OnProgressUpdated(object sender, EventArgs e)
		{
			var moduleSaver = (ModuleSaver)sender;
			double totalProgress = 100 * moduleSaver.TotalProgress;
			double currentFileProgress = 100 * moduleSaver.CurrentFileProgress;
			ExecInOldThread(() => {
				this.TotalProgress = totalProgress;
				this.CurrentFileProgress = currentFileProgress;
			});
		}

		void moduleSaver_OnLogMessage(object sender, ModuleSaverLogEventArgs e)
		{
			AsyncAddMessage(e.Message, e.Event == ModuleSaverLogEvent.Error || e.Event == ModuleSaverLogEvent.Warning, true);
		}

		void AsyncAddMessage(string msg, bool isError, bool canIgnore)
		{
			// If there are a lot of errors, we don't want to add a ton of extra delegates to be
			// called in the old thread. Just use one so we don't slow down everything to a crawl.
			lock (addMessageStringBuilder) {
				if (!canIgnore || !shownMessages.Contains(msg)) {
					addMessageStringBuilder.AppendLine(msg);
					if (canIgnore)
						shownMessages.Add(msg);
				}
				if (isError)
					errors++;
				if (!hasAddedMessage) {
					hasAddedMessage = true;
					ExecInOldThread(() => {
						string logMsgTmp;
						int errorsTmp;
						lock (addMessageStringBuilder) {
							logMsgTmp = addMessageStringBuilder.ToString();
							errorsTmp = errors;

							hasAddedMessage = false;
							addMessageStringBuilder.Clear();
							errors = 0;
						}

						ErrorCount += errorsTmp;
						logMessage.Append(logMsgTmp);
						OnPropertyChanged("LogMessage");
					});
				}
			}
		}
		HashSet<string> shownMessages = new HashSet<string>(StringComparer.Ordinal);
		StringBuilder addMessageStringBuilder = new StringBuilder();
		int errors;
		bool hasAddedMessage;

		public void CancelSave()
		{
			if (!IsSaving)
				return;
			var ms = moduleSaver;
			if (ms == null)
				return;

			State = SaveState.Canceling;
			ms.CancelAsync();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		void OnPropertyChanged(string propName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(propName));
		}
	}
}