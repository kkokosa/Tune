using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tune.Core
{
    public class DiagnosticEngine
    {
        public DiagnosticEngine()
        {
            this.nativeTarget = new NativeTarget(Process.GetCurrentProcess().Id);
        }

        public DiagnosticAssembly Compile(string script, DiagnosticAssemblyMode mode, DiagnosticAssembyPlatform platform)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(script);
            //UpdateLog("Script parsed.");

            string assemblyName = $"assemblyName_{DateTime.Now.Ticks}";
            OptimizationLevel compilationLevel = mode == DiagnosticAssemblyMode.Release //cbMode.SelectedItem.ToString() == "Release"
                ? OptimizationLevel.Release
                : OptimizationLevel.Debug;
            Platform compilationPlatform = platform == DiagnosticAssembyPlatform.x64 ? Platform.X64 : Platform.X86;
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: compilationLevel,
                    allowUnsafe: true,
                    platform: compilationPlatform));

            var result = new DiagnosticAssembly(this, assemblyName, compilation);
            return result;
        }

        public Symbol ResolveNativeSymbol(ulong address)
        {
            return this.nativeTarget.ResolveSymbol(address);
        }

        private NativeTarget nativeTarget;
    }
}
