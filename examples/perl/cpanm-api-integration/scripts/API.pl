use Mojolicious::Lite -signatures;

my $port = $ENV{PORT} // 3000;

my @listeners = ("http://*:$port");

get '/' => sub ($c) {
    $c->render(text => 'How fleeting, how fragile.');
};

app->start('daemon', map { ('-l', $_) } @listeners);
