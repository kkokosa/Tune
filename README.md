# Tune
The Ultimate .NET Experiment

![Example Tune screenshot](/Docs/tune_screenshot01.png)

The Ultimate .NET Experiment (Tune) as its purpose is to learn .NET internals and performance tuning by experiments with C# code. The main way of working with this tool is as follows:
* write a sample, valid C# script which contains at least one class with public method taking a single string parameter. It will be executed by hitting Run button. This script can contain as many additional methods and classes as you wish. Just remember that first public method from the first public class will be executed (with single parameter taken from the input box below the script). You may also choose whether you want to build in Debug or Release mode (note: currently it is only x64 bit compilation). There are three predefined examples under File menu.
* after clicking Run button, the script will be compiled and executed. Additionally, it will be decompiled both to IL (Intermediate Language) and assembly code in the corresponding tabs.
* all the time Tune is running (including time during script execution) a graph with GC data is being drawn. It shows information about generation sizes and GC occurrences (illustrated as vertical lines with the number below indicating which generation has been triggered).

Note: As it is currently in very early 0.2 version, it can be treated as Proof Of Concept with many, many features still missing. But it is usable enough to have some fun with it already.

## Overall architecture

Tune is built from a few very interesting pieces, which will be probably much more clearer to show on the following diagram than to describe in words:

![Tune architecture](/Docs/tune_architecture.png)

As you can see, it is using parts of [SharpDevelop](http://www.icsharpcode.net/) (ICSharpCode) and [Mono.Cecil](http://www.mono-project.com/docs/tools+libraries/libraries/Mono.Cecil/) libraries to decompile IL. To decompile into ASM it is using [ClrMd](https://github.com/Microsoft/clrmd) to find method address location in the memory and then use [SharpDisasm](https://sharpdisasm.codeplex.com/) (which is a [libudis86](http://udis86.sourceforge.net/) C library port). It additionally uses [dbghelp.dll](https://msdn.microsoft.com/en-us/library/windows/desktop/ms679309) to resolve native symbols. ETW data are being processed by the [TraceEvent](https://www.nuget.org/packages/Microsoft.Diagnostics.Tracing.TraceEvent/) library.

## Building

Requirements:
* Visual Studio 2015
* [Debugging Tools for Windows](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/) installed - look at post-build event

Additional info and examples:
* [The Ultimate .NET Experiment – open source project](http://tooslowexception.com/the-ultimate-net-experiment-project/)