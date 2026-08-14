using System.Text;

namespace Dayline.BuildingBlocks.Utils.Extensions;

public static class ByteArrayExtensions
{
    extension(byte[] input)
    {
        public string ToBase64String()
        {
            return Convert.ToBase64String(input);
        }

        public string ToUrlSuitable()
        {
            return input.ToBase64String().Replace("+", "-").Replace("/", "_").Replace("=", "%3d");
        }

        public string ToHexString()
        {
            var hex = new StringBuilder(input.Length * 2);
            foreach (var b in input) hex.Append($"{b:x2}");

            return hex.ToString();
        }
    }
}