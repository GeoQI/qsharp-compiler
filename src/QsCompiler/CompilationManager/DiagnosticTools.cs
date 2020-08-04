﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Quantum.QsCompiler.DataTypes;
using Microsoft.Quantum.QsCompiler.Diagnostics;
using Microsoft.Quantum.QsCompiler.SyntaxTree;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Lsp = Microsoft.VisualStudio.LanguageServer.Protocol;
using Position = Microsoft.Quantum.QsCompiler.DataTypes.Position;
using Range = Microsoft.Quantum.QsCompiler.DataTypes.Range;

namespace Microsoft.Quantum.QsCompiler.CompilationBuilder
{
    public static class DiagnosticTools
    {
        /// <summary>
        /// Given the location information for a declared symbol,
        /// as well as the position of the declaration within which the symbol is declared,
        /// returns the zero-based line and character index indicating the position of the symbol in the file.
        /// Returns null if the given object is not compatible with the position information generated by this CompilationBuilder.
        /// </summary>
        public static Position SymbolPosition(QsLocation rootLocation, QsNullable<Position> symbolPosition, Range symbolRange)
        {
            // the position offset is set to null (only) for variables defined in the declaration
            var offset = symbolPosition.IsNull ? rootLocation.Offset : rootLocation.Offset + symbolPosition.Item;
            return offset + symbolRange.Start;
        }

        /// <summary>
        /// Returns a new Diagnostic, making a deep copy of the given one (in particular a deep copy of it's Range)
        /// or null if the given Diagnostic is null.
        /// </summary>
        public static Diagnostic Copy(this Diagnostic message)
        {
            Lsp.Position CopyPosition(Lsp.Position position) =>
                position is null ? null : new Lsp.Position(position.Line, position.Character);

            Lsp.Range CopyRange(Lsp.Range range) =>
                range is null
                    ? null
                    : new Lsp.Range
                    {
                        Start = CopyPosition(range.Start),
                        End = CopyPosition(range.End)
                    };

            return message is null
                ? null
                : new Diagnostic
                {
                    Range = CopyRange(message.Range),
                    Severity = message.Severity,
                    Code = message.Code,
                    Source = message.Source,
                    Message = message.Message
                };
        }

        /// <summary>
        /// Translates the line numbers in the diagnostic by the given offset.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the new diagnostic range is invalid.</exception>
        public static QsCompilerDiagnostic TranslateLines(this QsCompilerDiagnostic diagnostic, int offset) =>
            new QsCompilerDiagnostic(
                diagnostic.Diagnostic,
                diagnostic.Arguments,
                diagnostic.Range.TranslateLines(offset),
                diagnostic.Source);

        /// <summary>
        /// Returns a function that returns true if the ErrorType of the given Diagnostic is one of the given types.
        /// </summary>
        public static Func<QsCompilerDiagnostic, bool> ErrorType(params ErrorCode[] codes) => diagnostic =>
            diagnostic.Diagnostic is DiagnosticItem.Error error && codes.Contains(error.Item);

        /// <summary>
        /// Returns a function that returns true if the WarningType of the given Diagnostic is one of the given types.
        /// </summary>
        public static Func<Diagnostic, bool> WarningType(params WarningCode[] types)
        {
            var codes = types.Select(warn => warn.Code());
            return m => m.IsWarning() && codes.Contains(m.Code);
        }

        /// <summary>
        /// Returns true if the given diagnostics is an error.
        /// </summary>
        public static bool IsError(this QsCompilerDiagnostic m) => m.Diagnostic.IsError;

        /// <summary>
        /// Returns true if the given diagnostics is a warning.
        /// </summary>
        public static bool IsWarning(this Diagnostic m) =>
            m.Severity == DiagnosticSeverity.Warning;

        /// <summary>
        /// Returns true if the given diagnostics is an information.
        /// </summary>
        public static bool IsInformation(this Diagnostic m) =>
            m.Severity == DiagnosticSeverity.Information;

        /// <summary>
        /// Extracts all elements satisfying the given condition and which start at a line that is larger or equal to lowerBound.
        /// Diagnostics without any range information are only extracted if no lower bound is specified or the specified lower bound is smaller than zero.
        /// Throws an ArgumentNullException if the given condition is null.
        /// </summary>
        public static IEnumerable<Diagnostic> Filter(this IEnumerable<Diagnostic> orig, Func<Diagnostic, bool> condition, int lowerBound = -1)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }
            return orig?.Where(m => condition(m) && lowerBound <= (m.Range?.Start?.Line ?? -1));
        }

        /// <summary>
        /// Extracts all elements satisfying the given condition and which start at a line that is larger or equal to lowerBound and smaller than upperBound.
        /// Throws an ArgumentNullException if the given condition is null.
        /// </summary>
        public static IEnumerable<Diagnostic> Filter(this IEnumerable<Diagnostic> orig, Func<Diagnostic, bool> condition, int lowerBound, int upperBound)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }
            return orig?.Where(m => condition(m) && lowerBound <= m.Range.Start.Line && m.Range.End.Line < upperBound);
        }

        /// <summary>
        /// Extracts all elements which start at a line that is larger or equal to lowerBound.
        /// </summary>
        public static IEnumerable<Diagnostic> Filter(this IEnumerable<Diagnostic> orig, int lowerBound)
        {
            return orig?.Filter(m => true, lowerBound);
        }

        /// <summary>
        /// Extracts all elements which start at a line that is larger or equal to lowerBound and smaller than upperBound.
        /// </summary>
        public static IEnumerable<Diagnostic> Filter(this IEnumerable<Diagnostic> orig, int lowerBound, int upperBound)
        {
            return orig?.Filter(m => true, lowerBound, upperBound);
        }

        /// <summary>
        /// Returns true if the start line of the given diagnostic is larger or equal to lowerBound.
        /// </summary>
        internal static bool SelectByStartLine(this QsCompilerDiagnostic m, int lowerBound) =>
            lowerBound <= m.Range.Start.Line;

        /// <summary>
        /// Returns true if the start line of the given diagnostic is larger or equal to lowerBound, and smaller than upperBound.
        /// </summary>
        internal static bool SelectByStartLine(this QsCompilerDiagnostic m, int lowerBound, int upperBound) =>
            lowerBound <= m.Range.Start.Line && m.Range.Start.Line < upperBound;

        /// <summary>
        /// Returns true if the end line of the given diagnostic is larger or equal to lowerBound, and smaller than upperBound.
        /// </summary>
        internal static bool SelectByEndLine(this QsCompilerDiagnostic m, int lowerBound, int upperBound) =>
            lowerBound <= m.Range.End.Line && m.Range.End.Line < upperBound;

        /// <summary>
        /// Returns true if the start position of the given diagnostic is larger or equal to lowerBound.
        /// </summary>
        internal static bool SelectByStart(this QsCompilerDiagnostic m, Position lowerBound) =>
            lowerBound <= m.Range.Start;

        /// <summary>
        /// Returns true if the start position of the diagnostic is contained in the range.
        /// </summary>
        internal static bool SelectByStart(this QsCompilerDiagnostic m, Range range) =>
            range.Contains(m.Range.Start);

        /// <summary>
        /// Returns true if the end position of the given diagnostic is larger or equal to lowerBound.
        /// </summary>
        internal static bool SelectByEnd(this QsCompilerDiagnostic m, Position lowerBound) =>
            lowerBound <= m.Range.End;

        /// <summary>
        /// Returns true if the end position of the diagnostic is contained in the range.
        /// </summary>
        internal static bool SelectByEnd(this QsCompilerDiagnostic m, Range range) =>
            range.Contains(m.Range.End);
    }
}
