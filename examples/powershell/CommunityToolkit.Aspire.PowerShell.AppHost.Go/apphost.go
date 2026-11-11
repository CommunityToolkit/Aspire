package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	blob := builder.AddConnectionString("myblob")
	check(blob.Err())
	powershell := builder.AddPowerShell("ps").WaitFor(blob)
	check(powershell.Err())

	script1 := powershell.AddScript("script1",
		"\nparam($name)\n"+
			"write-information \"Hello, $name\"\n"+
			"write-information \"`$myblob is $myblob\"\n")
	script1.WithReference(blob).WithArgs([]string{"world"})
	check(script1.Err())

	script2 := powershell.AddScript("script2", `
write-information "Getting there..."
write-information "the sum of 2 and 3 is $([int]$args[0] + [int]$args[1])"
`)
	script2.WithArgs([]string{"2", "3"}).WaitForCompletion(script1)
	check(script2.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
