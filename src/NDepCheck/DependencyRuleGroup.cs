﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck {
    public class DependencyRuleGroup : Pattern {
        private static readonly Comparison<DependencyRule> _sortOnDescendingHitCount = (r1, r2) => r2.HitCount - r1.HitCount;

        [NotNull]
        private readonly List<DependencyRule> _allowed;
        [NotNull]
        private readonly List<DependencyRule> _questionable;
        [NotNull]
        private readonly List<DependencyRule> _forbidden;

        [NotNull]
        private readonly string _group;
        [CanBeNull]
        private readonly IMatcher[] _groupMatchersOrNullForGlobalRules;
        [NotNull]
        private readonly ItemType _groupType;

        private DependencyRuleGroup([NotNull] ItemType groupType, [NotNull] string group, [NotNull] IEnumerable<DependencyRule> allowed,
                [NotNull] IEnumerable<DependencyRule> questionable, [NotNull] IEnumerable<DependencyRule> forbidden,
                bool ignoreCase) {
            if (groupType == null && group != "") {
                throw new ArgumentException("groupType is null, but group is not empty", nameof(groupType));
            }

            _groupType = groupType;
            _group = group;
            _groupMatchersOrNullForGlobalRules = group == "" ? null : CreateMatchers(groupType, group, 0, ignoreCase);
            _allowed = allowed.ToList();
            _questionable = questionable.ToList();
            _forbidden = forbidden.ToList();
        }

        public DependencyRuleGroup([NotNull] ItemType groupType, [NotNull] string group, bool ignoreCase)
            : this(groupType, group,
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>(),
                Enumerable.Empty<DependencyRule>(), ignoreCase) {
            // empty
        }

        [NotNull]
        public string Group => _group;

        public bool IsNotEmpty => _allowed.Any() || _questionable.Any() || _forbidden.Any();

        /// <summary>
        /// Add one or more <c>DependencyRules</c>s from a single input line.
        /// public for testability.
        /// </summary>
        public bool AddDependencyRules([NotNull] DependencyRuleSet parent, [CanBeNull] ItemType usingItemType, [CanBeNull] ItemType usedItemType,
                                       [NotNull] string ruleSourceName, int lineNo, [NotNull] string line, bool ignoreCase, string previousRawUsingPattern, out string rawUsingPattern) {
            if (usingItemType == null || usedItemType == null) {
                Log.WriteError($"Itemtypes not defined - $ line is missing in {ruleSourceName}, dependency rules are ignored", parent.FileIncludeStack, lineNo);
                rawUsingPattern = null;
                return false;
            } else if (line.Contains(DependencyRuleSet.MAYUSE)) {
                IEnumerable<DependencyRule> rules = CreateDependencyRule(parent, usingItemType, usedItemType, ruleSourceName, lineNo, line,
                    DependencyRuleSet.MAYUSE, false, ignoreCase, previousRawUsingPattern, out rawUsingPattern);
                _allowed.AddRange(rules);
                return true;
            } else if (line.Contains(DependencyRuleSet.MAYUSE_RECURSIVE)) {
                IEnumerable<DependencyRule> rules = CreateDependencyRule(parent, usingItemType, usedItemType, ruleSourceName, lineNo, line,
                                                           DependencyRuleSet.MAYUSE_RECURSIVE, false, ignoreCase, previousRawUsingPattern, out rawUsingPattern);
                _allowed.AddRange(rules);
                return true;
            } else if (line.Contains(DependencyRuleSet.MAYUSE_WITH_WARNING)) {
                IEnumerable<DependencyRule> rules = CreateDependencyRule(parent, usingItemType, usedItemType, ruleSourceName, lineNo, line,
                    DependencyRuleSet.MAYUSE_WITH_WARNING, true, ignoreCase, previousRawUsingPattern, out rawUsingPattern);
                _questionable.AddRange(rules);
                return true;
            } else if (line.Contains(DependencyRuleSet.MUSTNOTUSE)) {
                IEnumerable<DependencyRule> rules = CreateDependencyRule(parent, usingItemType, usedItemType, ruleSourceName, lineNo, line,
                                                           DependencyRuleSet.MUSTNOTUSE, false, ignoreCase, previousRawUsingPattern, out rawUsingPattern);
                _forbidden.AddRange(rules);
                return true;
            } else {
                throw new ApplicationException("Unexpected rule at " + ruleSourceName + ":" + lineNo);
            }
        }

        private IEnumerable<DependencyRule> CreateDependencyRule([NotNull] DependencyRuleSet parent,
            [NotNull] ItemType usingItemType, [NotNull] ItemType usedItemType, [NotNull] string ruleSourceName, int lineNo,
            [NotNull] string line, [NotNull] string use, bool questionableRule, bool ignoreCase, 
            string previousRawUsingPattern, out string currentRawUsingPattern) {

            int i = line.IndexOf(use, StringComparison.Ordinal);

            string rawUsingpattern = line.Substring(0, i).Trim();
            if (rawUsingpattern == "") {
                rawUsingpattern = previousRawUsingPattern;
            }
            currentRawUsingPattern = rawUsingpattern;

            string usingPattern = parent.ExpandDefines(rawUsingpattern);

            string rawUsedPattern = line.Substring(i + use.Length).Trim();
            string usedPattern = parent.ExpandDefines(rawUsedPattern);

            string repString = rawUsingpattern + " " + use + " " + rawUsedPattern;
            DependencyRuleRepresentation rep = new DependencyRuleRepresentation(ruleSourceName, lineNo, repString, questionableRule);

            var head = new DependencyRule(usingItemType, usingPattern, usedItemType, usedPattern, rep, ignoreCase);
            var result = new List<DependencyRule> { head };

            if (Log.IsVerboseEnabled) {
                Log.WriteInfo($"Matchers used for checking {repString} ({ruleSourceName}:{lineNo})");
                Log.WriteInfo("  Using: " + string.Join<IMatcher>(", ", head.Using));
                Log.WriteInfo("   Used: " + string.Join<IMatcher>(", ", head.Used));
            }

            if (use == DependencyRuleSet.MAYUSE_RECURSIVE) {
                IEnumerable<DependencyRule> rulesWithMatchingUsingPattern = _allowed.Where(r => r.MatchesUsingPattern(head.Used));

                result.AddRange(rulesWithMatchingUsingPattern.Select(tail => new DependencyRule(usingItemType, head.Using, usedItemType, tail.Used, rep)));
            }

            return result;
        }

        public DependencyRuleGroup Combine([NotNull] DependencyRuleGroup other, bool ignoreCase) {
            return new DependencyRuleGroup(_groupType, _group,
                _allowed.Union(other._allowed),
                _questionable.Union(other._questionable),
                _forbidden.Union(other._forbidden), ignoreCase);
        }

        public bool Check([CanBeNull] IInputContext inputContext, [NotNull] IEnumerable<Dependency> dependencies) {
            bool result = true;
            int reorgCount = 0;
            int nextReorg = 200;

            foreach (Dependency d in dependencies) {
                if (_groupMatchersOrNullForGlobalRules == null || Match(_groupType, _groupMatchersOrNullForGlobalRules, d.UsingItem) != null) {
                    result &= Check(inputContext, d);
                    if (++reorgCount > nextReorg) {
                        _forbidden.Sort(_sortOnDescendingHitCount);
                        _allowed.Sort(_sortOnDescendingHitCount);
                        _questionable.Sort(_sortOnDescendingHitCount);
                        nextReorg = 6 * nextReorg / 5 + 200;
                    }
                }
            }

            return result;
        }

        private bool Check([CanBeNull] IInputContext inputContext, [NotNull] Dependency d) {
            DependencyCheckResult result;

            if (_forbidden.Any(r => r.IsMatch(d))) {
                // First we check for forbidden - "if it is forbidden, it IS forbidden"
                result = DependencyCheckResult.Bad;
            } else if (_allowed.Any(r => r.IsMatch(d))) {
                // Then, we check for allwoed - "if it is not forbidden and allowed, then it IS allowed (and never questionable)"
                result = DependencyCheckResult.Ok;
            } else if (_questionable.Any(r => r.IsMatch(d))) {
                // Last, we check for questionable - "if it is questionable, it is questionable"
                result = DependencyCheckResult.Questionable;
            } else {
                // If no rule matches, it is bad!
                result = DependencyCheckResult.Bad;
            }

            if (result != DependencyCheckResult.Ok) {
                var ruleViolation = new RuleViolation(d, result);
                //Log.WriteViolation(ruleViolation);
                inputContext?.Add(ruleViolation);
            }

            d.AddCheckResult(result);

            return result != DependencyCheckResult.Bad;
        }

        [NotNull]
        public IEnumerable<DependencyRule> AllRules => _allowed.Concat(_forbidden).Concat(_questionable);
    }
}