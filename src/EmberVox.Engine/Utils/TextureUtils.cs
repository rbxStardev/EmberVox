using EmberVox.Core.Types;
using EmberVox.Rendering.RenderingManagement;
using StbImageSharp;

namespace EmberVox.Engine.Utils;

public static class TextureUtils
{
    public static TextureData GenDataFromImage(string imagePath)
    {
        StbImage.stbi_set_flip_vertically_on_load(1);
        ImageResult imageResult = ImageResult.FromStream(
            File.OpenRead(imagePath),
            ColorComponents.RedGreenBlueAlpha
        );

        return new TextureData(imageResult.Width, imageResult.Height, imageResult.Data);
    }

    public static TextureData GetDataFromNoise(int width, int height, FastNoiseLite noise)
    {
        byte[] noiseData = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float noiseValue = noise.GetNoise(x, y);
                byte mappedValue = (byte)((noiseValue + 1f) / 2f * 255f);

                int i = (x + y * width) * 4;
                noiseData[i] = mappedValue;
                noiseData[i + 1] = mappedValue;
                noiseData[i + 2] = mappedValue;
                noiseData[i + 3] = 255;
            }
        }

        return new TextureData(width, height, noiseData);
    }
}
