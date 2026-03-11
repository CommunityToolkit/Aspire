use strict;
use warnings;

my $actual_perl_version = "$^V";

if ($actual_perl_version !~ /^v5\.42\./) {
    die "Expected perl v5.42.x from perlbrew, but got $actual_perl_version\n";
}

$| = 1;

print "perlbrew worker started\n";
print "perl version validated: $actual_perl_version\n";

while (1) {
    print "tick\n";
    sleep 5;
}
