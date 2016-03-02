// Copyright (c) AlphaSierraPapa for the SharpDevelop Team
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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnSpy.Decompiler.Shared;
using ICSharpCode.Decompiler.Ast;
using ICSharpCode.Decompiler.Tests.Helpers;
using Microsoft.CSharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace ICSharpCode.Decompiler.Tests
{
	public abstract class DecompilerTestBase
	{
		protected static void ValidateFileRoundtrip(string samplesFileName)
		{
			var fullPath = Path.Combine(@"..\..\Tests", samplesFileName);
			AssertRoundtripCode(fullPath);
		}

		static string RemoveIgnorableLines(IEnumerable<string> lines)
		{
			return CodeSampleFileParser.ConcatLines(lines.Where(l => !CodeSampleFileParser.IsCommentOrBlank(l)));
		}

		protected static void AssertRoundtripCode(string fileName, bool optimize = false, bool useDebug = false, int compilerVersion = 4)
		{
			var code = RemoveIgnorableLines(File.ReadLines(fileName));
			AssemblyDef assembly = CompileRoslyn(code, optimize, useDebug);

			AstBuilder decompiler = AstBuilder.CreateAstBuilderTestContext(assembly.ManifestModule);
			decompiler.AddAssembly(assembly);
			new Helpers.RemoveCompilerAttribute().Run(decompiler.SyntaxTree);

			StringWriter output = new StringWriter();
			decompiler.GenerateCode(new PlainTextOutput(output));
			CodeAssert.AreEqual(code, output.ToString());
		}

		protected static AssemblyDef CompileLegacy(string code, bool optimize, bool useDebug, int compilerVersion)
		{
			CSharpCodeProvider provider = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v" + new Version(compilerVersion, 0) } });
			CompilerParameters options = new CompilerParameters();
			options.CompilerOptions = "/unsafe /o" + (optimize ? "+" : "-") + (useDebug ? " /debug" : "");
			if (compilerVersion >= 4)
				options.ReferencedAssemblies.Add("System.Core.dll");
			CompilerResults results = provider.CompileAssemblyFromSource(options, code);
			try
			{
				if (results.Errors.Count > 0)
				{
					StringBuilder b = new StringBuilder("Compiler error:");
					foreach (var error in results.Errors)
					{
						b.AppendLine(error.ToString());
					}
					throw new Exception(b.ToString());
				}
				return Utils.OpenModule(results.PathToAssembly).Assembly;
			}
			finally
			{
				File.Delete(results.PathToAssembly);
				results.TempFiles.Delete();
			}
		}

		protected static AssemblyDef CompileRoslyn(string code, bool optimize, bool useDebug)
		{
			var syntaxTree = CSharpSyntaxTree.ParseText(code);

			string assemblyName = Path.GetRandomFileName();
			MetadataReference[] references = new MetadataReference[]
			{
	MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
	MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
			};

			CSharpCompilation compilation = CSharpCompilation.Create(
				assemblyName,
				syntaxTrees: new[] { syntaxTree },
				references: references,
				options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));


			using (var ms = new MemoryStream())
			{
				EmitResult result = compilation.Emit(ms);

				if (!result.Success)
				{
					IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
						diagnostic.IsWarningAsError ||
						diagnostic.Severity == DiagnosticSeverity.Error);

					foreach (Diagnostic diagnostic in failures)
					{
						Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
					}
					throw new Exception("Roslyn compilation failed!");
				}
				else
				{
					return AssemblyDef.Load(ms.ToArray());
				}
			}

		}
	}
}
