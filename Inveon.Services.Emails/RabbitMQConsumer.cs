
using Inveon.Services.Emails.Messages;
using MailKit.Net.Smtp;
using MimeKit;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;


namespace Inveon.Services.Emails
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly ILogger<RabbitMQConsumer> _logger;
        private IConnection _connection;
        private readonly IModel _channel;

        public RabbitMQConsumer(ILogger<RabbitMQConsumer> logger)
        {
            _logger = logger;
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = "localhost",
                    UserName = "guest",
                    Password = "guest"
                };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(queue: "checkoutqueue", durable: false, exclusive: false, autoDelete: false, arguments: null);
            }
            catch (Exception)
            {
                //log exception
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (model, e) =>
            {
                var body = e.Body;
                var json = System.Text.Encoding.UTF8.GetString(body.ToArray());
                MessageDto messageDto = JsonConvert.DeserializeObject<MessageDto>(json);
                sendMessage(messageDto);
            };
            _channel.BasicConsume("checkoutqueue", false, consumer);

            // Until cancellation is requested, task will be delayed and all new messages will be consumed.
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
      
            return base.StopAsync(cancellationToken);
        }


        public void sendMessage(MessageDto messageDto)
        {
            MimeMessage message = new MimeMessage();
            message.From.Add(new MailboxAddress("Inveon Test", "dyavuzemre@gmail.com"));

            message.To.Add(MailboxAddress.Parse(messageDto.Email));
            String email = prepareEmailContent(messageDto);
            message.Subject = "Order Details";
            message.Body = new TextPart("plain")
            {
                Text = email
            };

            SmtpClient client = new SmtpClient();
            try
            {
                client.Connect("smtp.gmail.com", 465, true);
                client.Authenticate("dyavuzemre@gmail.com", "ewnerldkhkinfqpg");
                client.Send(message);

            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
            }
            finally
            {
                client.Disconnect(true);
                client.Dispose();



            }


        }

            public String prepareEmailContent(MessageDto messageDto)
        {
            String email = 
            @$"Sayın {messageDto.FirstName} {messageDto.LastName},Siparisiniz onaylanmıstır.
>>> Siparis Bilgileri <<< 
--- Siparis Tutarı : {messageDto.OrderTotal}
--- İndirim Tutarı : {messageDto.DiscountTotal}
--- Sipariş Zamanı : {messageDto.PickupDateTime.ToString("MM/dd/yyyy h:mm tt")}
--- Telefon Numaranız : {messageDto.Phone}
--- Email Adresiniz : {messageDto.Email}";
            return email;


        }
    }
}