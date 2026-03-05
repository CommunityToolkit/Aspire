# CommunityToolkit.Aspire.Hosting.Perl library

Provides extensions methods and resource definitions for the .NET Aspire AppHost to support running Perl scripts.

# Welcome Camel Curious Individual

Please have a look around.  

If you're here to give feedback, I hope to have made it easy for you.  I have a series of examples available to run.

I highly recommend you just use the dev container, and then ensure you can run the following:

- cpanm --version
- carton --version
- aspire --version
- aspire update --self (then select staging)

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

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

### Credits

There are many people to thank, but the work of JJAtria in making the OpenTelemetry::SDK module is what makes this integration feel great.