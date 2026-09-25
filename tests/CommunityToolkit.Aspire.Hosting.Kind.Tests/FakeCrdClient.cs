// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Autorest;
using k8s.Models;
using Moq;
using System.Net;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

internal sealed class FakeCrdClient
{
    private readonly Mock<IApiextensionsV1Operations> _operations = new();
    private readonly Mock<IKubernetes> _client = new();

    public FakeCrdClient(Func<string, CancellationToken, Task<V1CustomResourceDefinition>> read)
    {
        _operations.Setup(api => api.ReadCustomResourceDefinitionWithHttpMessagesAsync(
                It.IsAny<string>(), It.IsAny<bool?>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string name, bool? _,
                IReadOnlyDictionary<string, IReadOnlyList<string>> _, CancellationToken token) =>
                new HttpOperationResponse<V1CustomResourceDefinition>
                {
                    Body = await read(name, token),
                    Response = new HttpResponseMessage(),
                });
        _client.Setup(client => client.ApiextensionsV1).Returns(_operations.Object);
    }

    public IKubernetes Client => _client.Object;

    public int ReadCount => _operations.Invocations.Count;

    public IReadOnlyList<string> ReadNames => _operations.Invocations
        .Select(invocation => (string)invocation.Arguments[0]).ToList();

    public int DisposeCount => _client.Invocations.Count(invocation => invocation.Method.Name == nameof(IDisposable.Dispose));

    public static FakeCrdClient Established(params string[] names) =>
        new((name, _) => names.Contains(name, StringComparer.Ordinal)
            ? Task.FromResult(Definition(name, established: true))
            : Task.FromException<V1CustomResourceDefinition>(new InvalidOperationException($"Unexpected CRD: {name}")));

    public static V1CustomResourceDefinition Definition(string name, bool established, bool namesAccepted = true) =>
        new()
        {
            Metadata = new V1ObjectMeta { Name = name },
            Status = new V1CustomResourceDefinitionStatus
            {
                Conditions =
                [
                    new V1CustomResourceDefinitionCondition
                    {
                        Type = "NamesAccepted",
                        Status = namesAccepted ? "True" : "False",
                    },
                    new V1CustomResourceDefinitionCondition
                    {
                        Type = "Established",
                        Status = established ? "True" : "False",
                    },
                ],
            },
        };
}
