﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace ICSharpCode.ILSpy.Options
{
	/// <summary>
	/// Interaction logic for OptionsDialog.xaml
	/// </summary>
	public partial class OptionsDialog : Window
	{
		class MefState
		{
			public static readonly MefState Instance = new MefState();

			MefState()
			{
				App.CompositionContainer.ComposeParts(this);
			}

			[ImportMany(typeof(IOptionPageCreator))]
			public Lazy<IOptionPageCreator, IOptionPageCreatorMetadata>[] optionPages = null;
		}

		readonly IOptionPage[] optionPages;
		
		public OptionsDialog()
		{
			InitializeComponent();
			ILSpySettings settings = ILSpySettings.Load();
			var creators = MefState.Instance.optionPages.OrderBy(p => p.Metadata.Order).ToArray();
			optionPages = creators.Select(p => p.Value.Create()).ToArray();
			for (int i = 0; i < creators.Length; i++) {
				TabItem tabItem = new TabItem();
				tabItem.Header = creators[i].Metadata.Title;
				tabItem.Content = optionPages[i];
				tabControl.Items.Add(tabItem);
				
				optionPages[i].Load(settings);
			}
		}
		
		void OKButton_Click(object sender, RoutedEventArgs e)
		{
			ILSpySettings.Update(
				delegate (XElement root) {
					foreach (var optionPage in optionPages)
						optionPage.Save(root);
				});
			this.DialogResult = true;
			Close();
		}
	}

	public interface IOptionPageCreator
	{
		IOptionPage Create();
	}
	
	public interface IOptionPageCreatorMetadata
	{
		string Title { get; }
		int Order { get; }
	}
	
	public interface IOptionPage
	{
		void Load(ILSpySettings settings);
		void Save(XElement root);
	}
	
	[MetadataAttribute]
	[AttributeUsage(AttributeTargets.Class, AllowMultiple=false)]
	public class ExportOptionPageAttribute : ExportAttribute, IOptionPageCreatorMetadata
	{
		public ExportOptionPageAttribute() : base(typeof(IOptionPageCreator))
		{ }
		
		public string Title { get; set; }
		
		public int Order { get; set; }
	}
	
	[ExportMainMenuCommand(Menu = "_View", Header = "_Options", MenuCategory = "Options", MenuOrder = 999)]
	sealed class ShowOptionsCommand : SimpleCommand
	{
		public override void Execute(object parameter)
		{
			OptionsDialog dlg = new OptionsDialog();
			dlg.Owner = MainWindow.Instance;
			if (dlg.ShowDialog() == true) {
				MainWindow.Instance.Refresh();
			}
		}
	}
}