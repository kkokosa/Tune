using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Tune.Core
{
    class NativeTarget : IDisposable
    {
        private IntPtr _hProcess;
        private Process _process;
        private HashSet<string> _loadedModules = new HashSet<string>();
        private List<LightProcessModule> _processModules;
        private DateTimeOffset _lastRefreshTime;

        public NativeTarget(int processID)
        {
            _process = Process.GetProcessById(processID);
            _hProcess = _process.Handle;
            // Note that symsrv.dll and an updated dbghelp.dll (from the Debugging Tools)
            // need to be around for the symbol loads to succeed. There is a post-build
            // step that copies them over to the output directory, or we could bundle them
            // with the project.
            SymSetOptions(SYMOPT_UNDNAME | SYMOPT_DEFERRED_LOADS);
            string symbolPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            if (!SymInitialize(_hProcess, symbolPath, invadeProcess: false))
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            RefreshProcessModules();
        }

        public void Dispose()
        {
            SymCleanup(_hProcess);
            _process.Dispose();
            _hProcess = IntPtr.Zero;
        }

        public string ProcessName => _process.ProcessName;

        public Symbol ResolveSymbol(ulong address)
        {
            var module = ModuleForAddress(address);
            Symbol result = new Symbol
            {
                ModuleName = module.ModuleName,
                OffsetInMethod = address - module.BaseAddress,
                Address = address
            };

            if (!String.IsNullOrEmpty(module.FileName) && !_loadedModules.Contains(module.FileName))
            {
                // TODO Symbols for 32-bit ntdll in a WoW64 process are not resolved properly
                if (0 == SymLoadModule64(_hProcess, IntPtr.Zero, module.FileName, null,
                    module.BaseAddress, module.Size))
                {
                    return result;
                }
                _loadedModules.Add(module.FileName);
            }

            SYMBOL_INFO symbol = new SYMBOL_INFO();
            symbol.SizeOfStruct = 88;
            symbol.MaxNameLen = 1024;
            ulong displacement;
            if (SymFromAddr(_hProcess, address, out displacement, ref symbol))
            {
                result.MethodName = symbol.Name;
                result.OffsetInMethod = (uint)displacement;
            }
            return result;
        }

        private LightProcessModule ModuleForAddress(ulong address)
        {
            // TODO Think of the appropriate interval for this, maybe take it from the external interval value
            if (DateTime.Now - _lastRefreshTime > TimeSpan.FromSeconds(1))
                RefreshProcessModules();

            // System.Diagnostics.Process.Modules returns only the 64-bit modules
            // if attached to a 32-bit target, so we have to use our own implementation.
            return _processModules.FirstOrDefault(
                pm => pm.BaseAddress <= address &&
                (pm.BaseAddress + pm.Size) > address);
        }

        private void RefreshProcessModules()
        {
            _processModules = ProcessModules().ToList();
            _lastRefreshTime = DateTime.Now;
        }

        private IEnumerable<LightProcessModule> ProcessModules()
        {
            IntPtr[] moduleHandles = new IntPtr[1024];
            uint sizeNeeded;
            if (!K32EnumProcessModulesEx(_hProcess, moduleHandles, (uint)(moduleHandles.Length * IntPtr.Size),
                out sizeNeeded, LIST_MODULES_ALL))
            {
                yield break;
            }
            var buffer = new StringBuilder(2048);
            foreach (var moduleHandle in moduleHandles.Take((int)(sizeNeeded / IntPtr.Size)))
            {
                string fileName = "", baseName = "";
                if (0 != K32GetModuleFileNameEx(_hProcess, moduleHandle, buffer, (uint)buffer.Capacity))
                    fileName = buffer.ToString();
                if (0 != K32GetModuleBaseName(_hProcess, moduleHandle, buffer, (uint)buffer.Capacity))
                    baseName = buffer.ToString();
                MODULEINFO moduleInfo = new MODULEINFO();
                if (K32GetModuleInformation(_hProcess, moduleHandle, out moduleInfo, (uint)Marshal.SizeOf(moduleInfo)))
                {
                    yield return new LightProcessModule
                    {
                        FileName = fileName,
                        ModuleName = baseName,
                        BaseAddress = (ulong)moduleInfo.lpBaseOfDll.ToInt64(),
                        Size = moduleInfo.SizeOfImage
                    };
                }
            }
        }

        private struct LightProcessModule
        {
            public string ModuleName { get; set; }
            public string FileName { get; set; }
            public ulong BaseAddress { get; set; }
            public uint Size { get; set; }
        }

        private struct SYMBOL_INFO
        {
            public uint SizeOfStruct;
            public uint TypeIndex;
            public ulong Reserved1;
            public ulong Reserved2;
            public uint Index;
            public uint Size;
            public ulong ModBase;
            public uint Flags;
            public ulong Value;
            public ulong Address;
            public uint Register;
            public uint Scope;
            public uint Tag;
            public uint NameLen;
            public uint MaxNameLen;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
            public string Name;
        }

        [DllImport("dbghelp.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SymInitialize(IntPtr hProcess, string userSearchPath, [MarshalAs(UnmanagedType.Bool)] bool invadeProcess);

        [DllImport("dbghelp.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SymFromAddr(IntPtr hProcess, ulong address, out ulong displacement, ref SYMBOL_INFO symbol);

        private const uint SYMOPT_UNDNAME = 0x02;
        private const uint SYMOPT_DEFERRED_LOADS = 0x00000004;

        [DllImport("dbghelp.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern uint SymSetOptions(uint options);

        [DllImport("dbghelp.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern ulong SymLoadModule64(IntPtr hProcess, IntPtr hFile, string imageName, string moduleName, ulong baseAddress, uint size);

        [DllImport("dbghelp.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SymCleanup(IntPtr hProcess);

        private const uint LIST_MODULES_32BIT = 0x01;
        private const uint LIST_MODULES_64BIT = 0x02;
        private const uint LIST_MODULES_ALL = 0x03;
        private const uint LIST_MODULES_DEFAULT = 0x0;

        private struct MODULEINFO
        {
            public IntPtr lpBaseOfDll;
            public uint SizeOfImage;
            public IntPtr EntryPoint;
        }

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool K32EnumProcessModulesEx(IntPtr hProcess, IntPtr[] moduleHandles, uint sizeOfModuleHandles, out uint sizeNeeded, uint filterFlag);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern uint K32GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder filename, uint size);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern uint K32GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder baseName, uint size);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool K32GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO moduleInfo, uint size);
    }

    public struct Symbol
    {
        public string ModuleName { get; set; }
        public string MethodName { get; set; }
        public ulong OffsetInMethod { get; set; }
        public ulong Address { get; set; }

        public static Symbol Unknown(ulong address)
        {
            return new Symbol { Address = address };
        }

        public override string ToString()
        {
            if (String.IsNullOrEmpty(MethodName) && String.IsNullOrEmpty(ModuleName))
                return $"{Address,16:X}";
            else
                return $"{ModuleName}!{MethodName}+0x{OffsetInMethod:X}";
        }
    }
}
