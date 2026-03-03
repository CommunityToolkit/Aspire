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

## How can I help?

I need help reviewing implementation of aspire underlying features, but mostly, I need you to try to run the projects
and see if the UX as it helps you install things is helpful at all, for example: 

- If you're a Windows user and you don't have Perl, does the windows-environment-setup.md help you install well enough?  
- If you run in the dev container, do you get yourself set up well enough after getting in there on Linux?
- Is there anything I should hold off on that is half baked?
- Is there anything you'd like to see expanded on before it goes live?

## What I would like to finish before release

- [x] Double checking that I can run the API with HTTPS and respect the cert trust
- [x] Seeing if I can get a worker service perl app to generate telemetry manually with traces. 
- [ ] Ensure Carton usage is directionally similar to how npm in the javascript and pip in the python integrations work.  Do they try to include the resources in the repo as well?
- [ ] Talking with some of the perl maintainers that made this possible.
- [x] Determine how to get make and gcc on PATH in windows, CPAN doesn't include the Strawberry distributed c\bin\gcc.exe and CPANM does, but make still has an issue, so both need to be alerted to the user. 

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Perl
```

### Example usage

Then, in the _AppHost.cs_ file of the `AppHost` project, define a Perl resource as follows:

```csharp
var perlScript = builder.AddPerlScript("perl-script", "ScriptName.pl", "ScriptPath");
```

The app might look something like this:

```perl
print "Hello world!\r\n";
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-perl

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

