use strict;
use warnings;

my $actual_perl_version = "$^V";

if ($actual_perl_version =~ /^v5\.38\./) {
    print "PASS: running on expected perl series $actual_perl_version\n";
    exit 0;
}

print "FAIL: expected perl v5.38.x but got $actual_perl_version\n";
exit 1;
