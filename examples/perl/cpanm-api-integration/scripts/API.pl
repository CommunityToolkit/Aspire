use Mojolicious::Lite -signatures;

my $port = $ENV{PORT} // 3000;

my @listeners = ("http://*:$port");

get '/' => sub ($c) {
    $c->render(text => 'HEY!');
};

get '/fleeting' => sub ($c) {
    $c->render(text => 'fragile');
};

app->start('daemon', map { ('-l', $_) } @listeners);
