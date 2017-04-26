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
    public class Program {
        public const string VERSION = "V.3.70";

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
        public static readonly Option TransformPluginHelpOption = new ProgramOption(shortname: "tp?", name: "transform-plugin-help", usage: "assembly [filter]", description: "Show help for all transformers in assembly", moreNames: new[] { "cp?" });
        public static readonly Option TransformHelpOption = new ProgramOption(shortname: "tf?", name: "transform-help", usage: "[filter]", description: "Show help for all predefined transformers", moreNames: new[] { "cf?" });
        public static readonly Option TransformPluginDetailedHelpOption = new ProgramOption(shortname: "tp!", name: "transform-plugin-detail", usage: "assembly transformer [filter]", description: "Show detailed help for transformer in assembly", moreNames: new[] { "cp!" });
        public static readonly Option TransformDetailedHelpOption = new ProgramOption(shortname: "tf!", name: "transform-detail", usage: "transformer [filter]", description: "Show detailed help for predefined transformer", moreNames: new[] { "cf!" });

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
        public static readonly Option DoCommandOption = new ProgramOption(shortname: "dc", name: "do-command", usage: "command maxsecs [{ parameters... }]", description: "execute shell command; useful for opening result file");
        public static readonly Option DoScriptOption = new ProgramOption(shortname: "ds", name: "do-script", usage: "filename [parameters...]", description: "execute NDepCheck script");
        public static readonly Option DoScriptLoggedOption = new ProgramOption(shortname: "dl", name: "do-script-logged", usage: "scriptname", description: "execute NDepCheck script with log output");
        public static readonly Option DoScriptHelpOption = new ProgramOption(shortname: "ds?", name: "do-script-help", usage: "scriptname", description: "show help for NDepCheck script");
        public static readonly Option FormalParametersOption = new ProgramOption(shortname: "fp", name: "formal-parameters", usage: "name [default]", description: "define formal parameters for -ds call");
        public static readonly Option DoDefineOption = new ProgramOption(shortname: "dd", name: "do-define", usage: "name value", description: "define name gobally as value");
        public static readonly Option DoResetOption = new ProgramOption(shortname: "dr", name: "do-reset", usage: "[filename]", description: "reset state; and read file as dip file if provided");
        public static readonly Option DoTimeOption = new ProgramOption(shortname: "dt", name: "do-time", usage: "secs", description: "log execution time for commands running longer than secs seconds; default: 10");

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
        public static readonly Option ListFilesOption = new ProgramOption(shortname: "ls", name: "list", usage: "[-r] [filespec]", description: "list matching files");
        public static readonly Option GarbageCollectionOption = new ProgramOption(shortname: "gc", name: "garbage-collect", usage: "", description: "run garbage collection");
        public static readonly Option LogVerboseOption = new ProgramOption(shortname: "lv", name: "log-verbose", usage: "", description: "verbose logging");
        public static readonly Option LogChattyOption = new ProgramOption(shortname: "lc", name: "log-chatty", usage: "", description: "chatty logging");
        public static readonly Option LogReducedOption = new ProgramOption(shortname: "lr", name: "log-reduced", usage: "", description: "standard logging");
        public static readonly Option LazyOption = new ProgramOption(shortname: "lz", name: "lazy", usage: "", description: "execute readers and transformers lazily (NOT YET IMPLEMENTED FULLY)");

        private static readonly Option[] _allOptions = {
            HelpAllOption, HelpDetailedHelpOption, DebugOption,
            ReadPluginOption, ReadOption, ReadFileOption, ReadPluginHelpOption, ReadHelpOption, ReadPluginDetailedHelpOption, ReadDetailedHelpOption,
            ConfigurePluginOption, ConfigureOption,
            TransformPluginOption, TransformOption, TransformUndo, TransformTestDataOption, TransformPluginHelpOption, TransformHelpOption, TransformPluginDetailedHelpOption, TransformDetailedHelpOption,
            WritePluginOption, WriteFileOption, WriteDipOption, WriteTestDataOption, WritePluginHelpOption, WriteHelpOption, WritePluginDetailedHelpOption, WriteDetailedHelpOption,
            CalculatePluginOption, CalculateOption, CalculatePluginHelpOption, CalculateHelpOption, CalculatePluginDetailedHelpOption, CalculateDetailedHelpOption,
            DoBreakOption, DoCommandOption, DoScriptOption, DoScriptLoggedOption, DoScriptHelpOption, DoDefineOption, DoResetOption, DoTimeOption,
            WatchFilesOption, UnwatchFilesOption, UnwatchTriggersOption,
            HttpRunOption, HttpStopOption,
            IgnoreCaseOption,
            InteractiveOption, InteractiveStopOption, InteractiveWriteOption, InteractiveDependencyMatchOption, InteractiveItemMatchOption,
            CurrentDirectoryOption, ListFilesOption, GarbageCollectionOption, LogVerboseOption, LogChattyOption, LogReducedOption, LazyOption,
        };

        private readonly List<FileWatcher> _fileWatchers = new List<FileWatcher>();

        private WebServer _webServer;

        private string _interactiveLogFile {
            get; set;
        }

        public static int Main(string[] args) {
            ItemType.New("DUMMY(DUMMY)"); // For init - TODO - REALLY still necessary??
            Log.SetLevel(Log.Level.Standard);

            Log.Logger = new ConsoleLogger();

            var program = new Program();
            try {
                var globalContext = new GlobalContext();

                int lastResult = program.Run(args, new string[0], globalContext, writtenMasterFiles: null, logCommands: false);

                while (program._webServer != null || program._interactiveLogFile != null || program._fileWatchers.Any()) {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(value: "Type /?<enter> for help; or q<enter> for stopping NDepCheck.");
                    Console.Write(value: globalContext.Name + " NDepCheck> ");
                    Console.ResetColor();
                    string commands = Console.ReadLine();
                    string lowerInvariant = commands?.Trim().ToLowerInvariant();
                    if (commands == null
                        || Option.ArgMatches("q", "quit", "exit")
                        || lowerInvariant == "q"
                        || lowerInvariant == "exit") {
                        break;
                    } else {
                        commands = commands.Trim();
                        if (commands != "") {
                            InteractiveLog(program, commands);
                            var writtenMasterFiles = new List<string>();

                            program.Run(args: commands.Split(' ').Select(s => s.Trim()).Where(s => s != "").ToArray(),
                                passedValues: new string[0], globalContext: globalContext,
                                writtenMasterFiles: writtenMasterFiles, logCommands: false);

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
                if (Log.IsChattyEnabled)
                    Console.WriteLine(ex);
                return EXCEPTION_RESULT;
            } finally {
                // Main may be called multiple times in tests; therefore we clear all caches
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

        public int Run(string[] args, string[] passedValues, GlobalContext globalContext,
                       [CanBeNull] List<string> writtenMasterFiles, bool logCommands) {
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
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();
                    if (arg == "help") {
                        Log.WriteWarning("For help, use -? or -help");
                    } else if (HelpAllOption.IsMatch(arg)) {
                        // -? [filter]
                        string filter = ExtractOptionValue(globalContext, args, ref i, allowOptionValue: true);
                        return UsageAndExit(message: null, globalContext: globalContext, withIntro: _interactiveLogFile == null,
                                            detailed: filter != null, filter: filter ?? "");
                    } else if (HelpDetailedHelpOption.IsMatch(arg)) {
                        // -! [filter]
                        string filter = ExtractOptionValue(globalContext, args, ref i, allowOptionValue: true);
                        return UsageAndExit(message: null, globalContext: globalContext, withIntro: true,
                                            detailed: true, filter: filter ?? "");
                    } else if (arg == "-debug" || arg == "/debug") {
                        // -debug
                        Debugger.Launch();
                    } else if (ReadPluginOption.IsMatch(arg)) {
                        // -rp    assembly reader filepattern [- filepattern]
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string reader = ExtractNextValue(globalContext, args, ref i);
                        string filePattern = ExtractNextValue(globalContext, args, ref i);
                        globalContext.CreateInputOption(args, ref i, filePattern, assembly, reader);
                    } else if (ReadFileOption.IsMatch(arg)) {
                        // -rf    reader filepattern [- filepattern]
                        string reader = ExtractOptionValue(globalContext, args, ref i);
                        string filePattern = ExtractNextValue(globalContext, args, ref i);
                        globalContext.CreateInputOption(args, ref i, filePattern, "", reader);
                    } else if (ReadOption.IsMatch(arg)) {
                        // -rd    filepattern [- filepattern]
                        // -rd    - filepattern
                        string s = ExtractOptionValue(globalContext, args, ref i);
                        if (s == "-") {
                            string filePattern = ExtractRequiredOptionValue(globalContext, args, ref i, "File pattern missing after -");
                            globalContext.AddNegativeInputOption(filePattern);
                        } else {
                            string filePattern = s;
                            globalContext.CreateInputOption(args, ref i, filePattern, "",
                                IsDllOrExeFile(filePattern) ? typeof(DotNetAssemblyDependencyReaderFactory).FullName :
                                IsDipFile(filePattern) ? typeof(DipReaderFactory).FullName : null);
                        }

                    } else if (ReadPluginHelpOption.IsMatch(arg)) {
                        // -ra?    assembly [filter]
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<IReaderFactory>(assembly, filter);
                    } else if (ReadHelpOption.IsMatch(arg)) {
                        // -rf? [filter]
                        string filter = ExtractOptionValue(globalContext, args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<IReaderFactory>("", filter);
                    } else if (ReadPluginDetailedHelpOption.IsMatch(arg)) {
                        // -ra!    assembly reader
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string reader = ExtractNextValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowDetailedHelp<IReaderFactory>(assembly, reader, filter);
                    } else if (ReadDetailedHelpOption.IsMatch(arg)) {
                        // -rf!    reader
                        string reader = ExtractOptionValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowDetailedHelp<IReaderFactory>("", reader, filter);
                    } else if (ConfigurePluginOption.IsMatch(arg)) {
                        // -cp    assembly transformer { options }
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string transformer = ExtractNextValue(globalContext, args, ref i);
                        string transformerOptions = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ConfigureTransformer(assembly, transformer, transformerOptions,
                                                           forceReloadConfiguration: _interactiveLogFile != null);
                    } else if (ConfigureOption.IsMatch(arg)) {
                        // -cf    transformer  { options }
                        string transformer = ExtractOptionValue(globalContext, args, ref i);
                        string transformerOptions = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ConfigureTransformer("", transformer, transformerOptions,
                                                           forceReloadConfiguration: _interactiveLogFile != null);
                    } else if (TransformPluginOption.IsMatch(arg)) {
                        // -tp    assembly transformer [{ options }]
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string transformer = ExtractNextValue(globalContext, args, ref i);
                        string transformerOptions = ExtractNextValue(globalContext, args, ref i);
                        result = globalContext.Transform(assembly, transformer, transformerOptions);
                    } else if (TransformOption.IsMatch(arg)) {
                        // -tf    transformer  [{ options }]
                        string transformer = ExtractOptionValue(globalContext, args, ref i);
                        string transformerOptions = ExtractNextValue(globalContext, args, ref i);
                        result = globalContext.Transform("", transformer, transformerOptions);
                    } else if (TransformUndo.IsMatch(arg)) {
                        // -tu
                        globalContext.UndoTransform();
                    } else if (TransformTestDataOption.IsMatch(arg)) {
                        // -tt    assembly transformer [{ options }]
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string transformer = ExtractNextValue(globalContext, args, ref i);
                        string transformerOptions = ExtractNextValue(globalContext, args, ref i);
                        globalContext.TransformTestData(assembly, transformer, transformerOptions);
                        globalContext.InputFilesOrTestDataSpecified = true;
                    } else if (TransformPluginHelpOption.IsMatch(arg)) {
                        // -tp?    assembly
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<ITransformer>(assembly, filter);
                    } else if (TransformHelpOption.IsMatch(arg)) {
                        // -tf?
                        string filter = ExtractOptionValue(globalContext, args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<ITransformer>("", filter);
                    } else if (TransformPluginDetailedHelpOption.IsMatch(arg)) {
                        // -ta!    assembly transformer
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string transformer = ExtractNextValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowDetailedHelp<ITransformer>(assembly, transformer, filter);
                    } else if (TransformDetailedHelpOption.IsMatch(arg)) {
                        // -tf!    transformer
                        string transformer = ExtractOptionValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowDetailedHelp<ITransformer>("", transformer, filter);
                    } else if (WritePluginOption.IsMatch(arg)) {
                        // -wp    assembly writer [{ options }] filename
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string writer = ExtractNextValue(globalContext, args, ref i);
                        string s = ExtractNextValue(globalContext, args, ref i);
                        string masterFileName = Write(globalContext, s, args, ref i,
                            (writerOptions, fileName) => globalContext.RenderToFile(assembly, writer, writerOptions, fileName));
                        writtenMasterFiles?.Add(masterFileName);
                    } else if (WriteFileOption.IsMatch(arg)) {
                        // -wr    writer  [{ options }] filename
                        string writer = ExtractOptionValue(globalContext, args, ref i);
                        string s = ExtractNextValue(globalContext, args, ref i);
                        string masterFileName = Write(globalContext, s, args, ref i,
                            (writerOptions, fileName) => globalContext.RenderToFile("", writer, writerOptions, fileName));
                        writtenMasterFiles?.Add(masterFileName);
                    } else if (WriteDipOption.IsMatch(arg)) {
                        // -wd    filename
                        string fileName = ExtractOptionValue(globalContext, args, ref i);
                        string masterFileName = globalContext.RenderToFile("", typeof(DipWriter).Name, "", fileName);
                        writtenMasterFiles?.Add(masterFileName);
                    } else if (WriteTestDataOption.IsMatch(arg)) {
                        // -wt    assembly writer [{ options }] filename
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string writer = ExtractNextValue(globalContext, args, ref i);
                        string s = ExtractNextValue(globalContext, args, ref i);
                        Write(globalContext, s, args, ref i,
                            (writerOptions, fileName) => globalContext.RenderTestData(assembly, writer, writerOptions, fileName));
                    } else if (WritePluginHelpOption.IsMatch(arg)) {
                        // -wp?    assembly
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<IRenderer>(assembly, filter);
                    } else if (WriteHelpOption.IsMatch(arg)) {
                        // -wr?
                        string filter = ExtractOptionValue(globalContext, args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<IRenderer>("", filter);
                    } else if (WritePluginDetailedHelpOption.IsMatch(arg)) {
                        // -wp!    assembly reader
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string writer = ExtractNextValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowDetailedHelp<IRenderer>(assembly, writer, filter);
                    } else if (WriteDetailedHelpOption.IsMatch(arg)) {
                        // -wr!    reader
                        string writer = ExtractOptionValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowDetailedHelp<IRenderer>("", writer, filter);
                    } else if (CalculatePluginOption.IsMatch(arg)) {
                        // -xp    varname assembly calculator [varname ...]
                        string varname = ExtractOptionValue(globalContext, args, ref i);
                        string assembly = ExtractNextValue(globalContext, args, ref i);
                        string calculator = ExtractNextValue(globalContext, args, ref i);
                        List<string> input = ExtractInputVars(globalContext, args, ref i);
                        globalContext.Calculate(varname, assembly, calculator, input);
                    } else if (CalculateOption.IsMatch(arg)) {
                        // -xf    varname calculator [varname ...]
                        string varname = ExtractOptionValue(globalContext, args, ref i);
                        string calculator = ExtractNextValue(globalContext, args, ref i);
                        List<string> input = ExtractInputVars(globalContext, args, ref i);
                        globalContext.Calculate(varname, "", calculator, input);
                    } else if (CalculatePluginHelpOption.IsMatch(arg)) {
                        // -xa?    assembly [filter]
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<ICalculator>(assembly, filter);
                    } else if (CalculateHelpOption.IsMatch(arg)) {
                        // -xf? [filter]
                        string filter = ExtractOptionValue(globalContext, args, ref i);
                        globalContext.ShowAllPluginsAndTheirHelp<ICalculator>("", filter);
                    } else if (CalculatePluginDetailedHelpOption.IsMatch(arg)) {
                        // -xa!    assembly calculator
                        string assembly = ExtractOptionValue(globalContext, args, ref i);
                        string calculator = ExtractNextValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowDetailedHelp<IRenderer>(assembly, calculator, filter);
                    } else if (CalculateDetailedHelpOption.IsMatch(arg)) {
                        // -xf!    calculator
                        string calculator = ExtractOptionValue(globalContext, args, ref i);
                        string filter = ExtractNextValue(globalContext, args, ref i);
                        globalContext.ShowDetailedHelp<ICalculator>("", calculator, filter);
                    } else if (DoBreakOption.IsMatch(arg)) {
                        // -db
                        Log.WriteInfo(msg: "---- Stop reading options (-db)");
                        goto DONE;
                    } else if (DoCommandOption.IsMatch(arg)) {
                        // -dc    command
                        string cmd = ExtractRequiredOptionValue(globalContext, args, ref i, "Missing command after -dc");
                        int maxRunTime = ExtractIntOptionValue(globalContext, args, ref i, "Missing maximum runtime in seconds after -dc");
                        string cmdArgs = ExtractNextValue(globalContext, args, ref i).TrimStart('{', ' ', '\r', '\n').TrimEnd('}', ' ', '\r', '\n').Replace("\r\n", " ");
                        try {
                            var process = new Process {
                                StartInfo =
                                    new ProcessStartInfo(cmd) {
                                        UseShellExecute = false,
                                        Arguments = cmdArgs,
                                        WorkingDirectory = Environment.CurrentDirectory,
                                        RedirectStandardError = true,
                                        RedirectStandardOutput = true
                                    }
                            };
                            if (process.Start()) {
                                Log.WriteInfo(msg: $"Started process '{cmd}' with arguments '{cmdArgs}'");
                                process.WaitForExit(1000 * maxRunTime);
                                int exitCode = process.ExitCode;
                                if (exitCode == 0) {
                                    Log.WriteWarning($"Process {cmd} exited with code {exitCode}");
                                } else {
                                    Log.WriteInfo($"Process {cmd} completed with code 0");
                                }
                            } else {
                                Log.WriteError(msg: $"Could not start process '{cmd}' with arguments '{cmdArgs}'");
                            }
                            // Starting a process is like providing an input file
                            globalContext.InputFilesOrTestDataSpecified = true;
                        } catch (Exception ex) {
                            Log.WriteError(msg: $"Could not start process '{cmd}'; reason: {ex.Message}");
                            result = EXCEPTION_RESULT;
                        }
                    } else if (DoScriptHelpOption.IsMatch(arg)) {
                        string fileName = ExtractOptionValue(globalContext, args, ref i);
                        result = RunFrom(fileName, new string[0], globalContext, writtenMasterFiles,
                                         logCommands: false, showParameters: true);
                    } else if (DoScriptOption.IsMatch(arg) || DoScriptLoggedOption.IsMatch(arg)) {
                        // -dp    filename
                        // -dl    filename
                        string fileName = ExtractOptionValue(globalContext, args, ref i);
                        string[] paramValues = GetParamsList(globalContext, args, ref i);
                        result = RunFrom(fileName, paramValues, globalContext, writtenMasterFiles,
                                         logCommands: DoScriptLoggedOption.IsMatch(arg), showParameters: false);
                        // file is also an input file - and if there are no input files in -o, the error will come up there.
                        globalContext.InputFilesOrTestDataSpecified = true;
                    } else if (FormalParametersOption.IsMatch(arg)) {
                        Log.WriteError($"Option {FormalParametersOption.Name} must not occur after other options");
                    } else if (DoDefineOption.IsMatch(arg)) {
                        // -dd    name value
                        string varname = ExtractOptionValue(globalContext, args, ref i);
                        if (varname == null) {
                            globalContext.ShowAllValues();
                        } else {
                            string varvalue = ExtractNextValue(globalContext, args, ref i);
                            globalContext.SetDefine(varname, varvalue, location: "after -dd option");
                        }
                    } else if (DoResetOption.IsMatch(arg)) {
                        // -dr    [filename]
                        globalContext.ResetAll();

                        string fileName = ExtractNextValue(globalContext, args, ref i);
                        if (fileName != null && IsDipFile(fileName)) {
                            globalContext.CreateInputOption(args, ref i, fileName, assembly: "",
                                readerFactoryClass: typeof(DipReaderFactory).FullName);
                        }
                    } else if (DoTimeOption.IsMatch(arg)) {
                        globalContext.TimeLongerThan = TimeSpan.FromSeconds(ExtractIntOptionValue(globalContext, args, ref i, "Missing seconds"));
                    } else if (WatchFilesOption.IsMatch(arg)) {
                        // -aw    [filepattern [- filepattern]] script
                        string positive = ExtractOptionValue(globalContext, args, ref i);
                        string s = ExtractNextValue(globalContext, args, ref i);
                        string negative, scriptName;
                        if (s == "-") {
                            negative = ExtractNextValue(globalContext, args, ref i);
                            scriptName = ExtractNextValue(globalContext, args, ref i);
                        } else {
                            negative = null;
                            scriptName = s ?? positive;
                        }
                        AddFileWatchers(positive, negative, scriptName);
                    } else if (UnwatchFilesOption.IsMatch(arg)) {
                        // -au    filepattern
                        string filePattern = ExtractOptionValue(globalContext, args, ref i);
                        RemoveFileWatchers(filePattern);
                    } else if (UnwatchTriggersOption.IsMatch(arg)) {
                        // -an    script
                        string scriptName = ExtractOptionValue(globalContext, args, ref i);
                        RemoveFileWatchersOn(scriptName);
                    } else if (HttpRunOption.IsMatch(arg)) {
                        // -hr    port directory
                        string port = ExtractOptionValue(globalContext, args, ref i);
                        string fileDirectory = ExtractNextValue(globalContext, args, ref i);
                        StartWebServer(program: this, port: port, fileDirectory: fileDirectory);
                        ranAsWebServer = true;
                    } else if (HttpRunOption.IsMatch(arg)) {
                        // -hs
                        StopWebServer();
                    } else if (IgnoreCaseOption.IsMatch(arg)) {
                        // -ic
                        globalContext.IgnoreCase = true;
                    } else if (InteractiveOption.IsMatch(arg)) {
                        // -ia    [filename]
                        string filename = ExtractOptionValue(globalContext, args, ref i);
                        _interactiveLogFile = filename == null ? "" : Path.GetFullPath(filename);
                        if (_interactiveLogFile != "") {
                            Log.WriteInfo("Logging interactive input to " + _interactiveLogFile);
                        }
                        InteractiveLog(this, "// Opened interactive log " + _interactiveLogFile);
                    } else if (InteractiveStopOption.IsMatch(arg)) {
                        // -is
                        _interactiveLogFile = null;
                    } else if (InteractiveWriteOption.IsMatch(arg)) {
                        // -iw # [pattern]
                        int maxCount = ExtractIntOptionValue(globalContext, args, ref i, "Not a valid number");
                        string pattern = ExtractNextValue(globalContext, args, ref i);
                        globalContext.LogAboutNDependencies(maxCount, pattern);
                    } else if (InteractiveDependencyMatchOption.IsMatch(arg)) {
                        // -id [pattern]
                        string pattern = ExtractOptionValue(globalContext, args, ref i, allowOptionValue: true /* as --x-> patterns start with -*/);
                        globalContext.LogDependencyCount(pattern);
                    } else if (InteractiveItemMatchOption.IsMatch(arg)) {
                        // -ii [pattern]
                        string pattern = ExtractOptionValue(globalContext, args, ref i, allowOptionValue: true /* as --x-> patterns start with -*/);
                        globalContext.LogItemCount(pattern);
                    } else if (CurrentDirectoryOption.IsMatch(arg)) {
                        // -cd    [directory]
                        string directory = ExtractOptionValue(globalContext, args, ref i);
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
                    } else if (ListFilesOption.IsMatch(arg)) {
                        string filename;
                        bool recursive;
                        string s = ExtractNextValue(globalContext, args, ref i, allowOptionValue: true);
                        if (s != null && Option.ArgMatches(s, "r", "recursive")) {
                            recursive = true;
                            filename = ExtractNextValue(globalContext, args, ref i);
                        } else {
                            recursive = false;
                            filename = s;
                        }
                        ListFilesAndDirectories(recursive, filename);
                    } else if (GarbageCollectionOption.IsMatch(arg)) {
                        GC.Collect(2);
                        Log.WriteInfo($"Process has {Environment.WorkingSet / 1024 / 1024} MB allocated, " +
                                      $"{GC.GetTotalMemory(true) / 1024 / 1024} MB managed memory.");
                    } else if (LogVerboseOption.IsMatch(arg)) {
                        // -lv
                        Log.SetLevel(Log.Level.Verbose);
                        WriteVersion();
                    } else if (LogChattyOption.IsMatch(arg)) {
                        // -lc
                        Log.SetLevel(Log.Level.Chatty);
                        WriteVersion();
                    } else if (LogReducedOption.IsMatch(arg)) {
                        // -lr
                        Log.SetLevel(Log.Level.Standard);
                    } else if (LazyOption.IsMatch(arg)) {
                        // -lz
                        // (lazy reading and transforming NOT YET IMPLEMENTED)
                        globalContext.WorkLazily = true;
                    } else if (IsDllOrExeFile(arg)) {
                        globalContext.CreateInputOption(args, ref i, arg, assembly: "",
                            readerFactoryClass: typeof(DotNetAssemblyDependencyReaderFactory).FullName);
                    } else if (IsDipFile(arg)) {
                        globalContext.CreateInputOption(args, ref i, arg, assembly: "",
                            readerFactoryClass: typeof(DipReaderFactory).FullName);
                    } else if (IsNdFile(arg)) {
                        string[] paramValues = GetParamsList(globalContext, args, ref i);
                        result = RunFrom(arg, paramValues, globalContext, writtenMasterFiles, logCommands: false, showParameters: false);
                        globalContext.InputFilesOrTestDataSpecified = true;
                    } else {
                        return UsageAndExit(message: "Unsupported option '" + arg + "'", globalContext: globalContext);
                    }

                    if (logCommands) {
                        Log.WriteInfo($">>>> Finished {arg}");
                    }
                    stopWatch.Stop();
                    LogElapsedTime(globalContext, stopWatch, arg);
                }
            } catch (ArgumentException ex) {
                return UsageAndExit(ex.Message, globalContext);
            } catch (Exception ex) {
                Log.WriteError($"Could not run previous command; reason: {ex.GetType().Name} {ex.Message}");
                return EXCEPTION_RESULT;
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

            if (Log.IsChattyEnabled) {
                Log.WriteInfo(msg: "Completed with exitcode " + result);
            }

            return result;
        }

        [CanBeNull]
        private static string ExtractNextValue(GlobalContext globalContext, string[] args, ref int i, bool allowOptionValue = false) {
            return globalContext.ExpandDefinesAndHexChars(Option.ExtractNextValue(args, ref i, allowOptionValue), null);
        }

        [CanBeNull]
        private static string ExtractOptionValue(GlobalContext globalContext, string[] args, ref int i, bool allowOptionValue = false) {
            return globalContext.ExpandDefinesAndHexChars(Option.ExtractOptionValue(args, ref i, allowOptionValue), null);
        }

        private static int ExtractIntOptionValue(GlobalContext globalContext, string[] args, ref int i, string message) {
            int value;
            if (!int.TryParse(ExtractOptionValue(globalContext, args, ref i), out value)) {
                Option.ThrowArgumentException(message, string.Join(" ", args));
            }
            return value;
        }

        [NotNull]
        private static string ExtractRequiredOptionValue(GlobalContext globalContext, string[] args, ref int i, string message) {
            return globalContext.ExpandDefinesAndHexChars(Option.ExtractRequiredOptionValue(args, ref i, message), null);
        }

        private static string[] GetParamsList(GlobalContext globalContext, string[] args, ref int i) {
            var result = new List<string>();
            for (;;) {
                string p = ExtractNextValue(globalContext, args, ref i);
                if (p == null) {
                    break;
                }
                result.Add(p == ":" ? null : p);
            }
            return result.ToArray();
        }

        private void ListFilesAndDirectories(bool recursive, [CanBeNull] string filename) {
            const int MAX = 100;
            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string directoryName = Path.GetDirectoryName(filename);
            string directory = string.IsNullOrWhiteSpace(directoryName) ? Environment.CurrentDirectory : directoryName;
            string pattern = Path.GetFileName(filename) ?? "*";
            IEnumerable<string> names = Directory.GetFiles(directory, pattern, searchOption)
                .Select(s => "  " + s)
                .Concat(Directory.GetDirectories(directory, recursive ? "*" : pattern, searchOption)
                                 .Select(s => "D " + s))
                .OrderBy(s => s.Substring(2))
                .Take(MAX + 1)
                .ToArray();
            foreach (var f in names.Take(MAX)) {
                Log.WriteInfo(f);
            }
            if (names.Skip(MAX).Any()) {
                Log.WriteInfo("....");
            }
        }

        public static void LogElapsedTime(GlobalContext globalContext, Stopwatch stopWatch, string arg) {
            TimeSpan elapsed = stopWatch.Elapsed;
            if (elapsed >= globalContext.TimeLongerThan) {
                if (elapsed < TimeSpan.FromMinutes(1)) {
                    Log.WriteInfo($"{arg} took {elapsed.TotalSeconds:F3} s");
                } else if (elapsed < TimeSpan.FromHours(1)) {
                    Log.WriteInfo($@"{arg} took {elapsed:mm\:ss} minutes");
                } else if (elapsed < TimeSpan.FromDays(1)) {
                    Log.WriteInfo($@"{arg} took {elapsed:hh\:mm} hours");
                } else {
                    Log.WriteInfo($@"{arg} took {elapsed:d\:hh\:mm} days, hours and minutes");
                }
            }
        }

        private static List<string> ExtractInputVars(GlobalContext globalContext, string[] args, ref int i) {
            var input = new List<string>();
            for (var s = ExtractNextValue(globalContext, args, ref i); s != null; s = ExtractNextValue(globalContext, args, ref i)) {
                input.Add(s);
            }
            return input;
        }

        private static string Write(GlobalContext globalContext, string s, string[] args, ref int i, Func<string, string, string> action) {
            string writerOptions, fileName;
            if (Option.IsOptionGroupStart(s)) {
                writerOptions = s;
                fileName = ExtractNextValue(globalContext, args, ref i);
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

        private static bool IsNdFile(string arg) {
            return arg.EndsWith(value: ".nd", comparisonType: StringComparison.InvariantCultureIgnoreCase);
        }

        internal int RunFrom([NotNull] string fileName, [NotNull, ItemNotNull] string[] passedValues, [NotNull] GlobalContext globalContext,
                             [CanBeNull] List<string> writtenMasterFiles, bool logCommands, bool showParameters) {
            if (Path.GetExtension(fileName) == "") {
                fileName = Path.ChangeExtension(fileName, ".nd");
            }

            int lineNo = 0;
            try {
                var argsList = new List<string>();
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

                        if (splitLine.Any(s => Option.IsOptionGroupStart(s))) {
                            argsList.AddRange(splitLine);
                            int? groupStart = splitLine
                                    .Select((s, j) => new { S = s, I = j })
                                    .FirstOrDefault(sj => Option.IsOptionGroupStart(sj.S))?.I;
                            int? groupEnd = splitLine
                                    .Select((s, j) => new { S = s, I = j })
                                    .FirstOrDefault(sj => Option.IsOptionGroupEnd(sj.S))?.I;
                            // If there is a } after the {, we are NOT in inBraces mode.
                            inBraces = !(groupEnd > groupStart);
                        } else if (splitLine.Any(s => Option.IsOptionGroupEnd(s))) {
                            inBraces = false;
                            argsList.AddRange(splitLine);
                        } else if (!inBraces) {
                            argsList.AddRange(splitLine);
                        } else {
                            argsList.Add(line);
                        }
                    }
                }

                var args = argsList.ToArray();

                ValuesFrame locals = new ValuesFrame();

                var showParametersText = showParameters ? new StringBuilder() : null;

                int i = 0;
                int passedValueCount = 0;
                for (; i < argsList.Count; i++) {
                    string arg = argsList[i];
                    if (FormalParametersOption.IsMatch(arg)) {
                        string name = Option.ExtractRequiredOptionValue(args, ref i, $"missing parameter name after {FormalParametersOption}");
                        string defaultValue = locals.ExpandDefines(Option.ExtractNextValue(args, ref i), null);
                        string passedValue = passedValueCount < passedValues.Length
                            ? passedValues[passedValueCount++]
                            : null;
                        locals.SetDefine(name, passedValue ?? defaultValue ?? "", "-fp definition");
                        showParametersText?.AppendLine($"  {name} - default value: '{defaultValue ?? ""}'");
                    } else {
                        break;
                    }
                }

                if (showParametersText != null) {
                    Log.WriteInfo($"Parameters of script {fileName}:");
                    Log.WriteInfo(showParametersText.ToString());
                    return OPTIONS_PROBLEM;
                } else {
                    args = args.Skip(i).ToArray();

                    var locallyWrittenFiles = new List<string>();
                    string previousCurrentDirectory = Environment.CurrentDirectory;
                    ValuesFrame previousLocals = globalContext.SetLocals(locals);
                    try {
                        Environment.CurrentDirectory = Path.GetDirectoryName(path: Path.GetFullPath(fileName)) ?? "";
                        return Run(args: args, passedValues: passedValues, globalContext: globalContext,
                                   writtenMasterFiles: locallyWrittenFiles, logCommands: logCommands);
                    } finally {
                        writtenMasterFiles?.AddRange(collection: locallyWrittenFiles.Select(Path.GetFullPath));
                        Environment.CurrentDirectory = previousCurrentDirectory;
                        globalContext.SetLocals(previousLocals);
                    }
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
                Console.WriteLine("*** THIS SHOULD BE A HELP TEXT ABOUT NDepCheck items in general");
                Console.WriteLine(Item.ITEM_HELP);
                return exitValue;
            } else if (filter.StartsWith("dep")) {
                Console.WriteLine("*** THIS SHOULD BE A HELP TEXT ABOUT NDepCheck dependencies in general");
                Console.WriteLine(DependencyMatch.DEPENDENCY_MATCH_HELP);
                return exitValue;
            } else if (filter.StartsWith("marker")) {
                Console.WriteLine(ObjectWithMarkers.MARKER_HELP);
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
                    globalContext.ShowAllPluginsAndTheirHelp<IRenderer>("", filter);
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