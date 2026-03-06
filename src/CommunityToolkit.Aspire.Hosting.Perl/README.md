# CommunityToolkit.Aspire.Hosting.Perl library

Provides extensions methods and resource definitions for the .NET Aspire AppHost to support running Perl.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Perl
```

### Example usage

Then, in the _AppHost.cs_ file of the `AppHost` project, define a Perl resource as follows:

```csharp
var perlScript = builder.AddPerlScript("perl-script", "ScriptPath", "ScriptName.pl");
```

The app might look something like this:

```perl
print "Hello world!\r\n";
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-perl

## Roadmap

See `docs/roadmap.md` for notes on planned work for the Perl integration.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

### Credits

There are many people to thank, but the work of JJAtria in making the OpenTelemetry::SDK module is what makes this integration feel great in Aspire and without it, I don't know that I would have even attempted to create it.  

Thanks also to the Aspire Discord community at large for all the assistance when I had questions about the internals of Aspire.