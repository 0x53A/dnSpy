﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

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
using System.Diagnostics;
using System.IO;
using dnSpy.Contracts.Scripting;
using dnSpy.Contracts.TextEditor;
using dnSpy.Scripting.Roslyn.Common;
using dnSpy.Scripting.Roslyn.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;

namespace dnSpy.Scripting.Roslyn.CSharp {
	sealed class CSharpControlVM : ScriptControlVM {
		protected override string Logo {
			get {
				// This is how MS gets the version, see roslyn/src/Interactive/EditorFeatures/CSharp/Interactive/CSharpReplServiceProvider.cs
				return string.Format("Microsoft (R) Roslyn C# Compiler version {0}",
					FileVersionInfo.GetVersionInfo(typeof(CSharpCommandLineArguments).Assembly.Location).FileVersion);
			}
		}

		protected override string Help {
			get { return dnSpy_Scripting_Roslyn_Resources.HelpString; }
		}

		protected override ObjectFormatter ObjectFormatter {
			get { return CSharpObjectFormatter.Instance; }
		}

		protected override DiagnosticFormatter DiagnosticFormatter {
			get { return CSharpDiagnosticFormatter.Instance; }
		}

		public CSharpControlVM(IReplEditor replEditor, IServiceLocator serviceLocator)
			: base(replEditor, serviceLocator) {
		}

		protected override Script<T> Create<T>(string code, ScriptOptions options, Type globalsType, InteractiveAssemblyLoader assemblyLoader) {
			return CSharpScript.Create<T>(code, options, globalsType, assemblyLoader);
		}

		protected override ScriptOptions CreateScriptOptions(ScriptOptions options) {
			var rspFile = GetResponseFile("CSharpInteractive.rsp");
			if (rspFile == null)
				return options;
			this.replEditor.OutputPrintLine(string.Format(dnSpy_Scripting_Roslyn_Resources.LoadingContextFromFile, Path.GetFileName(rspFile)));
			foreach (var t in ResponseFileReader.Read(rspFile)) {
				if (t.Item1 == "/r") {
					Debug.Assert(t.Item3.Length == 0);
					if (t.Item3.Length != 0)
						continue;
					options = options.AddReferences(t.Item2);
				}
				else if (t.Item1 == "/u") {
					Debug.Assert(t.Item3.Length == 0);
					if (t.Item3.Length != 0)
						continue;
					options = options.AddImports(t.Item2);
				}
				else
					Debug.Fail(string.Format("Unknown option: '{0}'", t.Item1));
			}
			return options;
		}
	}
}
