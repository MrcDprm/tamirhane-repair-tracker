using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Tamirhane.Tests;

/// <summary>
/// Push servisi (FCM, Mozilla...) yerine: gelen istekleri saklar, adres başına cevap kodu seçilebilir.
/// Testler bildirimi bir tarayıcı gibi kendi anahtarlarıyla çözüp okur (<see cref="TestBrowser"/>).
/// </summary>
public sealed class FakePushService : HttpMessageHandler
{
    private readonly ConcurrentQueue<PushRequest> _requests = new();
    private readonly ConcurrentDictionary<string, HttpStatusCode> _answers = new();

    public IReadOnlyList<PushRequest> Requests => [.. _requests];

    public IReadOnlyList<PushRequest> To(string endpoint) => [.. _requests.Where(r => r.Endpoint == endpoint)];

    /// <summary>Bu adrese gelen isteklere verilecek cevap (varsayılan 201 Created).</summary>
    public void Answer(string endpoint, HttpStatusCode status) => _answers[endpoint] = status;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var endpoint = request.RequestUri!.AbsoluteUri;
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(ct);
        string? Header(string name) =>
            request.Headers.TryGetValues(name, out var v) ? v.Single()
            : request.Content?.Headers.TryGetValues(name, out var c) == true ? c.Single() : null;

        _requests.Enqueue(new PushRequest(endpoint, request.Headers.Authorization?.ToString(), Header("TTL"), Header("Topic"),
            Header("Urgency"), Header("Content-Encoding"), body));
        return new HttpResponseMessage(_answers.GetValueOrDefault(endpoint, HttpStatusCode.Created));
    }
}

public sealed record PushRequest(
    string Endpoint, string? Authorization, string? Ttl, string? Topic, string? Urgency, string? ContentEncoding, byte[] Body);

/// <summary>
/// Abone olan bir tarayıcı: kendi anahtar çifti ve gizli değeri vardır. Push servisinden gelen şifreli gövdeyi
/// RFC 8291 (aes128gcm) adımlarıyla çözer; tarayıcının bildirimi gerçekten okuyabildiğini böyle görürüz.
/// </summary>
public sealed class TestBrowser : IDisposable
{
    private readonly ECDiffieHellman _key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _auth = RandomNumberGenerator.GetBytes(16);

    public TestBrowser(string endpoint) => Endpoint = endpoint;

    public string Endpoint { get; }
    public string P256dh => Base64Url.EncodeToString(PublicKey);
    public string Auth => Base64Url.EncodeToString(_auth);

    private byte[] PublicKey
    {
        get
        {
            var q = _key.ExportParameters(false).Q;
            return [0x04, .. q.X!, .. q.Y!];
        }
    }

    public string Decrypt(byte[] body)
    {
        // Başlık: salt (16) | kayıt boyu (4) | anahtar uzunluğu (1) | sunucunun geçici açık anahtarı (65)
        var salt = body[..16];
        var keyLength = body[20];
        var serverKey = body[21..(21 + keyLength)];
        var cipher = body[(21 + keyLength)..];

        using var server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = serverKey[1..33], Y = serverKey[33..65] },
        });
        var shared = _key.DeriveRawSecretAgreement(server.PublicKey);

        byte[] info = [.. "WebPush: info\0"u8, .. PublicKey, .. serverKey];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, _auth, info);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());

        var plain = new byte[cipher.Length - 16];
        using (var aes = new AesGcm(cek, 16))
            aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plain);

        // Son kayıtta metin 0x02 ayracıyla biter, ardından dolgu sıfırları gelebilir.
        var end = Array.LastIndexOf(plain, (byte)0x02);
        return Encoding.UTF8.GetString(plain, 0, end);
    }

    /// <summary>Kayıt boyu alanı (RFC 8188); büyük-endian 4 bayt.</summary>
    public static uint RecordSize(byte[] body) => BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4));

    public void Dispose() => _key.Dispose();
}
