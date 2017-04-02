// (c) HMM�ller 2006...2015

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NDepCheck.Transforming.ViolationChecking;

namespace NDepCheck.Tests {
    [TestClass]
    public class AttributeTests {
        private static readonly string _testAssemblyPath = Path.Combine(Path.GetDirectoryName(typeof(MainTests).Assembly.Location ?? "IGNORE"), "NDepCheck.TestAssemblyForAttributes.dll");

        [TestMethod]
        public void Exit0() {
            {
                string ruleFile = CreateTempDotNetDepFileName();
                using (TextWriter tw = new StreamWriter(ruleFile)) {
                    tw.Write(@"
                    $ DOTNETCALL ---> DOTNETCALL

                    NDepCheck.TestAssembly.For.Attributes.** ---> NDepCheck.TestAssembly.For.Attributes.**
                    NDepCheck.TestAssembly.For.Attributes ---> System.**

                    $ MY_ITEM_TYPE(NAMESPACE:CLASS:ASSEMBLY.NAME:ASSEMBLY.VERSION:ASSEMBLY.CULTURE:MEMBER.NAME:MEMBER.SORT:CUSTOM.SectionA:CUSTOM.SectionB:CUSTOM.SectionC) ---> DOTNETCALL
                    // VORERST ------------------------------                    
                    : ---> :

                    $ MY_ITEM_TYPE ---> MY_ITEM_TYPE
                    // VORERST ------------------------------                    
                    : ---> :

                    $ DOTNETREF ---> DOTNETREF
                    * ---> *
                ");
                }
                Assert.AreEqual(0, Program.Main(
                    new[] { "-f", typeof(CheckDeps).Name, "{", "-f", ruleFile, "}", _testAssemblyPath }
                ));
                File.Delete(ruleFile);
            }
        }

        private static string CreateTempDotNetDepFileName() {
            return Path.GetTempFileName() + ".dll.dep";
        }
    }
}
