using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Diagnostics.Runtime;
using Mono.Cecil;
using SharpDisasm;
using SharpDisasm.Translators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tune.Core
{
    public class DiagnosticAssembly : IDisposable
    {
        private DiagnosticEngine engine;
        private MemoryStream dllStream;
        private MemoryStream pdbStream;
        private Assembly assembly;
        private string assemblyName;
        private ulong currentMethodAddress = 0;

        public DiagnosticAssembly(DiagnosticEngine engine, string assemblyName, CSharpCompilation compilation)
        {
            this.engine = engine;
            this.assemblyName = assemblyName;
            this.engine.UpdateLog($"Script compilation into assembly {assemblyName}.");
            this.dllStream = new MemoryStream();
            this.pdbStream = new MemoryStream();
            var emitResult = compilation.Emit(this.dllStream, this.pdbStream);
            if (!emitResult.Success)
            {
                var x = emitResult.Diagnostics;
                this.engine.UpdateLog($"Script compilation failed: {string.Join(Environment.NewLine, x.Select(d => d.ToString()))}.");
            }
            else
            {
                this.engine.UpdateLog("Script compilation succeeded.");
                this.dllStream.Seek(0, SeekOrigin.Begin);
                this.assembly = Assembly.Load(this.dllStream.ToArray());
                this.engine.UpdateLog("Dynamic assembly loaded.");
            }
        }

        public string Execute(string argument)
        {
            Type type = assembly.GetTypes().First();
            MethodInfo mi = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).First();
            object obj = Activator.CreateInstance(type);
            this.engine.UpdateLog($"Object with type {type.FullName} and method {mi.Name} resolved.");

            object result = null;
            try
            {
                TextWriter programWriter = new StringWriter();
                Console.SetOut(programWriter);
                this.engine.UpdateLog($"Invoking method {mi.Name} with argument {argument}");
                result = mi.Invoke(obj, new object[] { argument });
                this.engine.UpdateLog($"Script result: {result}");
                this.engine.UpdateLog("Script log:");
                this.engine.UpdateLog(programWriter.ToString());
                return result.ToString();
            }
            catch (Exception e)
            {
                this.engine.UpdateLog($"Script execution failed: {e.ToString()}");
                return e.ToString();
            }
        }

        public string DumpIL()
        {
            TextWriter ilWriter = new StringWriter();
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(this.dllStream);
            var ilOutput = new PlainTextOutput(ilWriter);
            var reflectionDisassembler = new ReflectionDisassembler(ilOutput, false, CancellationToken.None);
            reflectionDisassembler.WriteModuleContents(assemblyDefinition.MainModule);
            this.engine.UpdateLog("Dynamic assembly disassembled to IL.");
            return ilWriter.ToString();
        }

        public string DumpASM()
        {
            TextWriter asmWriter = new StringWriter();
            using (DataTarget target = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, 5000, AttachFlag.Passive))
            {
                foreach (ClrInfo clrInfo in target.ClrVersions)
                {
                    this.engine.UpdateLog("Found CLR Version:" + clrInfo.Version.ToString());

                    // This is the data needed to request the dac from the symbol server:
                    ModuleInfo dacInfo = clrInfo.DacInfo;
                    this.engine.UpdateLog($"Filesize:  {dacInfo.FileSize:X}");
                    this.engine.UpdateLog($"Timestamp: {dacInfo.TimeStamp:X}");
                    this.engine.UpdateLog($"Dac File:  {dacInfo.FileName}");

                    ClrRuntime runtime = target.ClrVersions.Single().CreateRuntime();
                    var appDomain = runtime.AppDomains[0];
                    var module = appDomain.Modules.LastOrDefault(m => m.AssemblyName != null && m.AssemblyName.StartsWith(assemblyName));
                    
                    asmWriter.WriteLine(
                        $"; {clrInfo.ModuleInfo.ToString()} ({clrInfo.Flavor} {clrInfo.Version})");
                    asmWriter.WriteLine(
                        $"; {clrInfo.DacInfo.FileName} ({clrInfo.DacInfo.TargetArchitecture} {clrInfo.DacInfo.Version})");
                    asmWriter.WriteLine();
                    foreach (var typeClr in module.EnumerateTypes())
                    {
                        // Note: Accroding to https://github.com/dotnet/coreclr/blob/master/src/vm/methodtable.h:
                        // "(...) for value types GetBaseSize returns the size of instance fields for
                        // a boxed value, and GetNumInstanceFieldsBytes for an unboxed value."
                        // but mentioned method implementation is trivial:
                        // inline DWORD MethodTable::GetNumInstanceFieldBytes()
                        //{
                        //    LIMITED_METHOD_DAC_CONTRACT;
                        //    return (GetBaseSize() - GetClass()->GetBaseSizePadding());
                        //}

                        asmWriter.WriteLine($"; Type {typeClr.Name}");
                        asmWriter.WriteLine($";    MethodTable: 0x{typeClr.MethodTable:x16}");
                        asmWriter.WriteLine($";    Size:        {typeClr.BaseSize}{(typeClr.IsValueClass ? string.Format(" (when boxed)") : string.Empty)}");
                        asmWriter.WriteLine($";    IsValueType: {typeClr.IsValueClass}");
                        asmWriter.WriteLine($";    IsArray:     {typeClr.IsArray}");
                        asmWriter.WriteLine($";    IsEnum:      {typeClr.IsEnum}");
                        asmWriter.WriteLine($";    IsPrimitive: {typeClr.IsPrivate}");
                        asmWriter.WriteLine( ";    Fields:");
                        asmWriter.WriteLine( ";        {0,6} {1,16} {2,20} {3,4}", "Offset", "Name", "Type", "Size");
                        var orderedFields = typeClr.Fields.ToList().OrderBy(x => x.Offset);
                        foreach (var field in orderedFields)
                        {
                            asmWriter.WriteLine($";        {field.Offset,6} {field.Name,16} {field.Type.Name,20} {field.Size,4}");
                        }
                        
                        ClrHeap heap = runtime.Heap;

                        foreach (ClrMethod method in typeClr.Methods)
                        {
                            MethodCompilationType compileType = method.CompilationType;
                            ArchitectureMode mode = clrInfo.DacInfo.TargetArchitecture == Architecture.X86
                                ? ArchitectureMode.x86_32
                                : ArchitectureMode.x86_64;

                            this.currentMethodAddress = 0;
                            var translator = new IntelTranslator
                            {
                                SymbolResolver = (Instruction instruction, long addr, ref long offset) =>
                                    ResolveSymbol(runtime, instruction, addr, ref currentMethodAddress)
                            };

                            // This not work even ClrMd says opposite...
                            //ulong startAddress = method.NativeCode;
                            //ulong endAddress = method.ILOffsetMap.Select(entry => entry.EndAddress).Max();

                            DisassembleAndWrite(method, mode, translator, ref currentMethodAddress, asmWriter);
                            this.engine.UpdateLog($"Method {method.Name} disassembled to ASM.");
                            asmWriter.WriteLine();
                        }
                    }
                    break;
                }
            }
            return asmWriter.ToString();
        }

        private void DisassembleAndWrite(ClrMethod method, ArchitectureMode architecture, Translator translator, ref ulong methodAddressRef, TextWriter writer)
        {
            writer.WriteLine(method.GetFullSignature());
            var info = FindNonEmptyHotColdInfo(method);
            if (info == null)
            {
                writer.WriteLine("    ; Unable to load method data (not JITted?)");
                return;
            }
            var methodAddress = info.HotStart;
            methodAddressRef = methodAddress;
            using (var disasm = new Disassembler(new IntPtr(unchecked((long)methodAddress)), (int)info.HotSize, architecture, methodAddress))
            {
                foreach (var instruction in disasm.Disassemble())
                {
                    writer.Write(String.Format("0x{0:X8}`{1:X8}:", (instruction.Offset >> 32) & 0xFFFFFFFF, instruction.Offset & 0xFFFFFFFF));
                    writer.Write("    L");
                    writer.Write((instruction.Offset - methodAddress).ToString("x4"));
                    writer.Write(": ");
                    writer.WriteLine(translator.Translate(instruction));
                }
            }
        }

        private HotColdRegions FindNonEmptyHotColdInfo(ClrMethod method)
        {
            // I can't really explain this, but it seems that some methods 
            // are present multiple times in the same type -- one compiled
            // and one not compiled. A bug in clrmd?
            if (method.HotColdInfo.HotSize > 0)
                return method.HotColdInfo;

            if (method.Type == null)
                return null;

            var methodSignature = method.GetFullSignature();
            foreach (var other in method.Type.Methods)
            {
                if (other.MetadataToken == method.MetadataToken && other.GetFullSignature() == methodSignature && other.HotColdInfo.HotSize > 0)
                    return other.HotColdInfo;
            }

            return null;
        }

        private string ResolveSymbol(ClrRuntime runtime, Instruction instruction, long addr, ref ulong currentMethodAddress)
        {
            var operand = instruction.Operands.Length > 0 ? instruction.Operands[0] : null;
            if (operand?.PtrOffset == 0)
            {
                var baseOffset = instruction.PC - currentMethodAddress;
                return $"L{baseOffset + operand.PtrSegment:x4}";
            }

            string signature = runtime.GetMethodByAddress(unchecked((ulong)addr))?.GetFullSignature();
            if (!string.IsNullOrWhiteSpace(signature))
                return signature;
            Symbol symbol = this.engine.ResolveNativeSymbol((ulong)addr);
            if (!string.IsNullOrWhiteSpace(symbol.MethodName))
                return symbol.ToString();
            return null;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.dllStream.Dispose();
                    this.pdbStream.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    public enum DiagnosticAssemblyMode
    {
        Release,
        Debug
    }

    public enum DiagnosticAssembyPlatform
    {
        x64,
        x86
    }
}

