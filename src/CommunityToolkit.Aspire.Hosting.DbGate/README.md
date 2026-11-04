This package is designed to be used internally by the community toolkit and is not intended to be used directly in the application code.

## Example compatibility

The [DbGate example AppHost](../../examples/dbgate/CommunityToolkit.Aspire.Hosting.DbGate.AppHost/AppHost.cs)
uses the `mongo:7.0` release line for its two MongoDB resources. MongoDB 8.x can
refuse to start on Linux kernels 6.19 and later because of an incompatibility
between the kernel and its memory allocator
([SERVER-121912](https://jira.mongodb.org/browse/SERVER-121912)).
MongoDB's [compatibility matrix](https://jira.mongodb.org/browse/SERVER-125742)
lists MongoDB 7.0 as unaffected; it is
[supported until August 31, 2027](https://www.mongodb.com/legal/support-policy/lifecycles).
The release-line tag allows maintenance updates without changing the example.

This override is local to the example, not a change to `WithDbGate()` or Aspire's
MongoDB defaults. Revisit it when CI runner kernels and the default MongoDB image
are compatible. It does not describe an in-place downgrade path for existing data.
