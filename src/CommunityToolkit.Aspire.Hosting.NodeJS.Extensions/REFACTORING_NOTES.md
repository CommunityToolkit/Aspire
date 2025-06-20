# Node.js Package Installer Refactoring

This refactoring transforms the Node.js package installers from lifecycle hooks to ExecutableResource-based resources, addressing issue #732.

## What Changed

### Before (Lifecycle Hook Approach)
- Package installation was handled by lifecycle hooks during `BeforeStartAsync`
- No visibility into installation progress in the dashboard
- Limited logging capabilities
- Process management handled manually via `Process.Start`

### After (Resource-Based Approach)
- Package installers are now proper `ExecutableResource` instances
- They appear as separate resources in the Aspire dashboard
- Full console output visibility and logging
- DCP (Distributed Application Control Plane) handles process management
- Parent-child relationships ensure proper startup ordering

## New Resource Classes

### NpmInstallerResource
```csharp
var installer = new NpmInstallerResource("npm-installer", "/path/to/project", useCI: true);
// Supports both 'npm install' and 'npm ci' commands
```

### YarnInstallerResource
```csharp
var installer = new YarnInstallerResource("yarn-installer", "/path/to/project");
// Executes 'yarn install' command
```

### PnpmInstallerResource
```csharp
var installer = new PnpmInstallerResource("pnpm-installer", "/path/to/project");
// Executes 'pnpm install' command
```

## Usage Examples

### Basic Usage (No API Changes)
```csharp
var builder = DistributedApplication.CreateBuilder();

// API remains the same - behavior is now resource-based
var viteApp = builder.AddViteApp("frontend", "./frontend")
    .WithNpmPackageInstallation(useCI: true);

var backendApp = builder.AddYarnApp("backend", "./backend")
    .WithYarnPackageInstallation();
```

### What Happens Under the Hood
```csharp
// This now creates:
// 1. NodeAppResource named "frontend" 
// 2. NpmInstallerResource named "frontend-npm-install" (child of frontend)
// 3. WaitAnnotation on frontend to wait for installer completion
// 4. ResourceRelationshipAnnotation linking installer to parent
```

## Benefits

### Dashboard Visibility
- Installer resources appear as separate items in the Aspire dashboard
- Real-time console output from package installation
- Clear status indication (starting, running, completed, failed)
- Ability to re-run installations if needed

### Better Resource Management
- DCP handles process lifecycle instead of manual `Process.Start`
- Proper resource cleanup and error handling
- Integration with Aspire's logging and monitoring systems

### Improved Startup Ordering
- Parent resources automatically wait for installer completion
- Failed installations prevent app startup (fail-fast behavior)
- Clear dependency visualization in the dashboard

### Development vs Production
- Installers only run during development (excluded from publish mode)
- No overhead in production deployments
- Maintains backward compatibility

## Migration Guide

### For Users
No changes required! The existing APIs (`WithNpmPackageInstallation`, `WithYarnPackageInstallation`, `WithPnpmPackageInstallation`) work exactly the same.

### For Contributors
The lifecycle hook classes are marked as `[Obsolete]` but remain functional for backward compatibility:
- `NpmPackageInstallerLifecycleHook`
- `YarnPackageInstallerLifecycleHook` 
- `PnpmPackageInstallerLifecycleHook`
- `NodePackageInstaller`

These will be removed in a future version once all usage has migrated to the resource-based approach.

## Testing

Comprehensive test coverage includes:
- Unit tests for installer resource properties and command generation
- Integration tests for parent-child relationships
- Cross-platform compatibility (Windows vs Unix commands)
- Publish mode exclusion verification
- Wait annotation and resource relationship validation