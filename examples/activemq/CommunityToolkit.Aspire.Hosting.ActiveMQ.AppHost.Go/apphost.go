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

	bindMountRoot, err := os.MkdirTemp("", "activemq-polyglot-")
	check(err)
	artemisDataPath := filepath.Join(bindMountRoot, "artemis-data")
	artemisConfPath := filepath.Join(bindMountRoot, "artemis-conf")
	check(os.MkdirAll(artemisDataPath, 0755))
	check(os.MkdirAll(artemisConfPath, 0755))

	password := builder.AddParameter("mq-password", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("admin"), Secret: aspire.BoolPtr(true),
	})
	user := builder.AddParameter("mq-user", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("admin"), PublishValueAsDefault: aspire.BoolPtr(true),
	})
	check(password.Err())
	check(user.Err())

	classic := builder.AddActiveMQ("classic", &aspire.AddActiveMQOptions{
		UserName: &user, Password: &password, Port: aspire.Float64Ptr(36161),
		Scheme: aspire.StringPtr("activemq"), WebPort: aspire.Float64Ptr(38161),
	}).WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("classic-data")}).
		WithConfVolume(&aspire.WithConfVolumeOptions{Name: aspire.StringPtr("classic-conf")})
	classic2 := builder.AddActiveMQ("classic2", &aspire.AddActiveMQOptions{
		UserName: &user, Password: &password,
	})
	artemis := builder.AddActiveMQArtemis("artemis", &aspire.AddActiveMQArtemisOptions{
		UserName: &user, Password: &password, Port: aspire.Float64Ptr(36162),
		Scheme: aspire.StringPtr("tcp"), WebPort: aspire.Float64Ptr(38162),
	}).WithDataBindMount(artemisDataPath).WithConfBindMount(artemisConfPath)
	artemis2 := builder.AddActiveMQArtemis("artemis2", &aspire.AddActiveMQArtemisOptions{
		UserName: &user, Password: &password,
	}).WithDataVolume().WithConfVolume()

	check(classic.Err())
	check(classic2.Err())
	check(artemis.Err())
	check(artemis2.Err())
	check(classic.PrimaryEndpoint().Err())
	check(classic.Host().Err())
	check(classic.Port().Err())
	_ = classic.UriExpression()
	_ = classic.ConnectionStringExpression()
	check(classic2.PrimaryEndpoint().Err())
	check(classic2.Host().Err())
	check(classic2.Port().Err())
	check(artemis.PrimaryEndpoint().Err())
	check(artemis.Host().Err())
	check(artemis.Port().Err())
	_ = artemis.UriExpression()
	_ = artemis.ConnectionStringExpression()
	check(artemis2.PrimaryEndpoint().Err())
	check(artemis2.Host().Err())
	check(artemis2.Port().Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
