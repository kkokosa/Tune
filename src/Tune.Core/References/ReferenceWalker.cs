using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

namespace Tune.Core.References
{
    class ReferenceWalker : CSharpSyntaxWalker
    {
        public HashSet<string> References = new HashSet<string>();

        public ReferenceWalker()
            : base(SyntaxWalkerDepth.StructuredTrivia)
        {
        }

        public override void VisitTrivia(SyntaxTrivia trivia)
        {
            if (trivia.Kind() == SyntaxKind.SingleLineCommentTrivia)
            {
                if (ReferenceParser.TryParse(trivia.ToString(), out string reference))
                {
                    References.Add(reference);
                }
            }

            base.VisitTrivia(trivia);
        }
    }
}
