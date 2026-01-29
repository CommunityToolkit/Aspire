# Aspire Community Toolkit Diagnostics

Some of the API's in the Aspire Community Toolkit are decorated with the [`ExperimentalAttrible`](https://learn.microsoft.com/dotnet/api/system.diagnostics.codeanalysis.experimentalattribute). This attribute is used to indicate that an API is not ready for production use and is subject to change in the future.

## CTASPIRE001

There are cases where there is an API provided by the Aspire Community Toolkit that is an intermidiary solution until a feature is added to Aspire itself. This could be a private API that is not yet ready for public consumption, or a workaround waiting for the completion of a feature in Aspire.

In these cases, refer to the `<remarks>` docs section of the API for more information on the feature in Aspire and the issue(s) to track.

Once a release of Aspire with that API is available, the API in the Aspire Community Toolkit will be marked as obsolete and will be removed in a future release.

## CTASPIRE002

This API is marked as experimental as it does not have a stable implementation, meaning the underlying API is not stable or the test coverage is not consistent in passing. No guarantees are made regarding its functionality or stability.

## CTASPIRE003

The API is marked for deprecation and will be removed in a future release.
