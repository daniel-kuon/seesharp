public class BruchTests
{
    [Fact]
    public void Konstruktor_setzt_Zaehler_Nenner_und_Etikett()
    {
        var b = new Bruch(1, 2, "b1");

        Assert.Equal(1, b.Zaehler);
        Assert.Equal(2, b.Nenner);
        Assert.Equal("b1", b.Etikett);
    }

    [Fact]
    public void Standardkonstruktor_liefert_null_durch_eins_ohne_Etikett()
    {
        var b = new Bruch();

        Assert.Equal(0, b.Zaehler);
        Assert.Equal(1, b.Nenner);
        Assert.Equal("", b.Etikett);
    }

    [Fact]
    public void Konstruktor_ignoriert_Nenner_null()
    {
        var b = new Bruch(3, 0, "x");

        Assert.Equal(1, b.Nenner);
    }

    [Fact]
    public void Konstruktor_kuerzt_Etikett_auf_40_Zeichen()
    {
        var b = new Bruch(1, 1, new string('a', 50));

        Assert.Equal(40, b.Etikett.Length);
    }

    [Fact]
    public void Objektinitialisierer_funktioniert()
    {
        var b = new Bruch { Zaehler = 3, Etikett = "b3", Nenner = 7 };

        Assert.Equal((3, 7, "b3"), (b.Zaehler, b.Nenner, b.Etikett));
    }

    [Fact]
    public void Kuerze_teilt_durch_den_ggT()
    {
        var b = new Bruch(6, 8, "");
        b.Kuerze();

        Assert.Equal((3, 4), (b.Zaehler, b.Nenner));
    }

    [Fact]
    public void Kuerze_macht_aus_null_Zaehler_null_durch_eins()
    {
        var b = new Bruch(0, 8, "");
        b.Kuerze();

        Assert.Equal((0, 1), (b.Zaehler, b.Nenner));
    }

    [Fact]
    public void Addiere_Bruch_addiert_und_kuerzt()
    {
        var b = new Bruch(1, 2, "b1");
        b.Addiere(new Bruch(1, 4, "b2"));

        Assert.Equal((3, 4), (b.Zaehler, b.Nenner));
    }

    [Fact]
    public void Addiere_Zahlen_mit_autokurz()
    {
        var b = new Bruch(1, 2, "");

        Assert.True(b.Addiere(1, 2, autokurz: true));
        Assert.Equal((1, 1), (b.Zaehler, b.Nenner));
    }

    [Fact]
    public void Addiere_Zahlen_ohne_autokurz_kuerzt_nicht()
    {
        var b = new Bruch(1, 2, "");

        Assert.True(b.Addiere(1, 2, autokurz: false));
        Assert.Equal((4, 4), (b.Zaehler, b.Nenner));
    }

    [Fact]
    public void Addiere_mit_Nenner_null_liefert_false_und_aendert_nichts()
    {
        var b = new Bruch(1, 2, "");

        Assert.False(b.Addiere(1, 0, autokurz: true));
        Assert.Equal((1, 2), (b.Zaehler, b.Nenner));
    }
}
