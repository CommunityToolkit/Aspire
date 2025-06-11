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

    # only run this if Azure CLI is installed
    if ((gcm az -ErrorAction SilentlyContinue) -ne $null) {

        az storage container create --connection-string $myblob -n demo
        az storage blob upload --connection-string $myblob -c demo --file ./scripts/script.ps1
        write-information "Blob uploaded"

    } else {

        write-warning "Azure CLI not found, skipping blob upload"

    }
    write-information $pwd

    
""").WithArgs("world");

// outputs "the sum of 2 and 3 is 5"
var script2 = ps.AddScript("script2", """
    & ./scripts/script.ps1 @args
    """)
    .WithArgs(2, 3)
    .WaitForCompletion(script1);

builder.Build().Run();

