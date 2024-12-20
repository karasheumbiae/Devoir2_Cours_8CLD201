using Azure.Storage.Blobs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BusFunction
{ 
public class BusFunction
{
    [Function("BusFunction")]
    public async Task Run(
        [ServiceBusTrigger("imagequeue", Connection = "ServiceBusConnectionString")] string fileName,
        FunctionContext context)
    {
        var logger = context.GetLogger("BusFunction");
        logger.LogInformation($"Processing file: {fileName}");

        // Variables pour les conteneurs Blob
        string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        string sourceContainerName = "images";
        string destinationContainerName = "processed";

        try
        {
            // Créer les clients pour les conteneurs
            BlobContainerClient sourceContainer = new BlobContainerClient(connectionString, sourceContainerName);
            BlobClient sourceBlob = sourceContainer.GetBlobClient(fileName);

            if (!await sourceBlob.ExistsAsync())
            {
                logger.LogWarning($"File '{fileName}' does not exist in the source container.");
                return;
            }

            // Télécharger le fichier depuis le conteneur source
            MemoryStream inputBlobStream = new MemoryStream();
            await sourceBlob.DownloadToAsync(inputBlobStream);
            inputBlobStream.Position = 0;

            // Traiter l'image (redimensionner ou ajouter un watermark)
            MemoryStream outputBlobStream = new MemoryStream();
            using (SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(inputBlobStream))
            {
                // Redimensionner l'image
                int newWidth = image.Width / 2;
                int newHeight = image.Height / 2;
                image.Mutate(x => x.Resize(newWidth, newHeight));

                // Sauvegarder dans le flux mémoire
                image.Save(outputBlobStream, new JpegEncoder());
            }
            outputBlobStream.Position = 0;

            // Téléverser l'image traitée dans le conteneur destination
            BlobContainerClient destinationContainer = new BlobContainerClient(connectionString, destinationContainerName);
            BlobClient destinationBlob = destinationContainer.GetBlobClient(fileName);
            await destinationBlob.UploadAsync(outputBlobStream, overwrite: true);

            // Supprimer le fichier original du conteneur source
            await sourceBlob.DeleteAsync();

            logger.LogInformation($"File '{fileName}' processed and moved to 'processed' container.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error processing file '{fileName}': {ex.Message}");
        }
    }
}
}