# CommunityToolkit Aspire PowerShell Scripting

## About

Script your resources, use the pwsh (powershell core) engine and reference connectionstring expressions, live resources, dotnet instances or whatever else is in scope for your AppHost. 


```csharp
using CommunityToolkit.Aspire.Hosting.PowerShell;

var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blob = storage.AddBlobs("myblob");

var ps = builder.AddPowerShell("ps")
    .WithReference(blob)
    .WaitFor(storage);

// uploads the script in scripts/
var script1 = ps.AddScript("script1", """
    param($name)

    write-information "Hello, $name"

    # uncommenting this will hang the script if you don't attach the pwsh debugger
    # wait-debugger

    write-information "`$myblob is $myblob"

    az storage container create --connection-string $myblob -n demo
    az storage blob upload --connection-string $myblob -c demo --file ./scripts/script.ps1
    
    write-information $pwd

    write-information "Blob uploaded"
""").WithArgs("world");

// outputs "the sum of 2 and 3 is 5"
var script2 = ps.AddScript("script2", """
    & ./scripts/script.ps1 @args
    """)
    .WithArgs(2, 3)
    .WaitForCompletion(script1);

builder.Build().Run();

```

## Debugging

While your Apphost is running a script that is waiting via `Wait-Debugger`, open a terminal with powershell (pwsh) 7.4 or later (win, osx, linux) and use `Get-PSHostProcessInfo`, `Enter-PSHostProcess`, `Get-Runspace` and `Debug-Runspace` to connect the debugger. 

See https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/enter-pshostprocess?view=powershell-7.5 for more information.
