using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using UnityEditor;
using UnityEditor.Compilation;

namespace Needle.SelectiveProfiling.Analysis
{
	internal static class TypeFinder
	{
		public static void FindTypes(MonoScript script, List<Type> types)
		{
			if (!script) return;
			var type = script.GetClass();
			if (type != null)
				types.Add(type);

			// var tree = CSharpSyntaxTree.ParseText(script.text);
			// var collector = new TypesCollector();
			// collector.Visit(tree.GetRoot());
			// if (collector.FullTypeNames != null)
			// 	Debug.Log("FOUND TYPES " + string.Join("\n", collector.FullTypeNames));
		}

		// private const string NESTED_CLASS_DELIMITER = "+";
		// private const string NAMESPACE_CLASS_DELIMITER = ".";
		//
		// public static string GetFullName(this ClassDeclarationSyntax source)
		// {
		// 	Debug.Assert(source != null);
		//
		// 	var items = new List<string>();
		// 	var parent = source.Parent;
		// 	while (parent.IsKind(SyntaxKind.ClassDeclaration))
		// 	{
		// 		var parentClass = parent as ClassDeclarationSyntax;
		// 		Debug.Assert(null != parentClass);
		// 		items.Add(parentClass.Identifier.Text);
		// 		parent = parent.Parent;
		// 	}
		//
		// 	var nameSpace = parent as NamespaceDeclarationSyntax;
		// 	var sb = new StringBuilder();
		// 	if (nameSpace != null)
		// 	{
		// 		sb.Append(nameSpace.Name);
		// 		sb.Append(NAMESPACE_CLASS_DELIMITER);
		// 	}
		//
		// 	items.Reverse();
		// 	items.ForEach(i => { sb.Append(i).Append(NESTED_CLASS_DELIMITER); });
		// 	sb.Append(source.Identifier.Text);
		//
		// 	var result = sb.ToString();
		// 	return result;
		// }


		private static Assembly[] _cachedAssemblies;

		private static Assembly[] CachedAssemblies
		{
			get
			{
				if (_cachedAssemblies == null)
					_cachedAssemblies = CompilationPipeline.GetAssemblies();
				return _cachedAssemblies;
			}
		}

		private static DefaultAssemblyResolver unityResolver;

		internal static DefaultAssemblyResolver UnityResolver
		{
			get
			{
				if (unityResolver != null) return unityResolver;

				// set up assembly resolver so we can go from AssemblyNameReference to AssemblyDefinition
				var resolver = new DefaultAssemblyResolver();
#if UNITY_2019_1_OR_NEWER
				foreach (var p in CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All))
#else
                foreach (var p in CompilationPipeline.GetPrecompiledAssemblyNames().Select(x => CompilationPipeline.GetPrecompiledAssemblyPathFromAssemblyName(x)))
#endif
					resolver.AddSearchDirectory(Path.GetDirectoryName(p));
				foreach (var p in CachedAssemblies)
					resolver.AddSearchDirectory(Path.GetDirectoryName(p.outputPath));

				unityResolver = resolver;
				return unityResolver;
			}
		}
	}

	// internal class TypesCollector : CSharpSyntaxWalker
	// {
	// 	[CanBeNull] public List<string> FullTypeNames;
	//
	// 	public override void VisitClassDeclaration(ClassDeclarationSyntax node)
	// 	{
	// 		base.VisitClassDeclaration(node);
	// 		var name = node.GetFullName();
	// 		if (FullTypeNames == null) FullTypeNames = new List<string>();
	// 		FullTypeNames.Add(name);
	// 	}
	// }
}