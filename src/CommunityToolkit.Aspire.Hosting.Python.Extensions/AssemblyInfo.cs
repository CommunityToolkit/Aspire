using System.Diagnostics.CodeAnalysis;

// We'll link ourselves to the official Aspire exerpimental warning code so that if they do
// a refactor of the official package we are not caught with a non-experimental, yet unaligned implementation.
[assembly: Experimental("ASPIREHOSTINGPYTHON001", UrlFormat = "https://aka.ms/dotnet/aspire/diagnostics#{0}")]
