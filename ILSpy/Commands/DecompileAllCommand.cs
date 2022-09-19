// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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

#if DEBUG

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy.Properties;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpyX;

using TomsToolbox.Essentials;

namespace ICSharpCode.ILSpy
{
	[ExportMainMenuCommand(ParentMenuID = nameof(Resources._File), Header = nameof(Resources.DEBUGDecompile), MenuCategory = nameof(Resources.Open), MenuOrder = 2.5)]
	sealed class DecompileAllCommand : SimpleCommand
	{
		public override bool CanExecute(object parameter)
		{
			return System.IO.Directory.Exists("c:\\temp\\decompiled");
		}

		public override void Execute(object parameter)
		{
			Docking.DockWorkspace.Instance.RunWithCancellation(ct => Task<AvalonEditTextOutput>.Factory.StartNew(() => {
				AvalonEditTextOutput output = new AvalonEditTextOutput();
				Parallel.ForEach(
					Partitioner.Create(MainWindow.Instance.CurrentAssemblyList.GetAssemblies(), loadBalance: true),
					new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
					delegate (LoadedAssembly asm) {
						if (!asm.HasLoadError)
						{
							Stopwatch w = Stopwatch.StartNew();
							Exception exception = null;
							using (var writer = new System.IO.StreamWriter("c:\\temp\\decompiled\\" + asm.ShortName + ".cs"))
							{
								try
								{
									var options = MainWindow.Instance.CreateDecompilationOptions();
									options.CancellationToken = ct;
									options.FullDecompilation = true;
									new CSharpLanguage().DecompileAssembly(asm, new PlainTextOutput(writer), options);
								}
								catch (Exception ex)
								{
									writer.WriteLine(ex.ToString());
									exception = ex;
								}
							}
							lock (output)
							{
								output.Write(asm.ShortName + " - " + w.Elapsed);
								if (exception != null)
								{
									output.Write(" - ");
									output.Write(exception.GetType().Name);
								}
								output.WriteLine();
							}
						}
					});
				return output;
			}, ct)).Then(output => Docking.DockWorkspace.Instance.ShowText(output)).HandleExceptions();
		}
	}

	[ExportMainMenuCommand(ParentMenuID = nameof(Resources._File), Header = nameof(Resources.DEBUGDecompile100x), MenuCategory = nameof(Resources.Open), MenuOrder = 2.6)]
	sealed class Decompile100TimesCommand : SimpleCommand
	{
		public override void Execute(object parameter)
		{
			const int numRuns = 100;
			var language = MainWindow.Instance.CurrentLanguage;
			var nodes = MainWindow.Instance.SelectedNodes.ToArray();
			var options = MainWindow.Instance.CreateDecompilationOptions();
			Docking.DockWorkspace.Instance.RunWithCancellation(ct => Task<AvalonEditTextOutput>.Factory.StartNew(() => {
				options.CancellationToken = ct;
				Stopwatch w = Stopwatch.StartNew();
				for (int i = 0; i < numRuns; ++i)
				{
					foreach (var node in nodes)
					{
						node.Decompile(language, new PlainTextOutput(), options);
					}
				}
				w.Stop();
				AvalonEditTextOutput output = new AvalonEditTextOutput();
				double msPerRun = w.Elapsed.TotalMilliseconds / numRuns;
				output.Write($"Average time: {msPerRun.ToString("f1")}ms\n");
				return output;
			}, ct)).Then(output => Docking.DockWorkspace.Instance.ShowText(output)).HandleExceptions();
		}
	}
}

#endif