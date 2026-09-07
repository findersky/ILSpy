// Copyright (c) 2026 AlphaSierraPapa for the SharpDevelop Team
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

using ICSharpCode.BamlDecompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.ILSpyCmd
{
	static class ResourceExtensions
	{
		public static IEnumerable<string> EnumerateResourcePaths(MetadataFile module)
		{
			foreach (var r in module.Resources.Where(r => r.ResourceType == ResourceType.Embedded))
			{
				IReadOnlyList<string> entries;
				if (r.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)
					&& TryReadResourcesEntryNames(r, out entries))
				{
					foreach (var name in entries)
						yield return r.Name + "/" + name;
				}
				else
				{
					yield return r.Name;
				}
			}
		}

		public static bool TryGetResource(MetadataFile module, string resourcePath, out object value)
		{
			foreach (var r in module.Resources.Where(r => r.ResourceType == ResourceType.Embedded))
			{
				if (string.Equals(resourcePath, r.Name, StringComparison.OrdinalIgnoreCase))
				{
					using Stream stream = r.TryOpenStream();
					if (stream == null)
					{
						value = null;
						return false;
					}
					stream.Position = 0;
					var ms = new MemoryStream();
					stream.CopyTo(ms);
					value = ms.ToArray();
					return true;
				}

				string prefix = r.Name + "/";
				if (r.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)
					&& resourcePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					string entryName = resourcePath.Substring(prefix.Length);
					if (TryReadResourcesEntry(r, entryName, out value))
						return true;
				}
			}

			value = null;
			return false;
		}

		public static XDocument DecompileBaml(MetadataFile module, IAssemblyResolver resolver, Stream bamlStream, BamlDecompilerSettings settings, CancellationToken cancellationToken)
		{
			var typeSystem = new BamlDecompilerTypeSystem(module, resolver);
			var decompiler = new XamlDecompiler(typeSystem, settings) {
				CancellationToken = cancellationToken
			};
			return decompiler.Decompile(bamlStream).Xaml;
		}

		static bool TryReadResourcesEntryNames(Resource r, out IReadOnlyList<string> names)
		{
			using Stream stream = r.TryOpenStream();
			if (stream == null)
			{
				names = Array.Empty<string>();
				return false;
			}
			stream.Position = 0;

			ResourcesFile resourcesFile;
			try
			{
				resourcesFile = new ResourcesFile(stream);
			}
			catch (Exception ex) when (ex is BadImageFormatException || ex is EndOfStreamException)
			{
				names = null;
				return false;
			}

			using (resourcesFile)
			{
				var list = new List<string>(resourcesFile.ResourceCount);
				for (int i = 0; i < resourcesFile.ResourceCount; i++)
					list.Add(resourcesFile.GetResourceName(i));
				names = list;
				return true;
			}
		}

		static bool TryReadResourcesEntry(Resource r, string entryName, out object value)
		{
			using Stream stream = r.TryOpenStream();
			if (stream == null)
			{
				value = null;
				return false;
			}
			stream.Position = 0;

			ResourcesFile resourcesFile;
			try
			{
				resourcesFile = new ResourcesFile(stream);
			}
			catch (Exception ex) when (ex is BadImageFormatException || ex is EndOfStreamException)
			{
				value = null;
				return false;
			}

			using (resourcesFile)
			{
				for (int i = 0; i < resourcesFile.ResourceCount; i++)
				{
					if (!string.Equals(resourcesFile.GetResourceName(i), entryName, StringComparison.OrdinalIgnoreCase))
						continue;

					object entryValue = resourcesFile.GetResourceValue(i);
					if (entryValue is ResourceSerializedObject serialized)
					{
						value = serialized.GetBytes();
					}
					else if (entryValue is Stream s)
					{
						var ms = new MemoryStream();
						s.Position = 0;
						s.CopyTo(ms);
						value = ms.ToArray();
					}
					else
					{
						value = entryValue;
					}
					return true;
				}
			}

			value = null;
			return false;
		}
	}
}
