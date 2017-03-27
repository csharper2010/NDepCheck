using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Rendering {
    /// <summary>
    /// Class that creates AT&amp;T DOT (graphviz) output from dependencies - see <a href="http://graphviz.org/">http://graphviz.org/</a>.
    /// </summary>
    public class DotRenderer : IDependencyRenderer {
        public void Render(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, string argsAsString, string baseFilename) {
            new GenericDotRenderer().Render(items, dependencies, argsAsString, baseFilename);
        }

        public void RenderToStreamForUnitTests(IEnumerable<Item> items, IEnumerable<Dependency> dependencies, Stream output) {
            new GenericDotRenderer().RenderToStreamForUnitTests(items, dependencies, output);
        }

        public void CreateSomeTestItems(out IEnumerable<Item> items, out IEnumerable<Dependency> dependencies) {
            SomeRendererTestData.CreateSomeTestItems(out items, out dependencies);
        }

        public string GetHelp(bool detailedHelp) {
            return new GenericDotRenderer().GetHelp();
        }
    }

    public class GenericDotRenderer : IRenderer<INode, IEdge> {
        private void Render(/*IEnumerable<INode> nodes, */IEnumerable<IEdge> edges, [NotNull] TextWriter output, int? stringLengthForIllegalEdges) {
            IEnumerable<IEdge> visibleEdges = edges.Where(e => !e.Hidden);

            IDictionary<INode, IEnumerable<IEdge>> nodesAndEdges = Dependency.Edges2NodesAndEdges(visibleEdges);

            output.WriteLine("digraph D {");
            output.WriteLine("ranksep = 1.5;");

            foreach (var n in nodesAndEdges.Keys.OrderBy(n => n.Name)) {
                output.WriteLine("\"" + n.Name + "\" [shape=" + (n.IsInner ? "box,style=bold" : "oval") + "];");
            }

            output.WriteLine();

            foreach (var n in nodesAndEdges.Keys.OrderBy(n => n.Name)) {
                foreach (var e in nodesAndEdges[n].Where(e => e.UsingNode.IsInner || e.UsedNode.IsInner)) {
                    output.WriteLine(e.GetDotRepresentation(stringLengthForIllegalEdges));
                }
            }

            output.WriteLine("}");
        }

        public void Render(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, string argsAsString, [CanBeNull] string baseFilename) {
            int stringLengthForIllegalEdges = -1;
            Options.Parse(argsAsString,
                new OptionAction('e', (args, j) => {
                    if (!int.TryParse(Options.ExtractOptionValue(args, ref j), out stringLengthForIllegalEdges)) {
                        Options.Throw("No valid length after e", args);
                    }
                    return j;
                }));
            string filename = Path.ChangeExtension(baseFilename, ".dot");

            using (var sw = GlobalContext.CreateTextWriter(filename)) {
                Render(dependencies, sw, stringLengthForIllegalEdges);
            }
        }

        public void RenderToStreamForUnitTests(IEnumerable<INode> items, IEnumerable<IEdge> dependencies, Stream stream) {
            using (var sw = new StreamWriter(stream)) {
                Render(dependencies, sw, null);
            }
        }

        public string GetHelp() {
            return
@"  Writes dependencies to file in .dot format (graphviz; see http://graphviz.org/).
  This is helpful for smaller dependency graphs without any programming.
  For larger graphs, it is better to use or define a renderer that creates a
  specific structure, e.g., a ModulesAndInterfacesRenderer.

  Options: [-e #] -o filename | filename
    -e #          cutoff length of text for wrong dependencies; default: no cutoff
    filename      output filename in .dot (graphviz) format";
        }
    }
}