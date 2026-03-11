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
use strict;
use warnings;

print "Hello world!\r\n";
```

## Detailed Usage Developer Guide

For a comprehensive walkthrough of the API — including `appDirectory` resolution, `WithLocalLib`, package management strategies, perlbrew configuration, example project layouts, and common pitfalls — see the [Using the Perl Hosting Integration](docs/using-perl-integration.md) guide.

## Additional Information

For more info visit <https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-perl>.

## Roadmap

See [Roadmap](docs/roadmap.md) for notes on planned work for the Perl integration.

## Feedback & contributing

Please see the main repo for contribution guidelines: <https://github.com/CommunityToolkit/Aspire>.

## Credits

There are many people to thank, but the work of JJAtria in making the OpenTelemetry::SDK module is what makes this integration feel great in Aspire and without it, I don't know that I would have even attempted to create it.  

Thanks also to the Aspire Discord community at large for all the assistance when I had questions about the internals of Aspire.

### Referenced Libraries

This integration references or interacts with the following Perl ecosystem libraries and tools, while the libraries themselves are only installed by individual developers for their projects, I do use them as examples and want to give credit and note their licensing for posterity:

| Resource | Website / Repository | License |
| ---------- | --------------------- | --------- |
| Perl | [perl.org](https://www.perl.org) | [Artistic / GPL](https://github.com/Perl/perl5/blob/blead/Artistic) |
| Strawberry Perl | [strawberryperl.com](https://strawberryperl.com) | [Artistic / GPL](https://github.com/Perl/perl5/blob/blead/Artistic) |
| perlbrew | [perlbrew.pl](https://perlbrew.pl) | [MIT](https://github.com/gugod/App-perlbrew/blob/develop/LICENSE) |
| Berrybrew | [GitHub](https://github.com/stevieb9/berrybrew) | [License](https://github.com/stevieb9/berrybrew?tab=License-1-ov-file#readme) |
| App::cpanminus (cpanm) | [GitHub](https://github.com/miyagawa/cpanminus) | [License](https://metacpan.org/pod/App::cpanminus#LICENSE) |
| Carton | [GitHub](https://github.com/miyagawa/carton) | [License](https://metacpan.org/pod/Carton#LICENSE) |
| local::lib | [metacpan](https://metacpan.org/pod/local::lib) | [License](https://metacpan.org/pod/local::lib#LICENSE) |
| Mojolicious | [mojolicious.org](https://mojolicious.org) | [Artistic-2.0](https://github.com/mojolicious/mojo/blob/main/LICENSE) |
| OpenTelemetry::SDK | [GitHub](https://github.com/jjatria/perl-opentelemetry) | [License](https://github.com/jjatria/perl-opentelemetry?tab=License-1-ov-file#readme) |
| IO::Socket::SSL | [metacpan](https://metacpan.org/pod/IO::Socket::SSL) | [License](https://metacpan.org/pod/IO::Socket::SSL#COPYRIGHT) |
| LWP::UserAgent | [metacpan](https://metacpan.org/pod/LWP::UserAgent) | [License](https://metacpan.org/pod/LWP::UserAgent#COPYRIGHT-AND-LICENSE) |
| Google::ProtocolBuffers::Dynamic | [metacpan](https://metacpan.org/pod/Google::ProtocolBuffers::Dynamic) | [License](https://metacpan.org/pod/Google::ProtocolBuffers::Dynamic#COPYRIGHT-AND-LICENSE) |
