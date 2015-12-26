﻿/*
    Copyright (C) 2014-2015 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows;
using System.Xml.Linq;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Themes;
using dnSpy.Events;
using Microsoft.Win32;

namespace dnSpy.Themes {
	[Export, Export(typeof(IThemeManager)), PartCreationPolicy(CreationPolicy.Shared)]
	sealed class ThemeManager : IThemeManager {
		readonly Dictionary<Guid, Theme> themes;

		public ITheme Theme {
			get { return theme; }
			internal set {
				if (theme != value) {
					theme = value;
					themeSettings.ThemeGuid = value.Guid;
					InitializeResources();
					earlyThemeChanged.Raise(this, new ThemeChangedEventArgs());
					themeChanged.Raise(this, new ThemeChangedEventArgs());
				}
			}
		}
		ITheme theme;

		public IEnumerable<ITheme> AllThemesSorted {
			get { return themes.Values.OrderBy(x => x.Order); }
		}

		public bool IsHighContrast {
			get { return isHighContrast; }
			set {
				if (isHighContrast != value) {
					isHighContrast = value;
					SwitchThemeIfNecessary();
				}
			}
		}
		bool isHighContrast;

		public event EventHandler<ThemeChangedEventArgs> ThemeChanged {
			add { themeChanged.Add(value); }
			remove { themeChanged.Remove(value); }
		}
		readonly WeakEventList<ThemeChangedEventArgs> themeChanged;

		public event EventHandler<ThemeChangedEventArgs> EarlyThemeChanged {
			add { earlyThemeChanged.Add(value); }
			remove { earlyThemeChanged.Remove(value); }
		}
		readonly WeakEventList<ThemeChangedEventArgs> earlyThemeChanged;

		public ThemeSettings Settings {
			get { return themeSettings; }
		}
		readonly ThemeSettings themeSettings;

		[ImportingConstructor]
		ThemeManager(ThemeSettings themeSettings) {
			this.themeSettings = themeSettings;
			this.themeChanged = new WeakEventList<ThemeChangedEventArgs>();
			this.earlyThemeChanged = new WeakEventList<ThemeChangedEventArgs>();
			this.themes = new Dictionary<Guid, Theme>();
			Load();
			Debug.Assert(themes.Count != 0);
			SystemEvents.UserPreferenceChanged += (s, e) => IsHighContrast = SystemParameters.HighContrast;
			IsHighContrast = SystemParameters.HighContrast;
			Initialize(themeSettings.ThemeGuid ?? DefaultThemeGuid);
		}

		void InitializeResources() {
			var app = Application.Current;
			Debug.Assert(app != null);
			if (app != null)
				((Theme)Theme).UpdateResources(app.Resources);
		}

		Guid CurrentDefaultThemeGuid {
			get { return IsHighContrast ? DefaultHighContrastThemeGuid : DefaultThemeGuid; }
		}
		static readonly Guid DefaultThemeGuid = ThemeConstants.THEME_DARK_GUID;
		static readonly Guid DefaultHighContrastThemeGuid = ThemeConstants.THEME_HIGHCONTRAST_GUID;

		ITheme GetThemeOrDefault(Guid guid) {
			Theme theme;
			if (themes.TryGetValue(guid, out theme))
				return theme;
			if (themes.TryGetValue(DefaultThemeGuid, out theme))
				return theme;
			return AllThemesSorted.FirstOrDefault();
		}

		void SwitchThemeIfNecessary() {
			if (Theme == null || Theme.IsHighContrast != IsHighContrast)
				Theme = GetThemeOrDefault(CurrentDefaultThemeGuid);
		}

		void Load() {
			foreach (var basePath in GetDnthemePaths()) {
				string[] files;
				try {
					if (!Directory.Exists(basePath))
						continue;
					files = Directory.GetFiles(basePath, "*.dntheme", SearchOption.TopDirectoryOnly);
				}
				catch (IOException) {
					continue;
				}
				catch (UnauthorizedAccessException) {
					continue;
				}
				catch (SecurityException) {
					continue;
				}

				foreach (var filename in files)
					Load(filename);
			}
		}

		IEnumerable<string> GetDnthemePaths() {
			return AppDirectories.GetDirectories("Themes");
		}

		Theme Load(string filename) {
			try {
				var root = XDocument.Load(filename).Root;
				if (root.Name != "theme")
					return null;

				var theme = new Theme(root);
				if (string.IsNullOrEmpty(theme.MenuName))
					return null;

				themes[theme.Guid] = theme;
				return theme;
			}
			catch (Exception) {
				Debug.Fail(string.Format("Failed to load file '{0}'", filename));
			}
			return null;
		}

		void Initialize(Guid themeGuid) {
			var theme = GetThemeOrDefault(themeGuid);
			if (theme.IsHighContrast != IsHighContrast)
				theme = GetThemeOrDefault(CurrentDefaultThemeGuid) ?? theme;
			Theme = theme;
		}
	}
}