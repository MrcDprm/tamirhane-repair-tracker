using System.Globalization;

namespace Tamirhane.Infrastructure.Tests;

/// <summary>Geliştirmedeki .env okuyucusu: biçim kuralları, Türkçe kültürde I harfi, sadece repo içinde arama.</summary>
public sealed class DotEnvFileTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("dotenv-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void Lines_comments_quotes_and_export_are_read()
    {
        var values = DotEnvFile.Parse("""
            # yorum
            DATABASE_URL=Host=localhost;Port=5433;Password=a=b

            export PUBLIC_ORIGIN=https://localhost:7071
            EMAIL_FROM="Tamirhane <tamirhane@miracdeprem.com>"
            VAPID_SUBJECT='mailto:a@b.c'
              CLIENT_IP_HEADER = CF-Connecting-IP
            """);

        Assert.Equal("Host=localhost;Port=5433;Password=a=b", values["DATABASE_URL"]); // ilk = ayırır, gerisi değerde
        Assert.Equal("https://localhost:7071", values["PUBLIC_ORIGIN"]);
        Assert.Equal("Tamirhane <tamirhane@miracdeprem.com>", values["EMAIL_FROM"]);
        Assert.Equal("mailto:a@b.c", values["VAPID_SUBJECT"]);
        Assert.Equal("CF-Connecting-IP", values["CLIENT_IP_HEADER"]);
        Assert.Equal(5, values.Count);
    }

    [Fact]
    public void Empty_values_and_invalid_keys_are_ignored()
    {
        var values = DotEnvFile.Parse("VAPID_PUBLIC_KEY=\nRESEND_API_KEY=\"\"\n=değer\n1ABC=x\nAD SOYAD=x\nGEÇERSİZ=x\nnot a line");

        Assert.Empty(values);
    }

    [Fact]
    public void Windows_line_endings_are_handled()
    {
        var values = DotEnvFile.Parse("A=1\r\nB=2\r\n");

        Assert.Equal(("1", "2"), (values["A"], values["B"]));
    }

    [Fact]
    public void Keys_with_an_I_are_read_under_the_turkish_culture()
    {
        // Türkçe Windows'ta büyük/küçük harfe duyarsız eşleşme VAPID'deki I'yı ı yapıyordu; burada kültür rol oynamaz.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            var values = DotEnvFile.Parse("VAPID_PUBLIC_KEY=BPub\nVAPID_PRIVATE_KEY=priv\nCLIENT_IP_HEADER=X");
            Assert.Equal(["CLIENT_IP_HEADER", "VAPID_PRIVATE_KEY", "VAPID_PUBLIC_KEY"], values.Keys.Order(StringComparer.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void The_file_is_found_from_a_subfolder_of_the_repo()
    {
        var repo = Repo();
        File.WriteAllText(Path.Combine(repo.FullName, ".env"), "DATABASE_URL=bulundu");
        var web = repo.CreateSubdirectory(Path.Combine("src", "Tamirhane.Web"));

        Assert.Equal("bulundu", DotEnvFile.Read(web.FullName)["DATABASE_URL"]);
    }

    [Fact]
    public void A_file_above_the_repo_root_is_not_used()
    {
        File.WriteAllText(Path.Combine(_root.FullName, ".env"), "DATABASE_URL=baska-proje");
        var web = Repo().CreateSubdirectory(Path.Combine("src", "Tamirhane.Web"));

        Assert.Null(DotEnvFile.Find(web.FullName));
        Assert.Empty(DotEnvFile.Read(web.FullName));
    }

    private DirectoryInfo Repo()
    {
        var repo = _root.CreateSubdirectory("repo");
        File.WriteAllText(Path.Combine(repo.FullName, "Tamirhane.slnx"), "");
        return repo;
    }
}
