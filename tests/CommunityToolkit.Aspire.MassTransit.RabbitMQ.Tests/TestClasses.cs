using MassTransit;

namespace CommunityToolkit.Aspire.MassTransit.RabbitMQ.Tests;

// Define a second bus interface
public interface ISecondBus : IBus;

// Example consumers for the test
public class TestConsumer : IConsumer<TestRecord>
{
    public Task Consume(ConsumeContext<TestRecord> context) => Task.CompletedTask;
}

public class TestConsumerTwo : IConsumer<TestRecordTwo>
{
    public Task Consume(ConsumeContext<TestRecordTwo> context) => Task.CompletedTask;
}

public class TestSagaState :
    ISaga,
    InitiatedBy<TestRecord>
{
    public Guid CorrelationId { get; set; }

    public DateTime? SubmitDate { get; set; }
    public DateTime? AcceptDate { get; set; }

    public Task Consume(ConsumeContext<TestRecord> context)
    {
        throw new NotImplementedException();
    }
}

// Example message contracts for the test
public record TestRecord : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public DateTime DateTime { get; set; }
};

public record TestRecordTwo : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
    public DateTime DateTime { get; set; }
};