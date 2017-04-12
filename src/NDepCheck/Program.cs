using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gibraltar;
using JetBrains.Annotations;
using NDepCheck.Calculating;
using NDepCheck.Reading;
using NDepCheck.Rendering;
using NDepCheck.Transforming;
using NDepCheck.Transforming.ViolationChecking;
using NDepCheck.WebServing;

namespace NDepCheck {
    /// <remarks>
    ///     Main class of NDepCheck.
    ///     All static methods may run in parallel.
    /// </remarks>
    public class Program {
        private const string VERSION = "V.3.54";

        public const int OK_RESULT = 0;
        public const int OPTIONS_PROBLEM = 1;

        public const int DEPENDENCIES_NOT_OK = 3;

        public const int FILE_NOT_FOUND_RESULT = 4;
        public const int NO_RULE_GROUPS_FOUND = 5;
        public const int NO_RULE_SET_FOUND_FOR_FILE = 6;
        public const int EXCEPTION_RESULT = 7;

        private class ProgramOption : Option {
            public ProgramOption(string shortname, string name, string usage, string description, string[] moreNames = null)
                : base(shortname, name, usage, description, @default: "", moreNames: moreNames) {
                // empty
            }
        }

        public static readonly Option HelpAllOption = new ProgramOption(shortname: "?", name: "help-all", usage: "[filter]", description: "write help", moreNames: new[] { "h", "help" });
        public static readonly Option HelpDetailedHelpOption = new ProgramOption(shortname: "!", name: "help-detail", usage: "[filter]", description: "write extensive help", moreNames: new[] { "man" });
        public static readonly Option DebugOption = new ProgramOption(shortname: "debug", name: "debug", usage: "", description: "start .Net debugger");

        public static readonly Option ReadPluginOption = new ProgramOption(shortname: "rp", name: "read-plugin", usage: "assembly reader filepattern [- filepattern]", description: "Use <assembly.reader> to read files matching filepattern, but not second filepattern");
        public static readonly Option ReadFileOption = new ProgramOption(shortname: "rf", name: "read-file", usage: "reader filepattern [- filepattern]", description: "Use predefined reader to read files matching filepattern, but not second filepattern");
        public static readonly Option ReadOption = new ProgramOption(shortname: "rd", name: "read", usage: "[filepattern] [- filepattern]", description: "Use reader derived from file extension to read files matching filepattern, but not second filepattern");
        public static readonly Option ReadPluginHelpOption = new ProgramOption(shortname: "ra?", name: "read-plugin-help", usage: "assembly [filter]", description: "Show help for all readers in assembly");
        public static readonly Option ReadHelpOption = new ProgramOption(shortname: "rf?", name: "read-help", usage: "[filter]", description: "Show help for all predefined readers");
        public static readonly Option ReadPluginDetailedHelpOption = new ProgramOption(shortname: "ra!", name: "read-plugin-detail", usage: "assembly reader [filter]", description: "Show detailed help for reader in assembly");
        public static readonly Option ReadDetailedHelpOption = new ProgramOption(shortname: "rf!", name: "read-detail", usage: "reader [filter]", description: "Show detailed help for predefined reader");

        public static readonly Option ConfigurePluginOption = new ProgramOption(shortname: "cp", name: "configure-plugin", usage: "assembly transformer { options }", description: "Configure <assembly.transformer> with options");
        public static readonly Option ConfigureOption = new ProgramOption(shortname: "cf", name: "configure", usage: "reader  { options }", description: "Configure predefined transformer with options");

        public static readonly Option TransformPluginOption = new ProgramOption(shortname: "tp", name: "transform-plugin", usage: "assembly transformer [{ options }]", description: "Transform with <assembly.transformer> with options");
        public static readonly Option TransformOption = new ProgramOption(shortname: "tf", name: "transform", usage: "transformer  [{ options }]", description: "Transform with predefined transformer with options");
        public static readonly Option TransformUndo = new ProgramOption(shortname: "tu", name: "transform-undo", usage: "", description: "Undo transformation");
        public static readonly Option TransformTestDataOption = new ProgramOption(shortname: "tt", name: "transform-testdata", usage: "assembly transformer [{ options }]", description: "Transform internal testdata with <assembly.transformer> with options");
        public static readonly Option TransformPluginHelpOption = new ProgramOption(shortname: "tp?", name: "transform-plugin-help", usage: "assembly [filter]", description: "Show help for all transformers in assembly");
        public static readonly Option TransformHelpOption = new ProgramOption(shortname: "tf?", name: "transform-help", usage: "[filter]", description: "Show help for all predefined transformers");
        public static readonly Option TransformPluginDetailedHelpOption = new ProgramOption(shortname: "tp!", name: "transform-plugin-detail", usage: "assembly transformer [filter]", description: "Show detailed help for transformer in assembly");
        public static readonly Option TransformDetailedHelpOption = new ProgramOption(shortname: "tf!", name: "transform-detail", usage: "transformer [filter]", description: "Show detailed help for predefined transformer");

        public static readonly Option WritePluginOption = new ProgramOption(shortname: "wp", name: "write-plugin", usage: "assembly writer [{ options }] filename", description: "write to filename with <assembly.writer> with options");
        public static readonly Option WriteFileOption = new ProgramOption(shortname: "wr", name: "write", usage: "writer  [{ options }] filename", description: "write to filename with predefined writer with options");
        public static readonly Option WriteDipOption = new ProgramOption(shortname: "wd", name: "write-dip", usage: "filename", description: "write to filename with predefined writer with options");
        public static readonly Option WriteTestDataOption = new ProgramOption(shortname: "wt", name: "write-testdata", usage: "assembly writer [{ options }] filename", description: "write internal testdata to filename with <assembly.writer> with options");
        public static readonly Option WritePluginHelpOption = new ProgramOption(shortname: "wp?", name: "write-plugin-help", usage: "assembly [filter]", description: "Show help for all writers in assembly");
        public static readonly Option WriteHelpOption = new ProgramOption(shortname: "wr?", name: "write-help", usage: "[filter]", description: "Show help for all predefined writers");
        public static readonly Option WritePluginDetailedHelpOption = new ProgramOption(shortname: "wp!", name: "write-plugin-detail", usage: "assembly writer [filter]", description: "Show detailed help for writer in assembly");
        public static readonly Option WriteDetailedHelpOption = new ProgramOption(shortname: "wr!", name: "write-detail", usage: "writer [filter]", description: "Show detailed help for predefined writer");

        public static readonly Option CalculatePluginOption = new ProgramOption(shortname: "xp", name: "calculate-plugin", usage: "varname assembly calculator [define ...]", description: "Use <assembly.calculator> to calculate value assigned to varname");
        public static readonly Option CalculateOption = new ProgramOption(shortname: "xf", name: "calculate-file", usage: "varname calculator [define ...]", description: "Use predefined calculator to calculate value assigned to varname");
        public static readonly Option CalculatePluginHelpOption = new ProgramOption(shortname: "xa?", name: "calculate-plugin-help", usage: "assembly [filter]", description: "Show help for all calculators in assembly");
        public static readonly Option CalculateHelpOption = new ProgramOption(shortname: "xf?", name: "calculate-help", usage: "[filter]", description: "Show help for all predefined calculators");
        public static readonly Option CalculatePluginDetailedHelpOption = new ProgramOption(shortname: "xa!", name: "calculate-plugin-detail", usage: "assembly calculator [filter]", description: "Show detailed help for calculator in assembly");
        public static readonly Option CalculateDetailedHelpOption = new ProgramOption(shortname: "xf!", name: "calculate-detail", usage: "calculator [filter]", description: "Show detailed help for predefined calculator");

        public static readonly Option DoBreakOption = new ProgramOption(shortname: "db", name: "do-break", usage: "", description: "stop execution; useful for debugging of -df");
        public static readonly Option DoCommandOption = new ProgramOption(shortname: "dc", name: "do-command", usage: "command", description: "execute shell command; useful for opening result file");
        public static readonly Option DoScriptOption = new ProgramOption(shortname: "ds", name: "do-script", usage: "filename", description: "execute NDepCheck script");
        public static readonly Option DoScriptLoggedOption = new ProgramOption(shortname: "dl", name: "do-script-logged", usage: "filename", description: "execute NDepCheck script with log output");
        public static readonly Option DoDefineOption = new ProgramOption(shortname: "dd", name: "do-define", usage: "name value", description: "define name as value");
        public static readonly Option DoResetOption = new ProgramOption(shortname: "dr", name: "do-reset", usage: "[filename]", description: "reset state; and read file as dip file");

        public static readonly Option WatchFilesOption = new ProgramOption(shortname: "aw", name: "watch-files", usage: "[filepattern [- filepattern]] script", description: "Watch files");
        public static readonly Option UnwatchFilesOption = new ProgramOption(shortname: "au", name: "unwatch-files", usage: "filepattern", description: "Unwatch files specified by filepattern");
        public static readonly Option UnwatchTriggersOption = new ProgramOption(shortname: "an", name: "unwatch-triggers", usage: "script", description: "No longer watch all files triggering script");

        public static readonly Option HttpRunOption = new ProgramOption(shortname: "hr", name: "http-run", usage: "port directory", description: "run internal webserver");
        public static readonly Option HttpStopOption = new ProgramOption(shortname: "hs", name: "http-stop", usage: "", description: "stop internal webserver");

        public static readonly Option IgnoreCaseOption = new ProgramOption(shortname: "ic", name: "ignore-case", usage: "", description: "ignore case at multiple places");

        public static readonly Option InteractiveOption = new ProgramOption(shortname: "ia", name: "interactive", usage: "[filename]", description: "interactive mode, logging to filename");
        public static readonly Option InteractiveStopOption = new ProgramOption(shortname: "is", name: "interactive-stop", usage: "", description: "stop interactive mode", moreNames: new[] { "q", "quit", "exit" });
        public static readonly Option InteractiveWriteOption = new ProgramOption(shortname: "iw", name: "interactive-write", usage: "# [pattern]", description: "write about # dependencies matching pattern from all sources");
        public static readonly Option InteractiveDependencyMatchOption = new ProgramOption(shortname: "id", name: "interactive-match", usage: "[pattern]", description: "Show number of dependencies matching pattern from all sources");
        public static readonly Option InteractiveItemMatchOption = new ProgramOption(shortname: "ii", name: "interactive-match", usage: "[pattern]", description: "Show number of items matching pattern from all sources");

        public static readonly Option CurrentDirectoryOption = new ProgramOption(shortname: "cd", name: "current-directory", usage: "[directory]", description: "show or change current directory");
        public static readonly Option GarbageCollectionOption = new ProgramOption(shortname: "gc", name: "garbage-collect", usage: "", description: "run garbage collection");
        public static readonly Option LogVerboseOption = new ProgramOption(shortname: "lv", name: "log-verbose", usage: "", description: "verbose logging");
        public static readonly Option LogChattyOption = new ProgramOption(shortname: "lc", name: "log-chatty", usage: "", description: "chatty logging");
        public static readonly Option LazyOption = new ProgramOption(shortname: "lz", name: "lazy", usage: "", description: "execute readers and transformers lazily (NOT YET IMPLEMENTED FULLY)");

        private static readonly Option[] _allOptions = {
            HelpAllOption, HelpDetailedHelpOption, DebugOption,
            ReadPluginOption, ReadOption, ReadFileOption, ReadPluginHelpOption, ReadHelpOption, ReadPluginDetailedHelpOption, ReadDetailedHelpOption,
            ConfigurePluginOption, ConfigureOption, TransformPluginOption, TransformOption, TransformUndo,
            TransformTestDataOption, TransformPluginHelpOption, TransformHelpOption, TransformPluginDetailedHelpOption, TransformDetailedHelpOption,
            WritePluginOption, WriteFileOption, WriteDipOption, WriteTestDataOption, WritePluginHelpOption, WriteHelpOption, WritePluginDetailedHelpOption, WriteDetailedHelpOption,
            CalculatePluginOption, CalculateOption, CalculatePluginHelpOption, CalculateHelpOption, CalculatePluginDetailedHelpOption, CalculateDetailedHelpOption,
            DoBreakOption, DoCommandOption, DoScriptOption, DoScriptLoggedOption, DoDefineOption, DoResetOption,
            WatchFilesOption, UnwatchFilesOption, UnwatchTriggersOption,
            HttpRunOption, HttpStopOption,
            IgnoreCaseOption,
            InteractiveOption, InteractiveStopOption,InteractiveWriteOption, InteractiveDependencyMatchOption, InteractiveItemMatchOption,
            CurrentDirectoryOption, GarbageCollectionOption, LogVerboseOption, LogChattyOption, LazyOption,
        };

        private readonly List<FileWatcher> _fileWatchers = new List<FileWatcher>();

        private WebServer _webServer;

        private string _interactiveLogFile { get; set; }

        /// <summary>
        ///     The static Main method.
        /// </summary>
        public static int Main(string[] args) {
            ItemType.New("DUMMY(DUMMY)");

            Log.Logger = new ConsoleLogger();

            var program = new Program();
            try {
                // TODO: In my first impl, I used a separate GlobalContext() for each entry - why? This defies explorative working!
                var globalContext = new GlobalContext();
                int lastResult = program.Run(args, globalContext, writtenMasterFiles: null, logCommands: false);
                while (program._webServer != null || program._interactiveLogFile != null || program._fileWatchers.Any()) {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(value: "Type /?<enter> for help; or q<enter> for stopping NDepCheck.");
                    Console.Write(value: globalContext.Name + " NDepCheck> ");
                    Console.ResetColor();
                    string commands = Console.ReadLine();
                    if (commands == null
                         || commands.Trim().ToLowerInvariant().StartsWith(value: "q")
                         || commands.Trim().ToLowerInvariant() == "exit") {
                        break;
                    } else {
                        commands = commands.Trim();
                        if (commands != "") {
                            InteractiveLog(program, commands);
                            var writtenMasterFiles = new List<string>();

                            program.Run(args: commands.Split(' ').Select(s => s.Trim()).Where(s => s != "").ToArray(),
                                globalContext: globalContext, writtenMasterFiles: writtenMasterFiles, logCommands: false);

                            program.WriteWrittenMasterFiles(writtenMasterFiles);
                        }
                    }
                }
                return lastResult;
            } catch (FileNotFoundException ex) {
                Log.WriteWarning(ex.Message);
                return FILE_NOT_FOUND_RESULT;
            } catch (Exception ex) {
                Log.WriteError(msg: "Exception occurred: " + ex.Message + " (" + ex.GetType().FullName + ")");
                if (Log.IsChattyEnabled) Console.WriteLine(ex);
                return EXCEPTION_RESULT;
            } finally {
                // Main may be called multiple times; therefore we clear all caches
                Intern.ResetAll();
            }
        }

        private static void InteractiveLog(Program program, string commands) {
            if (!string.IsNullOrWhiteSpace(program._interactiveLogFile)) {
                using (var sw = File.AppendText(program._interactiveLogFile)) {
                    sw.WriteLine(value: $"// {DateTime.Now:G}");
                    sw.WriteLine(commands);
                }
            }
        }

        internal void WriteWrittenMasterFiles(List<string> writtenMasterFiles) {
            if (writtenMasterFiles.Any()) {
                Console.WriteLine(value: "Written master files:");
                writtenMasterFiles.Sort();
                foreach (var f in writtenMasterFiles) {
                    Console.WriteLine(value: "  " + f);
                }
            }
        }

        public int Run(string[] args, GlobalContext globalContext, [CanBeNull] List<string> writtenMasterFiles, bool logCommands) {
            Log.SetLevel(Log.Level.Standard);

            if (args.Length == 0) {
                return UsageAndExit(message: "No options or files specified", globalContext: globalContext);
            }

            bool ranAsWebServer = false;
            int result = OK_RESULT;

            try {
                for (int i = 0; i < args.Length; i++) {
                    string arg = args[i];
                    if (logCommands) {
                        Log.WriteInfo($">>>> Starting {arg}");
                    }

                    if (arg == "help") {
                        Log.WriteWarning("For help, use -? or -help");
                    } else if (HelpAllOption.Matches(arg)) {
                        // -? [filter]
                        string filter = Option.ExtractOptionValue(args, ref i, allowOptionValue: true);
                        return UsageAndExit(message: null, globalContext: globalContext, withIntro: _interactiveLogFile == null,
                                            detailed: filter != null, filter: (filter ?? "").TrimStart('-', '/'));
                    } else if (HelpDetailedHelpOption.Matches(arg)) {
                        // -! [filter]
                        string filter = Option.ExtractOptionValue(args, ref i, allowOptionValue: true);
                        return UsageAndExit(message: null, globalContext: globalContext, withIntro: true,
                                            detailed: true, filter: (filter ?? "").TrimStart('-', '/'));
                    } else if (arg == "-debug" || arg == "/debug") {
                        // -debug
                        Debugger.Launch();
                    } else if (ReadPluginOption.Matches(arg)) {
                        // -rp    assembly reader filepattern [- filepattern]
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string reader = Option.ExtractNextValue(args, ref i);
                        string filePattern = Option.ExtractNextValue(args, ref i);
                        globalContext.CreateInputOption(args, ref i, filePattern, assembly, reader);
                    } else if (ReadFileOption.Matches(arg)) {
                        // -rf    reader filepattern [- filepattern]
                        string reader = Option.ExtractOptionValue(args, ref i);
                        string filePattern = Option.ExtractNextValue(args, ref i);
                        globalContext.CreateInputOption(args, ref i, filePattern, "", reader);
                    } else if (ReadOption.Matches(arg)) {
                        // -rd    filepattern [- filepattern]
                        // -rd    - filepattern
                        string s = Option.ExtractOptionValue(args, ref i);
                        if (s == "-") {
                            string filePattern = Option.ExtractRequiredOptionValue(args, ref i, "File pattern missing after -");
                            globalContext.AddNegativeInputOption(filePattern);
                        } else {
                            string filePattern = s;
                            globalContext.CreateInputOption(args, ref i, filePattern, "",
                                IsDllOrExeFile(filePattern) ? typeof(DotNetAssemblyDependencyReaderFactory).FullName :
                                IsDipFile(filePattern) ? typeof(DipReaderFactory).FullName : null);
                        }

                    } else if (ReadPluginHelpOption.Matches(arg)) {
                        // -ra?    assembly [filter]
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<IReaderFactory>(assembly, filter);
                    } else if (ReadHelpOption.Matches(arg)) {
                        // -rf? [filter]
                        string filter = Option.ExtractOptionValue(args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<IReaderFactory>("", filter);
                    } else if (ReadPluginDetailedHelpOption.Matches(arg)) {
                        // -ra!    assembly reader
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string reader = Option.ExtractNextValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowDetailedHelp<IReaderFactory>(assembly, reader, filter);
                    } else if (ReadDetailedHelpOption.Matches(arg)) {
                        // -rf!    reader
                        string reader = Option.ExtractOptionValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowDetailedHelp<IReaderFactory>("", reader, filter);
                    } else if (ConfigurePluginOption.Matches(arg)) {
                        // -cp    assembly transformer { options }
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string transformer = Option.ExtractNextValue(args, ref i);
                        string transformerOptions = Option.ExtractNextValue(args, ref i);
                        globalContext.ConfigureTransformer(assembly, transformer, transformerOptions, 
                                                           forceReloadConfiguration: _interactiveLogFile != null);
                    } else if (ConfigureOption.Matches(arg)) {
                        // -cf    transformer  { options }
                        string transformer = Option.ExtractOptionValue(args, ref i);
                        string transformerOptions = Option.ExtractNextValue(args, ref i);
                        globalContext.ConfigureTransformer("", transformer, transformerOptions,
                                                           forceReloadConfiguration: _interactiveLogFile != null);
                    } else if (TransformPluginOption.Matches(arg)) {
                        // -tp    assembly transformer [{ options }]
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string transformer = Option.ExtractNextValue(args, ref i);
                        string transformerOptions = Option.ExtractNextValue(args, ref i);
                        result = globalContext.Transform(assembly, transformer, transformerOptions);
                    } else if (TransformOption.Matches(arg)) {
                        // -tf    transformer  [{ options }]
                        string transformer = Option.ExtractOptionValue(args, ref i);
                        string transformerOptions = Option.ExtractNextValue(args, ref i);
                        result = globalContext.Transform("", transformer, transformerOptions);
                    } else if (TransformUndo.Matches(arg)) {
                        // -tu
                        globalContext.UndoTransform();
                    } else if (TransformTestDataOption.Matches(arg)) {
                        // -tt    assembly transformer [{ options }]
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string transformer = Option.ExtractNextValue(args, ref i);
                        string transformerOptions = Option.ExtractNextValue(args, ref i);
                        globalContext.TransformTestData(assembly, transformer, transformerOptions);
                        globalContext.InputFilesOrTestDataSpecified = true;
                    } else if (TransformPluginHelpOption.Matches(arg)) {
                        // -tp?    assembly
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<ITransformer>(assembly, filter);
                    } else if (TransformHelpOption.Matches(arg)) {
                        // -tf?
                        string filter = Option.ExtractOptionValue(args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<ITransformer>("", filter);
                    } else if (TransformPluginDetailedHelpOption.Matches(arg)) {
                        // -ta!    assembly transformer
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string transformer = Option.ExtractNextValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowDetailedHelp<ITransformer>(assembly, transformer, filter);
                    } else if (TransformDetailedHelpOption.Matches(arg)) {
                        // -tf!    transformer
                        string transformer = Option.ExtractOptionValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowDetailedHelp<ITransformer>("", transformer, filter);
                    } else if (WritePluginOption.Matches(arg)) {
                        // -wp    assembly writer [{ options }] filename
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string writer = Option.ExtractNextValue(args, ref i);
                        string s = Option.ExtractNextValue(args, ref i);
                        string masterFileName = Write(s, args, ref i,
                            (writerOptions, fileName) => globalContext.RenderToFile(assembly, writer, writerOptions, fileName));
                        writtenMasterFiles?.Add(masterFileName);
                    } else if (WriteFileOption.Matches(arg)) {
                        // -wr    writer  [{ options }] filename
                        string writer = Option.ExtractOptionValue(args, ref i);
                        string s = Option.ExtractNextValue(args, ref i);
                        string masterFileName = Write(s, args, ref i,
                            (writerOptions, fileName) => globalContext.RenderToFile("", writer, writerOptions, fileName));
                        writtenMasterFiles?.Add(masterFileName);
                    } else if (WriteDipOption.Matches(arg)) {
                        // -wd    filename
                        string fileName = Option.ExtractOptionValue(args, ref i);
                        string masterFileName = globalContext.RenderToFile("", typeof(DipWriter).Name, "", fileName);
                        writtenMasterFiles?.Add(masterFileName);
                    } else if (WriteTestDataOption.Matches(arg)) {
                        // -wt    assembly writer [{ options }] filename
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string writer = Option.ExtractNextValue(args, ref i);
                        string s = Option.ExtractNextValue(args, ref i);
                        Write(s, args, ref i,
                            (writerOptions, fileName) => globalContext.RenderTestData(assembly, writer, writerOptions, fileName));
                    } else if (WritePluginHelpOption.Matches(arg)) {
                        // -wp?    assembly
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<IDependencyRenderer>(assembly, filter);
                    } else if (WriteHelpOption.Matches(arg)) {
                        // -wr?
                        string filter = Option.ExtractOptionValue(args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<IDependencyRenderer>("", filter);
                    } else if (WritePluginDetailedHelpOption.Matches(arg)) {
                        // -wp!    assembly reader
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string writer = Option.ExtractNextValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowDetailedHelp<IDependencyRenderer>(assembly, writer, filter);
                    } else if (WriteDetailedHelpOption.Matches(arg)) {
                        // -wr!    reader
                        string writer = Option.ExtractOptionValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowDetailedHelp<IDependencyRenderer>("", writer, filter);
                    } else if (CalculatePluginOption.Matches(arg)) {
                        // -xp    varname assembly calculator [varname ...]
                        string varname = Option.ExtractOptionValue(args, ref i);
                        string assembly = Option.ExtractNextValue(args, ref i);
                        string calculator = Option.ExtractNextValue(args, ref i);
                        List<string> input = ExtractInputVars(args, ref i);
                        globalContext.Calculate(varname, assembly, calculator, input);
                    } else if (CalculateOption.Matches(arg)) {
                        // -xf    varname calculator [varname ...]
                        string varname = Option.ExtractOptionValue(args, ref i);
                        string calculator = Option.ExtractNextValue(args, ref i);
                        List<string> input = ExtractInputVars(args, ref i);
                        globalContext.Calculate(varname, "", calculator, input);
                    } else if (CalculatePluginHelpOption.Matches(arg)) {
                        // -xa?    assembly [filter]
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<ICalculator>(assembly, filter);
                    } else if (CalculateHelpOption.Matches(arg)) {
                        // -xf? [filter]
                        string filter = Option.ExtractOptionValue(args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<ICalculator>("", filter);
                    } else if (CalculatePluginDetailedHelpOption.Matches(arg)) {
                        // -xa!    assembly calculator
                        string assembly = Option.ExtractOptionValue(args, ref i);
                        string calculator = Option.ExtractNextValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowDetailedHelp<IDependencyRenderer>(assembly, calculator, filter);
                    } else if (CalculateDetailedHelpOption.Matches(arg)) {
                        // -xf!    calculator
                        string calculator = Option.ExtractOptionValue(args, ref i);
                        string filter = Option.ExtractNextValue(args, ref i);
                        globalContext.ShowDetailedHelp<ICalculator>("", calculator, filter);
                    } else if (DoBreakOption.Matches(arg)) {
                        // -db
                        Log.WriteInfo(msg: "---- Stop reading options (-b)");
                        goto DONE;
                    } else if (DoCommandOption.Matches(arg)) {
                        // -dc    command
                        string cmd = Option.ExtractOptionValue(args, ref i);
                        try {
                            if (new Process { StartInfo = new ProcessStartInfo(cmd) }.Start()) {
                                Log.WriteInfo(msg: $"Started process '{cmd}'");
                            } else {
                                Log.WriteError(msg: $"Could not start process '{cmd}'");
                            }
                        } catch (Exception ex) {
                            Log.WriteError(msg: $"Could not start process '{cmd}'; reason: {ex.Message}");
                            result = EXCEPTION_RESULT;
                        }
                    } else if (DoScriptOption.Matches(arg) || DoScriptLoggedOption.Matches(arg)) {
                        // -ds    filename
                        string fileName = Option.ExtractOptionValue(args, ref i);
                        result = RunFrom(fileName, globalContext, writtenMasterFiles, logCommands: DoScriptLoggedOption.Matches(arg));
                        // file is also an input file - and if there are no input files in -o, the error will come up there.
                        globalContext.InputFilesOrTestDataSpecified = true;
                    } else if (DoDefineOption.Matches(arg)) {
                        // -dd    name value
                        string varname = Option.ExtractOptionValue(args, ref i);
                        if (varname == null) {
                            globalContext.ShowAllVars();
                        } else {
                            string varvalue = Option.ExtractNextValue(args, ref i);
                            globalContext.SetDefine(varname, varvalue, location: "after -dd option");

                            globalContext.GlobalVars[varname] = varvalue;
                        }
                    } else if (DoResetOption.Matches(arg)) {
                        // -dr    [filename]

                        globalContext.ResetAll();

                        string fileName = Option.ExtractNextValue(args, ref i);
                        if (fileName != null && IsDipFile(fileName)) {
                            globalContext.CreateInputOption(args, ref i, arg, assembly: "",
                                readerFactoryClass: typeof(DipReaderFactory).FullName);
                        }
                    } else if (WatchFilesOption.Matches(arg)) {
                        // -aw    [filepattern [- filepattern]] script
                        string positive = Option.ExtractOptionValue(args, ref i);
                        string s = Option.ExtractNextValue(args, ref i);
                        string negative, scriptName;
                        if (s == "-") {
                            negative = Option.ExtractNextValue(args, ref i);
                            scriptName = Option.ExtractNextValue(args, ref i);
                        } else {
                            negative = null;
                            scriptName = s ?? positive;
                        }
                        AddFileWatchers(positive, negative, scriptName);
                    } else if (UnwatchFilesOption.Matches(arg)) {
                        // -au    filepattern
                        string filePattern = Option.ExtractOptionValue(args, ref i);
                        RemoveFileWatchers(filePattern);
                    } else if (UnwatchTriggersOption.Matches(arg)) {
                        // -an    script
                        string scriptName = Option.ExtractOptionValue(args, ref i);
                        RemoveFileWatchersOn(scriptName);
                    } else if (HttpRunOption.Matches(arg)) {
                        // -hr    port directory
                        string port = Option.ExtractOptionValue(args, ref i);
                        string fileDirectory = Option.ExtractNextValue(args, ref i);
                        StartWebServer(program: this, port: port, fileDirectory: fileDirectory);
                        ranAsWebServer = true;
                    } else if (HttpRunOption.Matches(arg)) {
                        // -hs
                        StopWebServer();
                    } else if (IgnoreCaseOption.Matches(arg)) {
                        // -ic
                        globalContext.IgnoreCase = true;
                    } else if (InteractiveOption.Matches(arg)) {
                        // -ia    [filename]
                        string filename = Option.ExtractOptionValue(args, ref i);
                        _interactiveLogFile = filename == null ? "" : Path.GetFullPath(filename);
                        if (_interactiveLogFile != "") {
                            Log.WriteInfo("Logging interactive input to " + _interactiveLogFile);
                        }
                        InteractiveLog(this, "// Opened interactive log " + _interactiveLogFile);
                    } else if (InteractiveStopOption.Matches(arg)) {
                        // -is
                        _interactiveLogFile = null;
                    } else if (InteractiveWriteOption.Matches(arg)) {
                        // -iw # [pattern]
                        int maxCount = Option.ExtractIntOptionValue(args, ref i, "Not a valid number");
                        string pattern = Option.ExtractNextValue(args, ref i);
                        globalContext.LogAboutNDependencies(maxCount, pattern);
                    } else if (InteractiveDependencyMatchOption.Matches(arg)) {
                        // -id [pattern]
                        string pattern = Option.ExtractOptionValue(args, ref i);
                        globalContext.LogDependencyCount(pattern);
                    } else if (InteractiveItemMatchOption.Matches(arg)) {
                        // -ii [pattern]
                        string pattern = Option.ExtractOptionValue(args, ref i);
                        globalContext.LogItemCount(pattern);
                    } else if (CurrentDirectoryOption.Matches(arg)) {
                        // -cd    [directory]
                        string directory = Option.ExtractOptionValue(args, ref i);
                        if (directory == null) {
                            Log.WriteInfo(Environment.CurrentDirectory);
                        } else {
                            if (!Directory.Exists(directory)) {
                                Log.WriteError($"'{directory}' does not exist");
                            } else {
                                Environment.CurrentDirectory = directory;
                                Log.WriteInfo(Path.GetFullPath(Environment.CurrentDirectory));
                            }
                        }
                    } else if (GarbageCollectionOption.Matches(arg)) {
                        GC.Collect(2);
                        Log.WriteInfo($"Process has {Environment.WorkingSet/1024/1024} MB allocated, " +
                                      $"{GC.GetTotalMemory(true)/1024/1024} MB managed memory.");
                    } else if (LogVerboseOption.Matches(arg)) {
                        // -lv
                        Log.SetLevel(Log.Level.Verbose);
                        WriteVersion();
                    } else if (LogChattyOption.Matches(arg)) {
                        // -lc
                        Log.SetLevel(Log.Level.Chatty);
                        WriteVersion();
                    } else if (LazyOption.Matches(arg)) {
                        // -lz
                        //                         (lazy reading and transforming NOT YET IMPLEMENTED)
                        globalContext.WorkLazily = true;
                    } else if (IsDllOrExeFile(arg)) {
                        globalContext.CreateInputOption(args, ref i, arg, assembly: "",
                            readerFactoryClass: typeof(DotNetAssemblyDependencyReaderFactory).FullName);
                    } else if (IsDipFile(arg)) {
                        globalContext.CreateInputOption(args, ref i, arg, assembly: "",
                            readerFactoryClass: typeof(DipReaderFactory).FullName);
                    } else {
                        return UsageAndExit(message: "Unsupported option '" + arg + "'", globalContext: globalContext);
                    }

                    if (logCommands) {
                        Log.WriteInfo($">>>> Finished {arg}");
                    }
                }
            } catch (ArgumentException ex) {
                return UsageAndExit(ex.Message, globalContext);
            }

            if (!_fileWatchers.Any() && _interactiveLogFile == null) {
                if (!globalContext.InputFilesOrTestDataSpecified && !ranAsWebServer && !globalContext.HelpShown) {
                    return UsageAndExit(message: "No input files specified", globalContext: globalContext);
                }

                if (result == OK_RESULT && !globalContext.TransformingDone && !globalContext.RenderingDone) {
                    // Default action at end if nothing was done
                    globalContext.ReadAllNotYetReadIn();
                    result = globalContext.Transform(assembly: "", transformerClass: typeof(CheckDeps).FullName,
                        transformerOptions: "");
                    globalContext.RenderToFile(assemblyName: "",
                        rendererClassName: typeof(RuleViolationRenderer).FullName, rendererOptions: "", fileName: null);
                }
            }

            DONE:

            if (Log.IsVerboseEnabled) {
                Log.WriteInfo(msg: "Completed with exitcode " + result);
            }

            return result;
        }

        private static List<string> ExtractInputVars(string[] args, ref int i) {
            var input = new List<string>();
            for (var s = Option.ExtractNextValue(args, ref i); s != null; s = Option.ExtractNextValue(args, ref i)) {
                input.Add(s);
            }
            return input;
        }

        private static string Write(string s, string[] args, ref int i, Func<string, string, string> action) {
            string writerOptions, fileName;
            if (s.StartsWith("{")) {
                writerOptions = s;
                fileName = Option.ExtractNextValue(args, ref i);
            } else {
                writerOptions = "";
                fileName = s;
            }
            return action(writerOptions, fileName);
        }

        private static bool IsDipFile(string arg) {
            return arg.EndsWith(value: ".dip", comparisonType: StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool IsDllOrExeFile(string arg) {
            return arg.EndsWith(value: ".dll", comparisonType: StringComparison.InvariantCultureIgnoreCase) ||
                   arg.EndsWith(value: ".exe", comparisonType: StringComparison.InvariantCultureIgnoreCase);
        }

        internal int RunFrom([NotNull] string fileName, [NotNull] GlobalContext state, [CanBeNull] List<string> writtenMasterFiles, bool logCommands) {
            int lineNo = 0;
            try {
                var args = new List<string>();
                bool inBraces = false;
                using (var sr = new StreamReader(fileName)) {
                    for (;;) {
                        lineNo++;
                        string line = sr.ReadLine();
                        if (line == null) {
                            break;
                        }
                        string trimmedLine = Regex.Replace(line, pattern: "//.*$", replacement: "").Trim();
                        string[] splitLine = trimmedLine.Split(' ', '\t').Select(s => s.Trim()).Where(s => s != "").ToArray();

                        if (splitLine.Any(s => s == "{")) {
                            args.AddRange(splitLine);
                            // If there is a } after the {, we are NOT in inBraces mode.
                            inBraces = !(trimmedLine.IndexOf("}", StringComparison.InvariantCulture) > trimmedLine.IndexOf("{", StringComparison.InvariantCulture));
                        } else if (splitLine.Any(s => s == "}")) {
                            inBraces = false;
                            args.AddRange(collection: splitLine.Select(state.ExpandDefines));
                        } else if (!inBraces) {
                            args.AddRange(collection: splitLine.Select(state.ExpandDefines));
                        } else {
                            args.Add(line);
                        }
                    }
                }

                var locallyWrittenFiles = new List<string>();
                string previousCurrentDirectory = Environment.CurrentDirectory;
                try {
                    Environment.CurrentDirectory = Path.GetDirectoryName(path: Path.GetFullPath(fileName)) ?? "";
                    return Run(args: args.ToArray(), globalContext: state, writtenMasterFiles: locallyWrittenFiles, logCommands: logCommands);
                } finally {
                    writtenMasterFiles?.AddRange(collection: locallyWrittenFiles.Select(Path.GetFullPath));
                    Environment.CurrentDirectory = previousCurrentDirectory;
                }
            } catch (Exception ex) {
                Log.WriteError(msg: $"Cannot run commands in {fileName}; reason: {ex.GetType().Name}: {ex.Message}",
                    nestedFilenames: fileName, lineNo: lineNo);
                return EXCEPTION_RESULT;
            }
        }

        private int UsageAndExit([CanBeNull] string message, GlobalContext globalContext, 
                                 int exitValue = OPTIONS_PROBLEM, bool withIntro = true, 
                                 bool detailed = false, [NotNull] string filter = "") {

            if (filter.StartsWith("file")) {
                Console.WriteLine("*** THIS SHOULD BE A HELP TEXT ABOUT NDepCheck input files (+, //, defines)");
                return exitValue;
            } else if (filter.StartsWith("item")) {
                Console.WriteLine("*** THIS SHOULD BE A HELP TEXT ABOUT NDepCheck item patterns");
                return exitValue;
            } else if (filter.StartsWith("dep")) {
                Console.WriteLine("*** THIS SHOULD BE A HELP TEXT ABOUT NDepCheck dependency patterns");
                return exitValue;
            } else if (filter.StartsWith("marker")) {
                Console.WriteLine(ObjectWithMarkers.HELP);
                return exitValue;
            } else if (filter.StartsWith("type")) {
                Console.WriteLine("*** THIS SHOULD BE A HELP TEXT ABOUT NDepCheck item types");
                return exitValue;
            } else {
                var sb = new StringBuilder();
                if (withIntro) {
                    WriteVersion();

                    sb.AppendLine(value: @"
Usage:
   NDepCheck <option>...

Typical uses:
   ___TBD___

All messages of NDepCheck are written to Console.Out.

Option overview:
    Option can be written with leading - or /
");
                }
                sb.AppendLine(Option.CreateHelp(_allOptions, detailed: detailed, filter: filter));

                string help = sb.ToString();
                if (string.IsNullOrWhiteSpace(help)) {
                    globalContext.ShowAllPluginsAndTheirHelp<IReaderFactory>("", filter);
                    globalContext.ShowAllPluginsAndTheirHelp<ITransformer>("", filter);
                    globalContext.ShowAllPluginsAndTheirHelp<ICalculator>("", filter);
                    globalContext.ShowAllPluginsAndTheirHelp<IDependencyRenderer>("", filter);
                } else {
                    Console.WriteLine(help);
                }

                if (message != null) {
                    Log.WriteError(message);
                }

                //            if (detailed) {
                //                Console.Out.WriteLine(value: @"

                //############# NOT YET UPDATED ##################

                //   /_=<directory>    For each assembly file A.dll, look for corresponding 
                //         rule file A.dll.dep in this directory (multiple /d options are 
                //         supported). This is especially useful with + lines.

                //   /d=<directory>    Like /_, but also look in all subdirectories. Mixing
                //         /_ and /_ options is supported.

                //   /f=<rule file>    Use this rule file if no matching rule file is found
                //         via /_ and /d.  This is espeically useful if no /s and /d options
                //         are specified. __________________-

                //   /i[=<N>]        For each illegal edge (i.e., edge not allowed by 
                //         the dependency file), show an example of a concrete illegal 
                //         dependency in the DOT graph. N is the maximum width of strings 
                //         used; the default is 80. Graphs can become quite cluttered 
                //         with this option.

                //   /m[=N]   Specifies the maximum number of concurrent threads to use. 
                //         If you don't include this switch, the default value is 1. If
                //         you include this switch without specifying a value, NDepCheck
                //         will use up to the number of processors in the computer.

                //############# UPDATED ##################

                //    /v    Verbose. Shows regular expressions used for checking and 
                //         all checked dependencies. Attention: Place /v BEFORE any
                //         /d, /s, or /x option to see the regular expressions.
                //         Produces lots of output.

                //   /y    Even more debugging output.

                //   /debug   Start with debugger.

                //Assemblyspecs - one of the following:

                //    simplefileName      the assembly is checked.
                //                        e.g. ProjectDir\bin\MyProject.Main.dll

                //    filepattern         all matching assemblies are checked.
                //                        e.g. bin\MyProject.*.dll 

                //    directory           all .DLL and .EXE files in the directory are checked.
                //                        e.g. MyProject\bin\Debug

                //    @fileName           lines are read as assembly fileNames and checked.
                //                        The file may contain empty lines, which are ignored.
                //                        e.g. @MyListOfFiles.txt

                //    <one of the above> /e <one of the above>            
                //                        The files after the /e are excluded from checking.
                //                        e.g. MyProject.*.dll /e *.vshost.*

                //Dependecies:_

                //A dependency describes that some 'using item' uses another 'used item'.

                //Standard .Net dependencies:

                //    A standard dependency as read from a .Net assembly has the following
                //    format:

                //namespace:class:assembly_name;assembly_version;assembly_culture:member_name;member_sort

                //    where member_sort is usually empty; but for properties, it is either
                //    'get' or 'set' on the using side.

                //Rules files:
                //    Rule files contain rule definition commands. Here is a simple example

                //        $ DOTNETCALL   ---> DOTNETCALL 

                //        // Each assembly can use .Net
                //        ::**           --->  ::mscorlib
                //        ::**           --->  ::(System|Microsoft).**

                //        // Each assembly can use everything in itself (a coarse architecture)
                //        ::(Module*)**  --->  ::\1

                //        // Module2 can use Module1
                //        ::Module2**    --->  ::Module1**

                //        // Test assemblies can use anything
                //        ::*Test*.dll   --->  ::**


                //    The following commands are supported in rule files:

                //           empty line            ... ignored
                //           // comment            ... ignored
                //           # comment             ... ignored

                //           + filepath            ... include rules from that file. The path
                //                                     is interpreted relative to the current
                //                                     rule file.

                //           NAME := pattern       ... define abbreviation which is replaced
                //                                     in patterns before processing. NAME
                //                                     must be uppercase only (but it can
                //                                     contain digits, underscores etc.).
                //                                     Longer names are preferred to shorter
                //                                     ones during replacement. The pattern
                //                                     on the right side can in turn use 
                //                                     abbreviations. Abbreviation processing
                //                                     is done before all reg.exp. replacements
                //                                     described below.
                //                                     If an abbreviation definition for the 
                //                                     same name is encountered twice, it must
                //                                     define exactly the same value.

                //           pattern ---> pattern  ... allowed dependency. The second
                //                                     pattern may contain back-references
                //                                     of the form \1, \2 etc. that are
                //                                     matched against corresponding (...)
                //                                     groups in the first pattern.

                //           pattern ---! pattern  ... forbidden dependency. This can be used
                //                                     to exclude certain possibilities for
                //                                     specific cases instead of writing many
                //                                     ""allowed"" rules.

                //           pattern ---? pattern  ... questionable dependency. If a dependency
                //                                     matches such a rule, a warning will be
                //                                     emitted. This is useful for rules that
                //                                     should be removed, but have to remain
                //                                     in place for pragmatic reasons (only
                //                                     for some time, it is hoped).

                //           pattern {             ... aspect rule set. All dependencies whose
                //               --->,                 left side matches the pattern must
                //               ---?, and             additionally match one of the rules.
                //               ---! rules            This is very useful for defining
                //           }                         partial rule sets that are orthogonal to
                //                                     the global rules (which must describe
                //                                     all dependencies in the checked
                //                                     assemblies).

                //           NAME :=
                //               <arbitrary lines except =:>
                //           =:                    ... definition of a rule macro. The
                //                                     arbitrary lines can contain the strings
                //                                     \L and \R, which are replaced with the
                //                                     corresponding patterns from the macro 
                //                                     use. NAME need not consist of letters
                //                                     only; also names like ===>, :::>, +++>
                //                                     etc. are allowed and quite useful.
                //                                     However, names must not be ""too
                //                                     similar"": If repeated characters are
                //                                     are replaced with a single one, they must
                //                                     still be different; hence, ===> and ====>
                //                                     are ""too similar"" and lead to an error.
                //                                     As with abbreviations, if a macro 
                //                                     definition for the same name is 
                //                                     encountered twice, it must define 
                //                                     exactly the same value.

                //           pattern NAME pattern  ... Use of a defined macro.

                //           % pattern (with at least one group) 
                //                                 ... Define output in DAG graph (substring
                //                                     matching first group is used as label).
                //                                     If the group is empty, the dependency
                //                                     is not shown in the graph.
                //                                     Useful only with /d option.

                //         For an example of a dependency file, see near end of this help text.

                //         A pattern is a list of subpatterns separated by colons
                //           subpattern:subpattern:...
                //         where a subpattern can be a list of basepatterns separated by semicolons:
                //           basepattern;subpattern;...
                //         A basepattern, finally, can be one of the following:
                //           ^regexp$
                //           ^regexp
                //           regexp$
                //           fixedstring
                //           wildcardpath, which contains . (or /), * and ** with the following
                //                         meanings:
                //               .       is replaced with the reg.exp. [.] (matches single period)
                //               *       is replaced with the reg.exp. for an <ident> (a ""name"")
                //               **      is usually replaced with <ident>(?:.<ident>)* (a 
                //                       ""path"").

                //Exit codes:
                //   0    All dependencies ok (including questionable rules).
                //   1    Usage error.
                //   2    Cannot load dependency file (syntax error or file not found).
                //   3    Dependencies not ok.
                //   4    Assembly file specified as argument not found.
                //   5    Other exception.
                //   6    No dependency file found for an assembly in /d and /s 
                //        directories, and /x not specified.

                //############# REST NOT YET UPDATED ##################

                //Example of a dependency file with some important dependencies (all
                //using the wildcardpath syntax):

                //   // Every class may use all classes from its own namespace.
                //        (**).* ---> \1.*

                //   // Special dependency for class names without namespace
                //   // (the pattern above will not work, because it contains a
                //   // period): A class from the global namespace may use
                //   // all classes from that namespace.
                //        * ---> *

                //   // Every class may use all classes from child namespaces
                //   // of its own namespace.
                //        (**).* ---> \1.**.*

                //   // Every class may use all of System.
                //        ** ---> System.**

                //   // Use ALL as abbreviation for MyProgram.**
                //        ALL := MyProgram.**

                //   // All MyProgram classes must not use Windows Forms
                //   // (even though in principle, all classes may use all of 
                //   // System according to the previous ---> rule).
                //        ALL ---! System.Windows.Forms.**

                //   // All MyProgram classes may use classes from antlr.
                //        ALL ---> antlr.**

                //   // Special methods must only call special methods
                //   // and getters and setters.
                //   **::*SpecialMethod* {
                //      ** ---> **::*SpecialMethod*
                //      ** ---> **::get_*
                //      ** ---> **::set_
                //   }

                //   // In DAG output, identify each object by its path (i.e.
                //   // namespace).
                //        ! (**).*

                //   // Classes without namespace are identified by their class name:
                //        ! (*)

                //   // Classes in System.* are identified by the empty group, i.e.,
                //   // they (and arrows reaching them) are not shown at all.
                //        ! ()System.**

                //   // Using % instead of ! puts the node in the 'outer layer', where
                //   // only edges to the inner layer are drawn.");
                //            }
                return exitValue;
            }
        }

        private static void WriteVersion() {
            Log.WriteInfo(msg: "NDepCheck " + VERSION + " (c) HMM�ller, Th.Freudenberg 2006...2017");
        }

        private void AddFileWatchers([NotNull] string positiveFilePattern, [CanBeNull] string negativeFilePattern,
            [NotNull] string scriptName) {
            IEnumerable<string> files = Option.ExpandFilename(positiveFilePattern).Select(f => Path.GetFullPath(f));
            if (negativeFilePattern != null) {
                files = files.Except(Option.ExpandFilename(negativeFilePattern)).Select(f => Path.GetFullPath(f));
            }
            string fullScriptName = Path.GetFullPath(scriptName);
            FileWatcher fw = _fileWatchers.FirstOrDefault(f => f.FullScriptName == fullScriptName);
            if (fw == null) {
                _fileWatchers.Add(fw = new FileWatcher(fullScriptName, this));
            }
            foreach (var f in files) {
                fw.AddFile(f);
            }
        }

        private void RemoveFileWatchers([NotNull] string filePattern) {
            IEnumerable<string> files = Option.ExpandFilename(filePattern).Select(f => Path.GetFullPath(f));
            foreach (var fw in _fileWatchers) {
                foreach (var f in files) {
                    fw.RemoveFile(f);
                }
            }
        }

        private void RemoveFileWatchersOn([NotNull] string scriptName) {
            string fullScriptName = Path.GetFullPath(scriptName);
            FileWatcher fw = _fileWatchers.FirstOrDefault(f => f.FullScriptName == fullScriptName);
            if (fw != null) {
                _fileWatchers.Remove(fw);
                fw.Close();
            }
        }

        public void StartWebServer(Program program, string port, string fileDirectory) {
            if (_webServer != null) {
                throw new ApplicationException("Cannot start webserver if one is already running");
            }
            _webServer = new WebServer(program, port, fileDirectory);
            _webServer.Start();
        }

        public void StopWebServer() {
            _webServer?.Stop();
            _webServer = null;
        }
    }
}