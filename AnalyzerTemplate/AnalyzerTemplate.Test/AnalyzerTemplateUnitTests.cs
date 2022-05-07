using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = AnalyzerTemplate.Test.CSharpCodeFixVerifier<
    AnalyzerTemplate.AnalyzerTemplateAnalyzer,
    AnalyzerTemplate.AnalyzerTemplateCodeFixProvider>;

namespace AnalyzerTemplate.Test
{
    [TestClass]
    public class AnalyzerTemplateUnitTest
    {
        [TestMethod]
        public async Task TestMethod()
        {
            var test = @"
namespace notClassLibrary1
{
    public class notClass1
    {
        void notMethod(bool flag, int value)
        {
            bool {|#0:notAvailable|} = false;

            if (notAvailable)
            {

            }
        }
    }
}";

            var fixtest = @"
namespace notClassLibrary1
{
    public class notClass1
    {
        void notMethod(bool flag, int value)
        {
            bool Available = false;

            if (!Available)
            {

            }
        }
    }
}";

            var expected = VerifyCS.Diagnostic("AnalyzerTemplate").WithLocation(0);
            //Console.WriteLine(expected.);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }
    }
}
