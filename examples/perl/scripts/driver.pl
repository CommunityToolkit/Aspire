use strict;
use warnings;
use HTTP::Tiny;

$| = 1; # autoflush stdout so prints appear in Aspire dashboard immediately

my $api_url = $ENV{API_URL};
die "API_URL environment variable is not set\n" unless defined $api_url && $api_url ne '';

# Strip trailing slash if present
$api_url =~ s{/\z}{};

my $target = "$api_url/fleeting";
my $http = HTTP::Tiny->new(timeout => 5);

print "Driver started — polling $target every 3 seconds...\n";

while (1) {
    my $response = $http->get($target);

    if ($response->{success}) {
        print "[OK] $target => $response->{content}\n";
    } else {
        print "[ERR] $target => $response->{status} $response->{reason}\n";
    }

    sleep 3;
}
