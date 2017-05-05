﻿using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rubberduck.Parsing.PreProcessing
{
    public sealed class TokenStreamLivelinessExpression : Expression
    {
        private readonly IExpression _isAlive;
        private readonly IExpression _code;
        private readonly IEnumerable<IToken> _tokens;

        public TokenStreamLivelinessExpression(IExpression isAlive, IExpression code, IEnumerable<IToken> tokens)
        {
            _isAlive = isAlive;
            _code = code;
            _tokens = tokens;
        }

        public override IValue Evaluate()
        {
            var isAlive = _isAlive.Evaluate().AsBool;
            var code = _code.Evaluate().AsString;
            if (!isAlive)
            {
                HideDeadTokens(_tokens);
            }
            return isAlive ? new StringValue(code) : new StringValue(MarkAsDead(code));
        }

        private void HideDeadTokens(IEnumerable<IToken> deadTokens)
        {
            foreach(var token in deadTokens)
            {
                //We need this cast because the IToken interface does not expose the setters for the properties.
                //CommonToken is the default token type used by Antlr. (Any custom token types should extend it.)
                var commonToken = token as CommonToken;
                if (commonToken != null)
                {
                    HideNonNewline(commonToken);
                }
            }
        }

        private void HideNonNewline(CommonToken token)
        {
            //We do not remove the newlines to keep the line counts intact.
            if (token.Type != Grammar.VBALexer.NEWLINE)
            {
                token.Channel = TokenConstants.HiddenChannel;
            }
        }

        private string MarkAsDead(string code)
        {
            var hasNewLine = code.EndsWith(Environment.NewLine);
            // Remove parsed new line.
            code = code.Substring(0, code.Length - Environment.NewLine.Length);
            var lines = code.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var result = string.Join(Environment.NewLine, lines.Select(_ => string.Empty));
            if (hasNewLine)
            {
                result += Environment.NewLine;
            }
            return result;
        }
    }
}
