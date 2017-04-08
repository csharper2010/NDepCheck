﻿using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using NDepCheck.Transforming.SpecialNodeMarking;

namespace NDepCheck.Tests {
    [TestClass]
    public class TestMarkSpecialItems {
        private static IEnumerable<Item> Run(string options, string mark) {
            var globalContext = new GlobalContext();
            try {
                var msi = new MarkSpecialItems();
                var result = new List<Dependency>();
                msi.Transform(globalContext, "test", msi.GetTestDependencies(), options, "test", result);
                return
                    result.SelectMany(d => new[] {d.UsingItem, d.UsedItem})
                        .Distinct()
                        .Where(i => i.Markers.Any(m => m == mark));
            } finally {
                // Also static caches must be reset, as "Mark" modifies Items
                globalContext.ResetAll();
            }
        }

        private static void AssertResult(IEnumerable<Item> result, params char[] nameStarts) {
            CollectionAssert.AreEquivalent(nameStarts, result.Select(i => i.Name[0]).ToArray());
        }

        [TestMethod]
        public void TestMarkSources() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run($"{{ {MarkSpecialItems.MarkSourcesOption} " +
                                           $"{MarkSpecialItems.MarkerToAddOption} {mark} }}", mark);
            AssertResult(result, 'A');
        }

        [TestMethod]
        public void TestMarkSourcesRecursively() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run($"{{ {MarkSpecialItems.MarkSourcesOption} " +
                                           $"{MarkSpecialItems.RecursiveMarkOption} " +
                                           $"{MarkSpecialItems.MarkerToAddOption} {mark} }}", mark);
            AssertResult(result, 'A', 'B');
        }

        [TestMethod]
        public void TestMarkSinks() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run($"{{ {MarkSpecialItems.MarkSinksOption} " +
                                           $"{MarkSpecialItems.MarkerToAddOption} {mark} }}", mark);
            AssertResult(result, 'I', 'J');
        }

        [TestMethod]
        public void TestMarkSinksRecursively() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run($"{{ {MarkSpecialItems.MarkSinksOption} " +
                                           $"{MarkSpecialItems.RecursiveMarkOption} " +
                                           $"{MarkSpecialItems.MarkerToAddOption} {mark} }}", mark);
            AssertResult(result, 'F', 'G', 'H', 'I', 'J');
        }

        [TestMethod]
        public void TestMarkSinksRecursivelyIgnoringSelfCycles() {
            const string mark = "MARK";
            IEnumerable<Item> result = Run($"{{ {MarkSpecialItems.MarkSinksOption} " +
                                           $"{MarkSpecialItems.RecursiveMarkOption} " +
                                           $"{MarkSpecialItems.IgnoreSingleCyclesOption} " +
                                           $"{MarkSpecialItems.MarkerToAddOption} {mark} }}", mark);
            AssertResult(result, 'E', 'F', 'G', 'H', 'I', 'J');
        }
    }
}