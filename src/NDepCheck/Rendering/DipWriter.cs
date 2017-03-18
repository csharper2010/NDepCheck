﻿using System;
using System.Collections.Generic;
using System.IO;

namespace NDepCheck.Rendering {
    /// <summary>
    /// Writer for dependencies ("Edges") in standard "DIP" format
    /// </summary>
    public class DipWriter : IDependencyRenderer {


        public static void Write(IEnumerable<IEdge> edges, StreamWriter sw, bool withNotOkExampleInfo) {

                var writtenTypes = new HashSet<ItemType>();

                sw.WriteLine("// Written " + DateTime.Now);
                sw.WriteLine();
                foreach (var e in edges) {
                    WriteItemType(writtenTypes, e.UsingNode.Type, sw);
                    WriteItemType(writtenTypes, e.UsedNode.Type, sw);

                    sw.WriteLine(e.AsDipStringWithTypes(withNotOkExampleInfo));
                }            
        }

        private static void WriteItemType(HashSet<ItemType> writtenTypes, ItemType itemType, StreamWriter sw) {
            if (writtenTypes.Add(itemType)) {
                sw.Write("// ITEMTYPE ");
                sw.WriteLine(itemType.Name);
                sw.Write(itemType.Name);
                for (int i = 0; i < itemType.Keys.Length; i++) {
                    sw.Write(' ');
                    sw.Write(itemType.Keys[i]);
                    sw.Write(itemType.SubKeys[i]);
                }
                sw.WriteLine();
                sw.WriteLine();
            }
        }

        public void Render(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string argsAsString) {
            //int stringLengthForIllegalEdges = -1;
            string baseFilename = null;
            bool withNotOkExampleInfo = false;
            Options.Parse(argsAsString, arg => baseFilename = arg,
                //new OptionAction('e', (args, j) => {
                //    if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out stringLengthForIllegalEdges)) {
                //        Options.Throw("No valid length after e", args);
                //    }
                //    return j;
                //}), 
                new OptionAction('n', (args, j) => {
                    withNotOkExampleInfo = true;
                    return j;
                }), new OptionAction('o', (args, j) => {
                    baseFilename = Options.ExtractOptionValue(args, ref j);
                    return j;
                }));
            if (baseFilename == null) {
                Options.Throw("No filename set with option o", argsAsString);
            }
            string filename = Path.ChangeExtension(baseFilename, ".dip");

            using (var sw = new StreamWriter(filename)) {
                Write(dependencies, sw, withNotOkExampleInfo);
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, Stream output) {
            using (var sw = new StreamWriter(output)) {
                Write(dependencies, sw, true);
            }
        }

        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            ItemType amo = ItemType.New("AMO:Assembly:Module:Order");

            var bac = Item.New(amo, "BAC:BAC:0100".Split(':'));
            var kst = Item.New(amo, "KST:KST:0200".Split(':'));
            var kah = Item.New(amo, "KAH:KAH:0300".Split(':'));
            var kah_mi = Item.New(amo, "Kah.MI:KAH:0301".Split(':'));
            var vkf = Item.New(amo, "VKF:VKF:0400".Split(':'));

            items = new[] { bac, kst, kah, kah_mi, vkf};

            dependencies = new[] {
                    FromTo(kst, bac), FromTo(kst, kah_mi), FromTo(kah, bac), FromTo(vkf, bac), FromTo(vkf, kst), FromTo(vkf, kah, 3), FromTo(vkf, kah_mi, 2, 2)
                    // ... more to come
                };
        }

        private Dependency FromTo(Item from, Item to, int ct = 1, int questionable = 0) {
            return new Dependency(from, to, "Test", 0, 0, 0, 0, ct: ct, questionableCt: questionable);
        }

        public string GetHelp() {
            return 
@"  Writes dependencies to .dip files, which can be read in by NDepCheck.
  This is very helpful for building pipelines that process dependencies
  for different purposes.

  Options: [-n] -o filename | filename
    -n       ... each edge contains an example of a bad dependency
                 (if there is one); default: do not write bad example
    filename ... output file in .dip format";
        }
    }
}