using Inveon.Services.Emails;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<RabbitMQConsumer>();
    })
    .Build();

host.Run();
