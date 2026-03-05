use Log::Any '$log', default_adapter => 'Stdout';
use Mojolicious::Lite -signatures;
use Mojo::Util qw(url_escape);

use Log::Any::Adapter;
Log::Any::Adapter->set('MojoLog', logger => app->log);

plugin 'OpenTelemetry';

my $log = Log::Any->get_logger;

get '/health' => sub ($c) {
    $c->render(json => { status => 'ok', resource => 'second-layer-api' });
};

get '/theBadThing' => sub ($c) {
    $c->render(text => "Sometimes there's a crack in the world that I can peer through to see it for what it really is.");
};

my $port = $ENV{PORT} // 1337;
my $httpsPort = $ENV{HTTPS_PORT} // 2337;
$log->info("Starting second-layer Perl API on port $port");
$log->info("Starting second-layer Perl API on HTTPS port $httpsPort");

my $httpHost = $ENV{HTTP_BIND_HOST};
if (!defined $httpHost) {
    $httpHost = $^O eq 'MSWin32' ? '127.0.0.1' : '*';
}

my $httpsHost = $ENV{HTTPS_BIND_HOST};
if (!defined $httpsHost) {
    $httpsHost = $^O eq 'MSWin32' ? '127.0.0.1' : '*';
}

my $cert = $ENV{TLS_CERT};
my $key = $ENV{TLS_KEY};

my @listeners = ("http://$httpHost:$port");

if (defined $cert && defined $key && -f $cert && -f $key) {
    my $encodedCert = url_escape($cert);
    my $encodedKey = url_escape($key);
    push @listeners, "https://$httpsHost:$httpsPort?cert=$encodedCert&key=$encodedKey";
}

app->start('daemon', map { ('-l', $_) } @listeners);
