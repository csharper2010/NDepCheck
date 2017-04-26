﻿using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Modifying {
    public class MarkDeps : ITransformer {
        public static readonly Option DependencyMatchOption = new Option("dm", "dependency-match", "pattern", "Pattern for dependencies", @default: "all dependencies", multiple: true);

        public static readonly Option MarkLeftItemOption = new Option("ml", "mark-left", "marker", "Marker to add to item on left", @default: "", multiple: true);
        public static readonly Option MarkDependencyItemOption = new Option("md", "mark-dependency", "marker", "Marker to add to dependency", @default: "", multiple: true);
        public static readonly Option MarkRightItemOption = new Option("mr", "mark-right", "marker", "Marker to add to item on right", @default: "", multiple: true);

        public static readonly Option UnmarkLeftItemOption = new Option("ul", "unmark-left", "marker", "Marker to be removed from item on left", @default: "", multiple: true);
        public static readonly Option UnmarkDependencyItemOption = new Option("ud", "unmark-dependency", "marker", "Marker to be removed from dependency", @default: "", multiple: true);
        public static readonly Option UnmarkRightItemOption = new Option("ur", "unmark-right", "marker", "Marker to be removed from item on right", @default: "", multiple: true);

        public static readonly Option ClearLeftItemOption = new Option("cl", "clear-left", "", "Remove all markers from item on left", @default: false);
        public static readonly Option ClearDependencyItemOption = new Option("cd", "clear-dependency", "", "Remove all markersdependency", @default: false);
        public static readonly Option ClearRightItemOption = new Option("cr", "clear-right", "", "Remove all markers from item on right", @default: false);

        private static readonly Option[] _configOptions = { DependencyMatchOption,
            MarkLeftItemOption, MarkDependencyItemOption, MarkRightItemOption,
            UnmarkLeftItemOption, UnmarkDependencyItemOption, UnmarkRightItemOption
        };

        public string GetHelp(bool detailedHelp, string filter) {
            string result = $@"Modify counts and markers on dependencies, delete or keep dependencies.

Configuration options: None

Transformer options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}";
            if (detailedHelp) {
                result += @"

A dependency match has the format

    usingItemMatch -- dependencyMatch -> usedItemMatch

Each part can be empty, therefore the simplest match (which matches every
dependency) is -- -> or, equivalently, --->.

A dependency is modified if all three matches match it; empty matches
match always.

The three parts have the following syntax:
    usingItemMatch, usedItemMatch
        item pattern (see -help itempattern)

    dependencyMatch
        dependency pattern (see -help dependency)

Examples:
   'From -- -> 'To        Match dependencies where using item
                          has marker From and used item has marker To

   -- 'OnCycle ->         Match all dependencies with marker OnCycle 

   // TODO: add .Net examples
"; // TODO: add .Net examples


            }
            return result;
        }

        public bool RunsPerInputContext => false;

        private bool _ignoreCase;

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform(GlobalContext globalContext, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            var matches = new List<ItemDependencyItemMatch>();

            bool clearLeft = false;
            bool clearDependency = false;
            bool clearRight = false;

            var markersToAddOnLeft = new List<string>();
            var markersToAddOnDep = new List<string>();
            var markersToAddOnRight = new List<string>();

            var markersToRemoveOnLeft = new List<string>();
            var markersToRemoveOnDep = new List<string>();
            var markersToRemoveOnRight = new List<string>();

            Option.Parse(globalContext, transformOptions,
                DependencyMatchOption.Action((args, j) => {
                    string pattern = Option.ExtractRequiredOptionValue(args, ref j, "missing dependency match pattern", allowOptionValue: true);
                    matches.Add(ItemDependencyItemMatch.Create(pattern, _ignoreCase));
                    return j;
                }),
                MarkLeftItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker");
                    markersToAddOnLeft.Add(marker.TrimStart('\''));
                    return j;
                }),
                MarkDependencyItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker");
                    markersToAddOnDep.Add(marker.TrimStart('\''));
                    return j;
                }),
                MarkRightItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker");
                    markersToAddOnRight.Add(marker.TrimStart('\''));
                    return j;
                }),
                UnmarkLeftItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker");
                    markersToRemoveOnLeft.Add(marker.TrimStart('\''));
                    return j;
                }),
                UnmarkDependencyItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker");
                    markersToRemoveOnDep.Add(marker.TrimStart('\''));
                    return j;
                }),
                UnmarkRightItemOption.Action((args, j) => {
                    string marker = Option.ExtractRequiredOptionValue(args, ref j, "missing marker");
                    markersToRemoveOnRight.Add(marker.TrimStart('\''));
                    return j;
                }),
                ClearLeftItemOption.Action((args, j) => {
                    clearLeft = true;
                    return j;
                }),
                ClearDependencyItemOption.Action((args, j) => {
                    clearDependency= true;
                    return j;
                }),
                ClearRightItemOption.Action((args, j) => {
                    clearRight= true;
                    return j;
                })
            );

            var leftMatches = new List<Item>();
            var rightMatches = new List<Item>();

            // If no matches are provided, we match all - this is a special case for ease of use
            IEnumerable<Dependency> matchingDependencies = matches.Any() ? dependencies.Where(d => matches.Any(m => m.IsMatch(d))) : dependencies;
            int n = 0;
            foreach (var d in matchingDependencies) {
                leftMatches.Add(d.UsingItem);
                rightMatches.Add(d.UsedItem);
                if (clearDependency) {
                    d.ClearMarkers();
                } else {
                    d.UnionWithMarkers(markersToAddOnDep).RemoveMarkers(markersToRemoveOnDep);
                }
                n++;
            }

            Log.WriteInfo($"Marked {n} dependencies");

            // Items are modified afterwards - match loop above uses unchanged values
            foreach (var left in new HashSet<Item>(leftMatches)) {
                MarkItem(clearLeft, left, markersToAddOnLeft, markersToRemoveOnLeft);
            }
            foreach (var right in new HashSet<Item>(rightMatches)) {
                MarkItem(clearRight, right, markersToAddOnRight, markersToRemoveOnRight);
            }

            transformedDependencies.AddRange(dependencies);
            return Program.OK_RESULT;
        }

        private static void MarkItem(bool clear, Item item, List<string> markersToAdd, List<string> markersToRemove) {
            if (clear) {
                item.ClearMarkers();
            } else {
                item.UnionWithMarkers(markersToAdd).RemoveMarkers(markersToRemove);
            }
        }

        public void AfterAllTransforms(GlobalContext globalContext) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            Item am = Item.New(ItemType.SIMPLE, new[] { "A" }, new[] { "M" });
            Item bm = Item.New(ItemType.SIMPLE, new[] { "B" }, new[] { "M" });
            Item cn = Item.New(ItemType.SIMPLE, new[] { "C" }, new[] { "N" });
            return new[] {
                new Dependency(am, am, source: null, markers: "", ct:10, questionableCt:5, badCt:3),
                new Dependency(am, bm, source: null, markers: "use+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(am, cn, source: null, markers: "define", ct:5, questionableCt:0, badCt:2),
                new Dependency(bm, am, source: null, markers: "define", ct:5, questionableCt:0, badCt:2),
            };
        }
    }
}
