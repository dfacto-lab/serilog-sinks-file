using Serilog.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Events;
using System.IO;

namespace Serilog.Tests
{
    public class FixedOutputFormatter : ITextFormatter
    {
        string _substitutionText;

        public FixedOutputFormatter(string substitutionText)
        {
            _substitutionText = substitutionText;
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            output.Write(_substitutionText);
        }
    }
}
