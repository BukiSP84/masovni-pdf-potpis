using System.Net;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tsp;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Utilities.Collections;

/// <summary>Minimalni RFC 3161 TSA server samo za testiranje (samopotpisan certifikat).</summary>
sealed class LokalniTsa : IDisposable
{
    readonly HttpListener _l = new();
    readonly TimeStampTokenGenerator _gen;
    long _serijski = 1;
    public string Url { get; }

    public LokalniTsa(int port)
    {
        var kpg = new RsaKeyPairGenerator();
        kpg.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        AsymmetricCipherKeyPair kp = kpg.GenerateKeyPair();
        var cg = new X509V3CertificateGenerator();
        var dn = new X509Name("CN=Test TSA");
        cg.SetSerialNumber(BigInteger.One); cg.SetIssuerDN(dn); cg.SetSubjectDN(dn);
        cg.SetNotBefore(DateTime.UtcNow.AddDays(-1)); cg.SetNotAfter(DateTime.UtcNow.AddYears(1));
        cg.SetPublicKey(kp.Public);
        cg.AddExtension(X509Extensions.ExtendedKeyUsage, true, new ExtendedKeyUsage(KeyPurposeID.id_kp_timeStamping));
        var cert = cg.Generate(new Asn1SignatureFactory("SHA256WithRSA", kp.Private));
        _gen = new TimeStampTokenGenerator(kp.Private, cert, Org.BouncyCastle.Tsp.TspAlgorithms.Sha256, "1.2.3.4");
        _gen.SetCertificates(CollectionUtilities.CreateStore(new[] { cert }));
        Url = $"http://127.0.0.1:{port}/";
        _l.Prefixes.Add(Url);
        _l.Start();
        _ = Task.Run(Petlja);
    }

    async Task Petlja()
    {
        while (_l.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _l.GetContextAsync(); } catch { return; }
            try
            {
                using var ms = new MemoryStream(); await ctx.Request.InputStream.CopyToAsync(ms);
                var req = new TimeStampRequest(ms.ToArray());
                var resGen = new TimeStampResponseGenerator(_gen, TspAlgorithms.Allowed);
                var res = resGen.Generate(req, BigInteger.ValueOf(_serijski++), DateTime.UtcNow);
                var b = res.GetEncoded();
                ctx.Response.ContentType = "application/timestamp-reply";
                await ctx.Response.OutputStream.WriteAsync(b);
            }
            catch (Exception ex) { Console.Error.WriteLine("TSA: " + ex.Message); ctx.Response.StatusCode = 500; }
            ctx.Response.Close();
        }
    }

    public void Dispose() { try { _l.Stop(); } catch { } }
}
