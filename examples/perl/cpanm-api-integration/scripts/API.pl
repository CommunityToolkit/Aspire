use Mojolicious::Lite -signatures;

my $port = $ENV{PORT} // 3000;

my @listeners = ("http://*:$port");

get '/' => sub ($c) {
    $c->render(text => 'HEY!');
};

get '/fleeting' => sub ($c) {
    $c->render(text => 'fragile');
};

get '/cert-env' => sub ($c) {
    my @required = qw(SSL_CERT_FILE PERL_LWP_SSL_CA_FILE MOJO_CA_FILE);
    my $has_all = !grep { !defined $ENV{$_} || $ENV{$_} eq '' } @required;

    $c->render(text => $has_all ? 'present' : 'missing');
};

app->start('daemon', map { ('-l', $_) } @listeners);
