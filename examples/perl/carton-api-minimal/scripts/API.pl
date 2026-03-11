use Log::Any '$log', default_adapter => 'Stdout';
use Mojolicious::Lite -signatures;
use Mojo::Util qw(url_escape);

use Log::Any::Adapter;
Log::Any::Adapter->set('MojoLog', logger => app->log);

plugin 'OpenTelemetry';

my $port = $ENV{PORT} // 3000;
my $httpsPort = $ENV{HTTPS_PORT} // 3001;

my $httpsHost = $ENV{HTTPS_BIND_HOST};
if (!defined $httpsHost) {
    $httpsHost = $^O eq 'MSWin32' ? '127.0.0.1' : '*';
}

my $cert = $ENV{TLS_CERT};
my $key = $ENV{TLS_KEY};

my @listeners = ("http://*:$port");
if (defined $cert && defined $key && -f $cert && -f $key) {
    my $encodedCert = url_escape($cert);
    my $encodedKey = url_escape($key);
    push @listeners, "https://$httpsHost:$httpsPort?cert=$encodedCert&key=$encodedKey";
}

get '/' => sub ($c) {
    $c->render(text => "hello from carton api minimal\n");
};

get '/health' => sub ($c) {
    $c->render(json => { status => 'ok', example => 'carton-api-minimal' });
};

app->start('daemon', map { ('-l', $_) } @listeners);
