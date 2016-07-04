using System;
using System.Collections.Generic;
using Serilog.Events;
using Xunit.Sdk;

namespace Serilog.Tests.Support
{
    static class Some
    {
        public static LogEvent LogEvent(string messageTemplate, params object[] propertyValues)
        {
            var log = new LoggerConfiguration().CreateLogger();
            MessageTemplate template;
            IEnumerable<LogEventProperty> properties;
#pragma warning disable Serilog004 // Constant MessageTemplate verifier
            if (!log.BindMessageTemplate(messageTemplate, propertyValues, out template, out properties))
#pragma warning restore Serilog004 // Constant MessageTemplate verifier
            {
                throw new XunitException("Template could not be bound.");
            }
            return new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null, template, properties);
        }
    }
}
