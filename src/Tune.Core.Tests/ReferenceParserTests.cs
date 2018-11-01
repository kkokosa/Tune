using Tune.Core.References;
using Xunit;

namespace Tune.Core.Tests
{
    public class ReferenceParserTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("//System.Core")]
        [InlineData("System.Core")]
        [InlineData("using System.Core")]
        public void Input_WithoutMetaTag_Fails(string invalidInput)
        {
            var result = ReferenceParser.TryParse(invalidInput, out string reference);
            Assert.False(result);
            Assert.Equal(string.Empty, reference);
        }

        [Fact]
        public void Correct_Input_Success()
        {
            string input = "//#r System.Core  ";

            var result = ReferenceParser.TryParse(input, out string reference);

            Assert.True(result);
            Assert.Equal("System.Core", reference);
        }
    }
}
