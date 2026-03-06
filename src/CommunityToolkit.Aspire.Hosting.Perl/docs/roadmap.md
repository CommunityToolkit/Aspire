# Perl Integration Roadmap

## Roadmap

- Package Management Consolidation
- Berrybrew Support for Windows
- Debugging Support
- Container Support
- Dockerfile Support
- Incorporating any feedback

### Package management consolidation

Explore the consolidation of package manager configuration into a single API shape.

The current implementation may be fine, but it requires intellisense hunting to know how to proceed, particularly for folks with less Perl experience (what's a CpanMinus?).

- Consolidate `.WithCpanMinus(...)`, `.WithCarton(...)`, and related package manager configuration into `.WithPackageManager(ENUM)`.
- Keep package manager selection explicit and easier to discover.
- Reduce overlap between package-manager-specific entry points.

### Berrybrew support on Windows

Perlbrew is included already, because a lot of linux users will have a System Perl install that they want to shield from modification, but Perlbrew is not compatible on Windows.  I will explore Berrybrew-based runtime support for Windows in a future release.

### Debugging support

Add first-class debugging support for Perl applications launched through the integration.  Aspire team is currently working on this.  Keep an eye out for 13.3 or (hopefully) soon after.

### Container support

Add the ability to make a ContainerResource with your perl app (like a perl worker service) or api.

### Incorporating Feedback

I'll be watching for any feedback and trying to incorporate it as I go.  Please submit an issue if you have any suggestions.
