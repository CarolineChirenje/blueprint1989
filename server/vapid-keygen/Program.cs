using System;
using System.Security.Cryptography;

// Generate VAPID key pair (P-256 curve)
using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var parameters = ecdsa.ExportParameters(true);

// Public key: uncompressed point = 0x04 || X (32 bytes) || Y (32 bytes) = 65 bytes
var pubBytes = new byte[65];
pubBytes[0] = 0x04;
Buffer.BlockCopy(parameters.Q.X!, 0, pubBytes, 1, 32);
Buffer.BlockCopy(parameters.Q.Y!, 0, pubBytes, 33, 32);

var privBytes = parameters.D!;

string ToBase64Url(byte[] b) =>
    Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

Console.WriteLine("PUBLIC:" + ToBase64Url(pubBytes));
Console.WriteLine("PRIVATE:" + ToBase64Url(privBytes));
