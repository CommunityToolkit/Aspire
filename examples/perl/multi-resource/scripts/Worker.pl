use strict;
use warnings;
use OpenTelemetry::SDK;
use OpenTelemetry;
use IO::Async::Loop;
use IO::Async::Timer::Periodic;

# Worker in the multi-resource example — uses cpanm + WithPackage()
# while the API uses Carton. Shows mixed package managers in one AppHost.

$| = 1; # autoflush stdout so prints appear in Aspire dashboard immediately

my $tracer = OpenTelemetry->tracer_provider->tracer(
    name    => 'multi-resource-worker',
    version => '1.0'
);

my $loop = IO::Async::Loop->new;

$loop->add(IO::Async::Timer::Periodic->new(
    interval => 5,
    on_tick  => sub {
        $tracer->in_span('worker_tick' => sub {
            my ($span, $context) = @_;
            $span->set_attribute('worker.resource' => 'perl-worker');
            $span->add_event(name => 'log_emitted');
            print "Worker tick.\n";
        });
    },
)->start);

print "Multi-resource worker started (cpanm-managed)...\n";
$loop->run;
