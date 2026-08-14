using System.Security.Cryptography;
using System.Text;

namespace Dayline.BuildingBlocks.Utils.Extensions;

public static class HashExtensions
{
    private static readonly byte[] Empty = [];

    public static int CombineHashCodes(this IEnumerable<object?> objs)
    {
        unchecked
        {
            const int prime = 31;
            return objs.Aggregate(17, (current, obj) => current * prime + (obj?.GetHashCode() ?? 0));
        }
    }

    public static byte[] Hash(this string plainText, HashAlgorithm? hashAlgorithm = null, string encoding = "gb2312")
    {
        // get bytes from the plaintext
        var bytes = Encoding.GetEncoding(encoding).GetBytes(plainText);

        // encrypt
        using var algorithm = hashAlgorithm ?? MD5.Create();
        return algorithm.ComputeHash(bytes);
    }

    public static byte[] Md5(this string input, string encoding = "gb2312")
    {
        return string.IsNullOrEmpty(input) ? Empty : input.Hash(MD5.Create(), encoding);
    }

    public static byte[] Sha512(this string input, string encoding = "gb2312")
    {
        return string.IsNullOrEmpty(input) ? Empty : input.Hash(SHA512.Create(), encoding);
    }

    public static byte[] Sha256(this string input, string encoding = "gb2312")
    {
        return string.IsNullOrEmpty(input) ? Empty : input.Hash(SHA256.Create());
    }

    public static byte[] Sha1(this string input, string encoding = "gb2312")
    {
        return string.IsNullOrEmpty(input) ? Empty : input.Hash(SHA1.Create());
    }

    public static byte[]? Sha256(this byte[]? input)
    {
        return input == null ? null : SHA256.HashData(input);
    }
}