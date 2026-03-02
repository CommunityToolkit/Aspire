// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "The additional allocation cost from logging in TrekApiClient.GetSeriesAsync is acceptable for this sample Blazor application and simplifies the example code.", Scope = "member", Target = "~M:CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.BlazorApp.TrekApiClient.GetSeriesAsync~System.Threading.Tasks.Task{System.Collections.Generic.List{CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.BlazorApp.Series}}")]
