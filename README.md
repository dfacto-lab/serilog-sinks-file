# Serilog.Sinks.File

The file sink for Serilog.

[![Build status](https://ci.appveyor.com/api/projects/status/hh9gymy0n6tne46j?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-file) [![NuGet Version](http://img.shields.io/nuget/v/Serilog.Sinks.File.svg?style=flat)](https://www.nuget.org/packages/Serilog.Sinks.File/)


Writes log events to a text file.

```csharp
var log = new LoggerConfiguration()
    .WriteTo.File("log.txt")
    .CreateLogger();
```

To avoid sinking apps with runaway disk usage the file sink **limits file size to 1GB by default**. The limit can be increased or removed using the `fileSizeLimitBytes` parameter.

```csharp
    .WriteTo.File("log.txt", fileSizeLimitBytes: null)
```

Or in XML [app-settings format](https://github.com/serilog/serilog/wiki/AppSettings):

```xml
<add key="serilog:write-to:File.path" value="log.txt" />
```

> **Important:** Only one process may write to a log file at a given time. For multi-process scenarios, either use separate files or one of the non-file-based sinks.

* [Documentation](https://github.com/serilog/serilog/wiki)

Copyright &copy; 2016 Serilog Contributors - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html).
