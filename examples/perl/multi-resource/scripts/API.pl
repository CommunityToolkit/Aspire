use Log::Any '$log', default_adapter => 'Stdout';
use Mojolicious::Lite -signatures;
use Mojo::Util qw(url_escape);

use Log::Any::Adapter;
Log::Any::Adapter->set('MojoLog', logger => app->log);

plugin 'OpenTelemetry';

my $log = Log::Any->get_logger;

get '/' => sub ($c) {
    $log->info("Multi-resource API: request at /");
    $c->render(text => "Hello from the multi-resource Perl API (Carton-managed)!\n");
};

get '/health' => sub ($c) {
    $c->render(json => { status => 'ok', resource => 'perl-api', package_manager => 'carton' });
};

my $port = $ENV{PORT} // 1234;
$log->info("Starting multi-resource Perl API on port $port");

my $httpsPort = $ENV{HTTPS_PORT} // 2345;
$log->info("Starting multi-resource Perl API on HTTPS port $httpsPort");

my $httpHost = $ENV{HTTP_BIND_HOST} // '*';
my $httpsHost = $ENV{HTTPS_BIND_HOST};
if (!defined $httpsHost) {
    # On Windows, wildcard HTTPS binding with IO::Socket::SSL can fail with
    # "Permission denied". Localhost is reliable for development.
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