// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Management;

internal static class GlitchTipPagination
{
    internal static IEnumerable<string> SplitLinks(IEnumerable<string> headers)
    {
        foreach (var header in headers)
        {
            var value = header.Trim();
            // GlitchTip 6.2.6's ORM paginator serializes a Python set around the
            // standard Link header. Its raw SQL paginator uses standard syntax.
            if ((value.StartsWith("{'", StringComparison.Ordinal) && value.EndsWith("'}", StringComparison.Ordinal)) ||
                (value.StartsWith("{\"", StringComparison.Ordinal) && value.EndsWith("\"}", StringComparison.Ordinal)))
            {
                value = value[2..^2];
            }

            foreach (var link in value.Split(','))
            {
                yield return link;
            }
        }
    }
}
