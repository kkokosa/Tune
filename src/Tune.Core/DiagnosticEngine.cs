using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tune.Core
{
    public class DiagnosticEngine
    {
        public delegate void LogHandler(string message);
        public event LogHandler Log;

        public DiagnosticEngine()
        {
            this.nativeTarget = new NativeTarget(Process.GetCurrentProcess().Id);
        }

        public DiagnosticAssembly Compile(string script, DiagnosticAssemblyMode mode, DiagnosticAssembyPlatform platform)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(script);
            UpdateLog("Script parsed.");

            string assemblyName = $"assemblyName_{DateTime.Now.Ticks}";
            OptimizationLevel compilationLevel = mode == DiagnosticAssemblyMode.Release //cbMode.SelectedItem.ToString() == "Release"
                ? OptimizationLevel.Release
                : OptimizationLevel.Debug;
            Platform compilationPlatform = platform == DiagnosticAssembyPlatform.x64 ? Platform.X64 : Platform.X86;
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Vector).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: compilationLevel,
                    allowUnsafe: true,
                    platform: compilationPlatform));

            var result = new DiagnosticAssembly(this, assemblyName, compilation);
            return result;
        }

        public void UpdateLog(string message)
        {
            this.Log?.Invoke(message);
        }

        public Symbol ResolveNativeSymbol(ulong address)
        {
            return this.nativeTarget.ResolveSymbol(address);
        }

        private NativeTarget nativeTarget;

        public string ResolveSymbol(ulong address)
        {
            using (DataTarget target =
                DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, 5000, AttachFlag.Passive))
            {
                foreach (ClrInfo version in target.ClrVersions)
                {
                    ClrRuntime runtime = target.ClrVersions.Single().CreateRuntime();
                    string methodSignature = runtime.GetMethodByAddress(address)
                        ?.GetFullSignature();
                    if (!string.IsNullOrWhiteSpace(methodSignature))
                    {
                        return methodSignature;
                    }
                }
            }

            Symbol symbol = this.nativeTarget.ResolveSymbol((ulong)address);
            if (!string.IsNullOrWhiteSpace(symbol.MethodName))
            {
                return symbol.ToString();
            }
            return null;
        }
    }
}
