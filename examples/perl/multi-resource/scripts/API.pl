use Log::Any '$log', default_adapter => 'Stdout';
use Mojolicious::Lite -signatures;

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

my $port = $ENV{PORT} // 3031;
app->start('daemon', '-l', "http://*:$port");
