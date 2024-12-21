using System;
using System.IO;
using Azure.Messaging.ServiceBus;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BlobTriggerFunction
{
    public class BlobTriggerFunction
    {
        [Function("BlobTriggerFunction")]
        public async Task Run(
            [BlobTrigger("images/{name}", Connection = "AzureWebJobsStorage")] Stream inputBlob,
            string name,
            FunctionContext context)
        {
            var logger = context.GetLogger("BlobTriggerFunction");
            logger.LogInformation($"Blob trigger function executed. File name: {name}");

            try
            {
                // Lire la chaîne de connexion à Service Bus
                string serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
                if (string.IsNullOrEmpty(serviceBusConnectionString))
                {
                    throw new Exception("ServiceBusConnectionString is not set in the environment variables.");
                }

                string queueName = "imagequeue";

                // Initialiser le client Service Bus
                await using var client = new ServiceBusClient(serviceBusConnectionString);
                ServiceBusSender sender = client.CreateSender(queueName);

                // Créer et envoyer un message
                var message = new ServiceBusMessage($"File processed: {name}");
                await sender.SendMessageAsync(message);

                logger.LogInformation($"File name '{name}' successfully sent to Service Bus queue '{queueName}'.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error sending file name to Service Bus: {ex.Message}");
            }
        }
    }
}
