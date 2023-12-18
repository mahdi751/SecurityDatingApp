using API.Helpers;
using API.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;


namespace API.Services;

public class PhotoService : IPhotoService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<PhotoService> _logger; 

    public PhotoService(IOptions<CloudinarySettings> config,ILogger<PhotoService> logger)
    {
        var acc = new Account
        (
            config.Value.CloudName,
            config.Value.ApiKey,
            config.Value.ApiSecret
        );

        _cloudinary = new Cloudinary(acc);
        
            _logger = logger;
    }

    public async Task<ImageUploadResult> AddPhotoAsync(IFormFile file)
    {
        var uploadResult = new ImageUploadResult();

        if (file.Length > 0)
        {
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Transformation = new Transformation().Height(500).Width(500).Crop("fill").Gravity("face"),
                Folder = "da-net7u"
            };
            uploadResult = await _cloudinary.UploadAsync(uploadParams);
        }

        return uploadResult;
    }

    public async Task<DeletionResult> DeletePhotoAsync(string publicId)
    {
        var deleteParams = new DeletionParams(publicId);

        return await _cloudinary.DestroyAsync(deleteParams);
    }

    public double CalculateImageSimilarity(Stream image1Stream, Stream image2Stream)
    {
        try
        {
            using (var image1 = Image.Load<Rgb24>(image1Stream))
            using (var image2 = Image.Load<Rgb24>(image2Stream))
            {
                // Optionally, experiment with different resizing options or remove resizing.
                image1.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(256, 256),
                    Mode = ResizeMode.Max
                }));

                image2.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(256, 256),
                    Mode = ResizeMode.Max
                }));

                double mse = 0;

                for (int y = 0; y < image1.Height; y++)
                {
                    for (int x = 0; x < image1.Width; x++)
                    {
                        var pixel1 = image1[x, y];
                        var pixel2 = image2[x, y];

                        mse += Math.Pow(pixel1.R - pixel2.R, 2) +
                            Math.Pow(pixel1.G - pixel2.G, 2) +
                            Math.Pow(pixel1.B - pixel2.B, 2);
                    }
                }

                // Calculate the mean squared error
                mse /= (image1.Width * image1.Height * 3);

                // Normalize the MSE to a similarity score between 0 and 1
                var similarityScore = Math.Max(0, 1 - Math.Sqrt(mse) / 255.0);

                return similarityScore;
            }
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException ex)
        {
            // Log or handle the exception appropriately.
            // For example, log the file formats causing the issue.
            _logger.LogError("Image format not supported: {0}", ex.Message);
            throw; // Rethrow the exception to maintain the original behavior
        }
        catch (Exception ex)
        {
            // Log or handle other exceptions appropriately.
            _logger.LogError("Error calculating image similarity: {0}", ex.ToString());
            throw; // Rethrow the exception to maintain the original behavior
        }
    }
}
