use strict;
use warnings;

use OpenTelemetry::SDK;
use OpenTelemetry;

$| = 1;

my $tracer = OpenTelemetry->tracer_provider->tracer(
    name    => 'cpan-script-minimal-worker',
    version => '1.0'
);

print "cpan script worker started\n";

while (1) {
    $tracer->in_span('worker_tick' => sub {
        my ($span, $context) = @_;
        $span->set_attribute('example.name' => 'cpan-script-minimal');
        print "tick\n";
    });

    sleep 5;
}
