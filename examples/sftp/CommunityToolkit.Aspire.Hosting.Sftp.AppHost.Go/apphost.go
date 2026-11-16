package main

import (
	"apphost/modules/aspire"
	"log"
	"os"
	"path/filepath"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)
	directory, err := builder.AppHostDirectory()
	check(err)
	fixtures := filepath.Join(directory, "..", "CommunityToolkit.Aspire.Hosting.Sftp.AppHost")

	sftp := builder.AddSftp("sftp", &aspire.AddSftpOptions{Port: aspire.Float64Ptr(2222)})
	if os.Getenv("ASPIRE_RUNTIME_SMOKE") == "1" {
		sftp.WithEnvironment("SFTP_USERS", "foo:pass:::upload")
	} else {
		sftp.WithUsersFile(filepath.Join(fixtures, "etc", "sftp", "users.conf"))
		sftp.WithHostKeyFile(filepath.Join(fixtures, "etc", "ssh", "ssh_host_ed25519_key"), aspire.KeyTypeEd25519)
		sftp.WithUserKeyFile("foo", filepath.Join(fixtures, "home", "foo", ".ssh", "keys", "id_ed25519.pub"), aspire.KeyTypeEd25519)
	}
	defaults := builder.AddSftp("sftp-defaults")
	defaults.WithEnvironment("SFTP_USERS", "bar:pass:::upload")

	for _, resource := range []aspire.SftpContainerResource{sftp, defaults} {
		check(resource.Host().Err())
		check(resource.Port().Err())
		_ = resource.UriExpression()
		_ = resource.ConnectionStringExpression()
		check(resource.Err())
	}

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
