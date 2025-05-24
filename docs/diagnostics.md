# .NET Aspire Community Toolkit Diagnostics

Some of the API's in the .NET Aspire Community Toolkit are decorated with the [`ExperimentalAttrible`](https://learn.microsoft.com/dotnet/api/system.diagnostics.codeanalysis.experimentalattribute). This attribute is used to indicate that an API is not ready for production use and is subject to change in the future.

## CTASPIRE001

There are cases where there is an API provided by the .NET Aspire Community Toolkit that is an intermidiary solution until a feature is added to .NET Aspire itself. This could be a private API that is not yet ready for public consumption, or a workaround waiting for the completion of a feature in .NET Aspire.

In these cases, refer to the `<remarks>` docs section of the API for more information on the feature in .NET Aspire and the issue(s) to track.

Once a release of .NET Aspire with that API is available, the API in the .NET Aspire Community Toolkit will be marked as obsolete and will be removed in a future release.

## CTASPIRE002

Support for loading extensions into SQLite requires either a NuGet package or folder path to the library to be provided, and as a result there is some custom logic to load the extension based on the path or NuGet package. This logic will require some experimenting to figure out edge cases, so the feature for extension loading will be kept as experimental until it is proven to be stable.

## CTASPIRE003

The API is marked for deprecation and will be removed in a future release.
