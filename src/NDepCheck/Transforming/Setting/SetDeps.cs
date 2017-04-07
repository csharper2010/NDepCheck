﻿
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NDepCheck.Transforming.Setting {
    public class SetDeps : ITransformer {
        /*
          Deps
            -cf Marking {
                               '~y        => m
                               '~z        => ~m
                 a.b:x*'x&y -- 'y&z -> 'x => y
                 a.b:x*'x&y -- 'y&z ->    => -
             }

          Items

             -cf Marking {
                         a:b.*:             => m
                               '~z          => ~m
                 'x&y -> a.b:x*'x&y -- 'y&z => y
                      -> a.b:x*'x&y -- 'y&z => -
             }

    */

        //public class EdgePattern {
        //    //                                      12         3  4      5
        //    private const string PATTERN_PATTERN = "((.*)->)?.*(--(.*))=>(.*)";

        //    private readonly ItemPattern From;
        //    private readonly MarkerPattern Edge;
        //    private readonly ItemPattern To;
        //    private readonly string result;

        //    public EdgePattern(string p) {
        //        MatchCollection matches = Regex.Matches(p ?? "",  PATTERN_PATTERN);
        //        if (matches[2].Value != "") {
        //            From = ItemPattern.
        //        }

        //    }
        //}


        public string GetHelp(bool detailedHelp) {
            return @"Reset counts on edges.

Configuration options: [-f projectionfile | -p projections]

Transformer options: [-m &] [-q] [-b] [-u &]
  -m &    Regular expression matching usage of edges to clear; default: match all
  -q      Reset questionable count to zero
  -b      Reset bad count to zero
  -u &    Set usage of created edges
  -a &    Add usage of created edges
";
        }

        public bool RunsPerInputContext => false;

        public void Configure(GlobalContext globalContext, string configureOptions) {
            // empty
        }

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            // Only items are changed (Order is added)
            transformedDependencies.AddRange(dependencies);

            bool resetQuestionable = false;
            bool resetBad = false;
            Regex match = null;
            string usage = null;
            bool addUsage = false;

            Option.Parse(transformOptions,
                new OptionAction("m", (args, j) => {
                    match = new Regex(Option.ExtractOptionValue(args, ref j));
                    return j;
                }), new OptionAction("q", (args, j) => {
                    resetQuestionable = true;
                    return j;
                }), new OptionAction("b", (args, j) => {
                    resetBad = true;
                    return j;
                }), new OptionAction("u", (args, j) => {
                    usage = Option.ExtractOptionValue(args, ref j);
                    addUsage = false;
                    return j;
                }), new OptionAction("a", (args, j) => {
                    usage = Option.ExtractOptionValue(args, ref j);
                    addUsage = true;
                    return j;
                }));

            foreach (var d in dependencies) {
                if (match == null || d.Usage.Any(u => match.IsMatch(u))) {
                    if (resetQuestionable) {
                        d.ResetQuestionable();
                    }
                    if (resetBad) {
                        d.ResetBad();
                    }
                    if (usage != null) {
                        if (addUsage) {
                            d.AddUsage(usage);
                        } else {
                            d.SetUsage(usage);
                        }
                    }
                }
            }

            return Program.OK_RESULT;
        }

        public void FinishTransform(GlobalContext context) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            var a = Item.New(ItemType.SIMPLE, "A");
            var b = Item.New(ItemType.SIMPLE, "B");
            return new[] {
                new Dependency(a, a, source: null, usage: "", ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source: null, usage: "use+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source: null, usage: "define", ct:5, questionableCt:0, badCt:2),
            };                               
        }                                    
    }
}
