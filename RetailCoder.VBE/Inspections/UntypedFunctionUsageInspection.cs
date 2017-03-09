using System.Collections.Generic;
using System.Linq;
using Rubberduck.Inspections.Abstract;
using Rubberduck.Inspections.Results;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Inspections.Abstract;
using Rubberduck.Parsing.Inspections.Resources;
using Rubberduck.Parsing.VBA;

namespace Rubberduck.Inspections
{
    public sealed class UntypedFunctionUsageInspection : InspectionBase
    {
        public UntypedFunctionUsageInspection(RubberduckParserState state)
            : base(state, CodeInspectionSeverity.Hint)
        {
        }

        public override CodeInspectionType InspectionType { get { return CodeInspectionType.LanguageOpportunities; } }

        private readonly string[] _tokens = {
            Tokens.Error,
            Tokens.Hex,
            Tokens.Oct,
            Tokens.Str,
            Tokens.CurDir,
            Tokens.Command,
            Tokens.Environ,
            Tokens.Chr,
            Tokens.ChrW,
            Tokens.Format,
            Tokens.LCase,
            Tokens.Left,
            Tokens.LeftB,
            Tokens.LTrim,
            Tokens.Mid,
            Tokens.MidB,
            Tokens.Trim,
            Tokens.Right,
            Tokens.RightB,
            Tokens.RTrim,
            Tokens.UCase
        };

        public override IEnumerable<IInspectionResult> GetInspectionResults()
        {
            var declarations = BuiltInDeclarations
                .Where(item =>
                        _tokens.Any(token => item.IdentifierName == token || item.IdentifierName == "_B_var_" + token) &&
                        item.Scope.StartsWith("VBE7.DLL;"));

            return declarations.SelectMany(declaration => declaration.References
                .Where(item => _tokens.Contains(item.IdentifierName) &&
                               !IsIgnoringInspectionResultFor(item, AnnotationName))
                .Select(item => new UntypedFunctionUsageInspectionResult(this, item)));
        }
    }
}
